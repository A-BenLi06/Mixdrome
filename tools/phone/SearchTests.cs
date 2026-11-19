using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Client.Common.Services.DataStructures.SubsonicService;
using Mixdrome.Phone.Services;
using Mixdrome.Phone.Views;
using Windows.Phone.UI.Input;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// Exercises the real SearchPage source and protocol against delayed HTTP peers.
// Managed controls expose state transitions, not native XAML rendering or input.
public static class PhoneSearchTests
{
    private static int assertions;
    private static void Check(bool condition, string message) { ++assertions; if (!condition) throw new Exception(message); }
    private static object Call(SearchPage page, string method, params object[] arguments)
    { return typeof(SearchPage).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, arguments); }
    private static Task Search(SearchPage page, string query, bool append = false)
    { return (Task)Call(page, "Search", query, append); }
    private static string Response(string body)
    { return "<subsonic-response xmlns='http://subsonic.org/restapi' status='ok' version='1.16.1'>" + body + "</subsonic-response>"; }
    private static int Number(ProtocolRequest request, string name)
    { int value; return int.TryParse(request.Parameter(name), out value) ? value : 0; }
    private static string SearchResponse(ProtocolRequest request)
    {
        var body = new StringBuilder("<searchResult3>");
        foreach (var kind in new[] { "artist", "album", "song" })
        {
            var offset = Number(request, kind + "Offset");
            var count = Math.Min(Number(request, kind + "Count"), Math.Max(0, 120 - offset));
            for (var index = offset; index < offset + count; ++index)
                body.Append("<" + kind + " id='" + kind + index + "' name='" + kind + index + "' title='" + kind + index + "' artist='Artist' duration='60' albumCount='1' />");
        }
        return Response(body.Append("</searchResult3>").ToString());
    }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production phone search initialization, stale completion, spinner, paging and Back assertions. Native rendering remains unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        ApplicationData.Current.LocalSettings.Values.Clear(); PasswordVault.Credentials.Clear();
        using (var server = new ProtocolServer(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("ping.view")) return new ProtocolResponse { Body = Response(""), DelayMilliseconds = 250 };
            return new ProtocolResponse { Body = SearchResponse(request), DelayMilliseconds = request.Parameter("query") == "slow" ? 350 : 0 };
        }))
        {
            PhoneSettings.Save(new SubsonicServiceConfiguration { PrimaryUrl = server.Url, BaseUrl = server.Url, Username = "fixture", Password = "secret" });
            var page = new SearchPage();
            Call(page, "OnNavigatedTo", new NavigationEventArgs());
            var initialQuery = Search(page, "music");
            Check(page.TestProgress.IsActive && page.TrackResults.Count == 0, "An early query waits for in-flight account initialization.");
            await initialQuery;
            Check(page.ArtistResults.Count == 100 && page.AlbumResults.Count == 100 && page.TrackResults.Count == 100, "The first search requests one page from every category.");
            Check(!page.TestProgress.IsActive && page.TestMore.Visibility == Visibility.Visible, "A complete search clears loading and exposes additional results.");
            Check(page.TestEmpty.Visibility == Visibility.Collapsed, "Early submission is not misreported as an unconfigured account.");
            page.TestPivot.SelectedIndex = 1; Call(page, "OnResultsTabChanged", null, new SelectionChangedEventArgs());
            await Search(page, "music", true);
            var moreRequest = server.Requests.Last(request => request.Parameter("query") == "music");
            Check(moreRequest.Parameter("albumOffset") == "100" && moreRequest.Parameter("albumCount") == "100" && moreRequest.Parameter("artistCount") == "0" && moreRequest.Parameter("songCount") == "0", "More uses the selected category's offset and requests no repeated categories.");
            Check(page.AlbumResults.Count == 120 && page.ArtistResults.Count == 100 && page.SongResults.Count == 100, "Appending a category preserves its peers and song queue.");
            Check(page.TestMore.Visibility == Visibility.Collapsed, "A final partial page hides More for its category.");
            page.TestPivot.SelectedIndex = 2; Call(page, "OnResultsTabChanged", null, new SelectionChangedEventArgs());
            Check(page.TestMore.Visibility == Visibility.Visible, "Each category retains its independent next-page state.");
            await Search(page, "music", true);
            Check(page.SongResults.Count == 120 && page.SongResults.Last().Track == 120, "Song paging preserves occurrence order and ordinals.");

            var slow = Search(page, "slow");
            Check(page.TestProgress.IsActive, "An in-flight search shows loading.");
            await Search(page, "   ");
            Check(!page.TestProgress.IsActive && page.Results.Count == 0 && page.TestMore.Visibility == Visibility.Collapsed, "Empty submission clears loading, results and paging immediately.");
            await slow;
            Check(!page.TestProgress.IsActive && page.Results.Count == 0, "A stale response cannot repopulate cleared results or restore loading.");
            slow = Search(page, "slow");
            Call(page, "OnClearClick", null, new RoutedEventArgs());
            await slow;
            Check(!page.TestProgress.IsActive && page.Results.Count == 0, "Clear cancels the visible effect of an in-flight request.");
            var handledBack = new BackPressedEventArgs { Handled = true };
            Call(page, "OnHardwareBackPressed", null, handledBack);
            Check(page.Frame.BackCount == 0, "Already-handled Back does not navigate again.");
            Call(page, "OnNavigatedFrom", new NavigationEventArgs());

            var leaving = new SearchPage();
            Call(leaving, "OnNavigatedTo", new NavigationEventArgs());
            var abandoned = Search(leaving, "music");
            Call(leaving, "OnNavigatedFrom", new NavigationEventArgs());
            await abandoned;
            Check(leaving.Results.Count == 0 && !leaving.TestProgress.IsActive, "Leaving during account initialization discards service/query completion and loading.");
        }
    }
}

