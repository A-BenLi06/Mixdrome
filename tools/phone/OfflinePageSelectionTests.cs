using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mixdrome.Phone.Common;
using Mixdrome.Phone.Services;
using Mixdrome.Phone.Views;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// run-offline.ps1 inserts the exact production page methods. Managed controls
// and delayed storage checks exercise selection ownership without native UI.
public static class PhoneOfflinePageSelectionTests
{
    private static int assertions;
    private static void Check(bool condition, string message) { ++assertions; if (!condition) throw new Exception(message); }
    private static OfflineMixesPage Page()
    {
        PhoneOfflineMixes.Reset();
        var page = new OfflineMixesPage();
        foreach (var mix in PhoneOfflineMixes.Saved)
            page.Cards.Add(new PhoneOfflineCard { Saved = mix, Name = mix.Name });
        return page;
    }
    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition()) { if (DateTime.UtcNow >= deadline) throw new Exception("A controlled storage operation did not start."); await Task.Delay(1); }
    }
    private static string StartedMix(OfflineMixesPage page)
    { return ((PlaybackNavigationParameter)page.Frame.Navigations.Single()).Playlist[0].Id.Substring(0, 1); }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production phone offline-page latest-selection, refresh, departure and failure assertions. Native storage/UI remain unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        var page = Page();
        var a = page.Open(page.Cards[0]); var b = page.Open(page.Cards[1]);
        PhoneOfflineMixes.Validations[0].SetResult(true); await a;
        Check(page.Frame.Navigations.Count == 0, "The faster prior A validation cannot navigate after B was selected.");
        PhoneOfflineMixes.Validations[1].SetResult(true); await b;
        Check(StartedMix(page) == "B", "Only the latest selected offline mix starts playback.");
        var parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single();
        Check(parameter.IsMix && parameter.StartIndex == 0 && parameter.MixTitle == "Mix B"
            && parameter.Playlist.Select(song => song.Id).SequenceEqual(new[] { "B1", "B1", "B2" }),
            "Offline selection retains its complete duplicate-preserving queue and mix metadata.");

        page = Page(); a = page.Open(page.Cards[0]); b = page.Open(page.Cards[1]);
        PhoneOfflineMixes.Validations[0].SetResult(false); await a;
        Check(PhoneOfflineMixes.ReadCount == 0 && page.Status == string.Empty, "A stale missing-file result cannot refresh the list or show an obsolete error.");
        PhoneOfflineMixes.Validations[1].SetResult(true); await b;
        Check(StartedMix(page) == "B", "A stale invalid queue cannot suppress the newer valid selection.");

        page = Page(); a = page.Open(page.Cards[0]); b = page.Open(page.Cards[1]);
        PhoneOfflineMixes.Validations[0].SetException(new InvalidOperationException("Old validation failure")); await a;
        Check(page.Status == string.Empty, "An older validation exception cannot overwrite the newer selection's state.");
        PhoneOfflineMixes.Validations[1].SetResult(true); await b;
        Check(StartedMix(page) == "B", "The latest selection still succeeds after an old validation exception.");

        page = Page(); a = page.Open(page.Cards[0]);
        await page.Reload(); PhoneOfflineMixes.Validations[0].SetResult(true); await a;
        Check(page.Frame.Navigations.Count == 0 && page.Cards.Count == 2, "Refreshing invalidates old validation while retaining the refreshed mix list.");
        var oldCard = page.Cards[0]; await page.Reload(); await page.Open(oldCard);
        Check(PhoneOfflineMixes.Validations.Count == 1, "An item from a replaced list cannot start another storage validation.");

        page = Page(); a = page.Open(page.Cards[0]);
        await page.Reload(); PhoneOfflineMixes.Validations[0].SetException(new InvalidOperationException("Stale after refresh")); await a;
        Check(page.Status == string.Empty && page.Frame.Navigations.Count == 0, "A pre-refresh exception cannot affect the current list or navigation.");

        page = Page(); a = page.Open(page.Cards[0]); page.Leave();
        PhoneOfflineMixes.Validations[0].SetResult(true); await a;
        Check(page.Frame.Navigations.Count == 0, "Leaving rejects a pending successful queue validation.");
        page = Page(); a = page.Open(page.Cards[0]); page.Leave();
        PhoneOfflineMixes.Validations[0].SetException(new InvalidOperationException("Stale after departure")); await a;
        Check(page.Status == string.Empty, "Leaving also rejects late validation errors.");

        page = Page(); a = page.Open(page.Cards[0]);
        PhoneOfflineMixes.Validations[0].SetResult(false); await a;
        Check(PhoneOfflineMixes.ReadCount == 1 && page.Status == "OfflineFileMissing" && page.Frame.Navigations.Count == 0,
            "A current missing file still refreshes availability and reports its actual failure.");
        page = Page(); a = page.Open(page.Cards[0]);
        PhoneOfflineMixes.Validations[0].SetException(new InvalidOperationException("Current validation failure")); await a;
        Check(page.Status.StartsWith("LoadFailed") && page.Frame.Navigations.Count == 0, "A current validation exception remains visible without starting playback.");

        page = Page(); page.BeginDownload(); await page.Reload();
        Check(page.Cards.Count == 3 && page.Cards[0].IsDownloading, "Refreshing still retains the active foreground download card.");
        page.Leave();
        Check(page.DownloadCancelled, "The selection fix preserves foreground download cancellation on departure.");

        page = Page();
        var refresh = new TaskCompletionSource<IList<PhoneSavedMix>>();
        PhoneOfflineMixes.Read = () => refresh.Task;
        a = page.Open(page.Cards[0]); PhoneOfflineMixes.Validations[0].SetResult(false);
        await WaitFor(() => PhoneOfflineMixes.ReadCount == 1);
        b = page.Open(page.Cards[1]);
        refresh.SetResult(PhoneOfflineMixes.Saved); await a;
        Check(page.Status == string.Empty, "A newer click supersedes an older missing-file message while refresh is pending.");
        PhoneOfflineMixes.Validations[1].SetResult(true); await b;
        Check(StartedMix(page) == "B", "The latest validation may finish after an earlier refresh replaces the displayed cards.");

        page = Page(); var localPlayMe = page.Cards[0]; localPlayMe.Saved.IsPlayMe = true;
        a = page.Open(localPlayMe); PhoneOfflineMixes.Validations[0].SetResult(true); await a;
        Check(((PlaybackNavigationParameter)page.Frame.Navigations.Single()).MixTitle == null, "A saved Play Me mix preserves its existing player presentation.");
    }
}