namespace Windows.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public enum FocusState { Programmatic }
    public class RoutedEventArgs : EventArgs { }
}
namespace Windows.UI.Xaml.Controls
{
    public class Control { public Visibility Visibility = Visibility.Visible; public bool IsEnabled = true; }
    public class Page : Control
    {
        private readonly Dictionary<string, object> controls = new Dictionary<string, object>();
        public object DataContext;
        public Frame Frame = new Frame();
        protected virtual void OnNavigatedTo(NavigationEventArgs e) { }
        protected virtual void OnNavigatedFrom(NavigationEventArgs e) { }
        protected void RegisterName(string name, object value) { controls[name] = value; }
        public object FindName(string name) { return controls[name]; }
    }
    public sealed class Frame
    {
        public bool CanGoBack = true; public int BackCount;
        public bool Navigate(Type type, object parameter) { return true; }
        public void GoBack() { ++BackCount; }
    }
    public sealed class TextBox : Control { public string Text = ""; public bool Focus(FocusState state) { return true; } }
    public sealed class TextBlock : Control { public string Text; }
    public sealed class ProgressRing : Control { public bool IsActive; }
    public sealed class Pivot : Control { public int SelectedIndex; }
    public sealed class AppBarButton : Control { }
    public sealed class SelectionChangedEventArgs : EventArgs { }
    public sealed class ItemClickEventArgs : EventArgs { public object ClickedItem; }
}
namespace Windows.UI.Xaml.Media { public sealed class Brush { } }
namespace Windows.UI.Xaml.Navigation { public sealed class NavigationEventArgs : EventArgs { public object Parameter; } }
namespace Windows.System { public enum VirtualKey { Enter } }
namespace Windows.UI.Xaml.Input { public sealed class KeyRoutedEventArgs : EventArgs { public Windows.System.VirtualKey Key; public bool Handled; } }
namespace Windows.Phone.UI.Input
{
    public sealed class BackPressedEventArgs : EventArgs { public bool Handled; }
    public static class HardwareButtons { public static event EventHandler<BackPressedEventArgs> BackPressed { add { } remove { } } }
}
namespace Mixdrome.Phone
{
    public sealed class App
    {
        public static readonly App Current = new App();
        public readonly Dictionary<string, object> Resources = new Dictionary<string, object>
        {
            { "MixRadioYellowBrush", new Windows.UI.Xaml.Media.Brush() },
            { "MixRadioTealBrush", new Windows.UI.Xaml.Media.Brush() },
            { "MixRadioAccentBrush", new Windows.UI.Xaml.Media.Brush() }
        };
    }
}
namespace Mixdrome.Phone.Common
{
    public sealed class PhoneTitleBar : Windows.UI.Xaml.Controls.Control { public string Title; }
}
namespace Mixdrome.Phone.Views
{
    public sealed partial class SearchPage
    {
        public readonly Mixdrome.Phone.Common.PhoneTitleBar TestSectionBar = new Mixdrome.Phone.Common.PhoneTitleBar();
        public readonly TextBlock TestEmpty = new TextBlock();
        public readonly ProgressRing TestProgress = new ProgressRing();
        public readonly Pivot TestPivot = new Pivot();
        public readonly AppBarButton TestMore = new AppBarButton();
        private void InitializeComponent()
        {
            RegisterName("SectionBar", TestSectionBar);
            RegisterName("SearchTextBox", new TextBox()); RegisterName("EmptyStateText", TestEmpty);
            RegisterName("LoadingIndicator", TestProgress); RegisterName("ResultsPivot", TestPivot); RegisterName("MoreButton", TestMore);
        }
    }
    public sealed class ArtistAlbumsPage { }
    public sealed class AlbumPage { }
    public sealed class PlaybackPage { }
    public sealed class PlaybackNavigationParameter { public List<SongDisplayItem> Playlist; public int StartIndex; }
    public sealed class ArtistDisplayItem { public string Id, Name, Subtitle, Initial; }
    public sealed class SongDisplayItem { public string Id, Title, Artist, Album, CoverArt, DurationString; public int Track, Duration; }
}