namespace Mixdrome.Phone.Views
{
    public sealed class OfflineMixesPage : Page
    {
        private bool active = true;
        private CancellationTokenSource download;
        private PhoneOfflineCard pending;
        private int generation, selectionGeneration;
        private readonly TextBlock StatusText = new TextBlock(), HintText = new TextBlock();
        public ObservableCollection<PhoneOfflineCard> Cards { get; private set; } = new ObservableCollection<PhoneOfflineCard>();
        public OfflineMixesPage() { Frame.Navigated = () => OnNavigatedFrom(new NavigationEventArgs()); }
        public Task Open(PhoneOfflineCard card) { return OpenMixAsync(card); }
        public Task Reload() { return Refresh(); }
        public void Leave() { OnNavigatedFrom(new NavigationEventArgs()); }
        public void BeginDownload() { download = new CancellationTokenSource(); pending = new PhoneOfflineCard { Name = "Pending", IsDownloading = true }; }
        public bool DownloadCancelled { get { return download != null && download.IsCancellationRequested; } }
        public string Status { get { return StatusText.Text; } }
        private void ResizeCards() { }
        private void OnBackPressed(object sender, BackPressedEventArgs args) { }
/* PRODUCTION_PAGE_METHODS */
    }
    public sealed class PhoneOfflineCard { public string Name, Artwork; public PhoneSavedMix Saved; public bool IsPlayMe, IsDownloading; }
    public sealed class SongDisplayItem { public string Id; }
    public sealed class PlaybackPage { }
    public sealed class PlaybackNavigationParameter { public List<SongDisplayItem> Playlist; public int StartIndex; public bool IsMix; public string MixTitle; }
}
namespace Mixdrome.Phone.Services
{
    public sealed class PhoneSavedMix
    {
        public string Id, Name, Artwork;
        public bool IsPlayMe;
        public List<SongDisplayItem> CreateQueue() { return new List<SongDisplayItem> { new SongDisplayItem { Id = Id + "1" }, new SongDisplayItem { Id = Id + "1" }, new SongDisplayItem { Id = Id + "2" } }; }
    }
    public static class PhoneOfflineMixes
    {
        public static List<PhoneSavedMix> Saved;
        public static readonly List<TaskCompletionSource<bool>> Validations = new List<TaskCompletionSource<bool>>();
        public static Func<Task<IList<PhoneSavedMix>>> Read;
        public static int ReadCount;
        public static void Reset()
        {
            Validations.Clear(); ReadCount = 0;
            Saved = new List<PhoneSavedMix> { new PhoneSavedMix { Id = "A", Name = "Mix A" }, new PhoneSavedMix { Id = "B", Name = "Mix B" } };
            Read = () => Task.FromResult<IList<PhoneSavedMix>>(Saved);
        }
        public static Task<bool> ValidateLocalQueueAsync(IEnumerable<SongDisplayItem> queue) { var result = new TaskCompletionSource<bool>(); Validations.Add(result); return result.Task; }
        public static Task<IList<PhoneSavedMix>> GetSavedMixesAsync() { ++ReadCount; return Read(); }
    }
}
namespace Mixdrome.Phone.Common
{
    public static class PhoneText
    {
        public static string Get(string key) { return key; }
        public static string Format(string key, params object[] args) { return key + ": " + string.Join(",", args); }
    }
}
namespace Windows.Phone.UI.Input
{
    public sealed class BackPressedEventArgs : EventArgs { }
    public static class HardwareButtons { public static event EventHandler<BackPressedEventArgs> BackPressed { add { } remove { } } }
}
namespace Windows.UI.Xaml { public enum Visibility { Visible, Collapsed } }
namespace Windows.UI.Xaml.Controls
{
    public class Page { public readonly Frame Frame = new Frame(); protected virtual void OnNavigatedFrom(NavigationEventArgs args) { } }
    public sealed class Frame
    {
        public Action Navigated;
        public readonly List<object> Navigations = new List<object>();
        public bool Navigate(Type type, object parameter) { Navigations.Add(parameter); if (Navigated != null) Navigated(); return true; }
    }
    public sealed class TextBlock { public string Text = string.Empty; public Visibility Visibility; }
}
namespace Windows.UI.Xaml.Navigation { public sealed class NavigationEventArgs : EventArgs { } }
