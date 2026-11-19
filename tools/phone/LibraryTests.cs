using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

// Executes the production library page against delayed HTTP peers. Managed controls
// observe navigation/state only; they do not certify native rendering or touch input.
public static class PhoneLibraryTests
{
    private static int assertions;
    private static void Check(bool condition, string message) { ++assertions; if (!condition) throw new Exception(message); }
    private static object Call(LibraryPage page, string method, params object[] arguments)
    { return typeof(LibraryPage).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, arguments); }
    private static Task Open(LibraryPage page, PhoneLibraryItem item) { return (Task)Call(page, "OpenItem", item); }
    private static Task Play(LibraryPage page, PhoneLibraryItem item) { return (Task)Call(page, "PlayItem", item); }
    private static Task Shuffle(LibraryPage page) { return (Task)Call(page, "ShuffleAll"); }
    private static string Response(string body, bool success = true)
    { return "<subsonic-response xmlns='http://subsonic.org/restapi' status='" + (success ? "ok" : "failed") + "' version='1.16.1'>" + body + "</subsonic-response>"; }
    private static async Task WaitFor(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition()) { if (DateTime.UtcNow >= deadline) throw new Exception(message); await Task.Delay(10); }
    }
    private static async Task<LibraryPage> Page(string route = "playlists")
    {
        var page = new LibraryPage();
        Call(page, "OnNavigatedTo", new NavigationEventArgs { Parameter = route });
        await WaitFor(() => route == "playlists" ? page.Playlists.Count == 4 : route == "albums" ? page.Albums.Count == 3 : route == "genres" ? page.Genres.Count == 1 : route == "songs" ? page.Songs.Count == 3 : page.Artists.Count == 1, "Library initialization did not complete.");
        return page;
    }
    private static PhoneLibraryItem Item(LibraryPage page, string id) { return page.Playlists.First(item => item.Id == id); }
    private static void Leave(LibraryPage page) { Call(page, "OnNavigatedFrom", new NavigationEventArgs()); }
    private static string StartedTrack(LibraryPage page)
    { return ((PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter).Playlist[0].Id; }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production phone library play/detail, complete queues, delayed-selection, tab, refresh, departure and error assertions. Native rendering remains unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        ApplicationData.Current.LocalSettings.Values.Clear(); PasswordVault.Credentials.Clear();
        var songCount = 3;
        var songDelay = 0;
        var songFailureOffset = -1;
        using (var server = new ProtocolServer(request =>
        {
            var path = request.Uri.AbsolutePath;
            if (path.EndsWith("getPlaylists.view")) return new ProtocolResponse { Body = Response("<playlists><playlist id='A' name='A' songCount='2' /><playlist id='B' name='B' songCount='2' /><playlist id='F' name='Failure' songCount='2' /><playlist id='E' name='Empty' songCount='0' /></playlists>") };
            if (path.EndsWith("getArtists.view")) return new ProtocolResponse { Body = Response("<artists><index name='A'><artist id='artist' name='Artist' albumCount='1' /></index></artists>") };
            if (path.EndsWith("getAlbumList2.view")) return new ProtocolResponse { Body = Response("<albumList2><album id='album' name='Album' artist='Album Artist' /><album id='empty' name='Empty' /><album id='failure' name='Failure' /></albumList2>") };
            if (path.EndsWith("getGenres.view")) return new ProtocolResponse { Body = Response("<genres><genre songCount='125' albumCount='2'>Genre &amp; More</genre></genres>") };
            if (path.EndsWith("getArtist.view")) return new ProtocolResponse { Body = Response("<artist id='artist' name='Artist'><album id='album' name='Album' /><album id='album2' name='Second album' /></artist>") };
            if (path.EndsWith("getAlbum.view"))
            {
                var id = request.Parameter("id");
                if (id == "failure") return new ProtocolResponse { DelayMilliseconds = 150, Body = Response("<error code='40' message='Denied' />", false) };
                if (id == "empty") return new ProtocolResponse { Body = Response("<album id='empty' name='Empty' />") };
                var entries = id == "album2" ? "<song id='second' title='Last' duration='80' />" : "<song id='dup' title='First' duration='90' /><song id='dup' title='Duplicate' duration='95' />";
                return new ProtocolResponse { DelayMilliseconds = id == "album" ? 200 : 0, Body = Response("<album id='" + id + "' name='" + id + "' artist='Album Artist' coverArt='album-art'>" + entries + "</album>") };
            }
            if (path.EndsWith("getSongsByGenre.view"))
            {
                var offset = int.Parse(request.Parameter("offset"));
                var count = Math.Min(100, 125 - offset);
                return new ProtocolResponse { Body = Response("<songsByGenre>" + string.Concat(Enumerable.Range(offset, count).Select(index => "<song id='genre-" + index + "' title='Genre track " + index + "' artist='Genre Artist' album='Genre Album' duration='91' />")) + "</songsByGenre>") };
            }
            if (path.EndsWith("search3.view"))
            {
                var offset = int.Parse(request.Parameter("songOffset") ?? "0");
                if (offset == songFailureOffset) return new ProtocolResponse { DelayMilliseconds = songDelay, Body = Response("<error code='40' message='Denied' />", false) };
                var count = Math.Min(100, Math.Max(0, songCount - offset));
                return new ProtocolResponse { DelayMilliseconds = songDelay, Body = Response("<searchResult3>" + string.Concat(Enumerable.Range(offset, count).Select(index => "<song id='song-" + index + "' title='Song " + index + "' artist='Song Artist' album='Song Album' duration='90' />")) + "</searchResult3>") };
            }
            if (path.EndsWith("getPlaylist.view"))
            {
                var id = request.Parameter("id");
                if (id == "F") return new ProtocolResponse { DelayMilliseconds = 200, Body = Response("<error code='40' message='Denied' />", false) };
                if (id == "E") return new ProtocolResponse { Body = Response("<playlist id='E' name='Empty' />") };
                return new ProtocolResponse { DelayMilliseconds = id == "A" ? 300 : 650,
                    Body = Response("<playlist id='" + id + "' name='" + id + "'><entry id='" + id + "-1' title='First' artist='Artist' album='Album' duration='90' /><entry id='" + id + "-2' title='Second' duration='95' /></playlist>") };
            }
            return new ProtocolResponse { Body = Response("") };
        }))
        {
            PhoneSettings.Save(new SubsonicServiceConfiguration { PrimaryUrl = server.Url, BaseUrl = server.Url, Username = "fixture", Password = "secret" });
            var page = await Page();
            var a = Open(page, Item(page, "A"));
            await WaitFor(() => server.Requests.Any(request => request.Parameter("id") == "A"), "First playlist request was not issued.");
            var b = Open(page, Item(page, "B"));
            await a;
            Check(page.Frame.Navigations.Count == 0, "A faster prior playlist response cannot start after a newer selection.");
            await b;
            Check(page.Frame.Navigations.Count == 1 && StartedTrack(page) == "B-1", "Only the latest playlist selection starts playback.");
            var parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.StartIndex == 0 && parameter.Playlist.Count == 2 && parameter.Playlist[0].Album == "Album", "Successful playlist navigation retains its full queue and metadata.");
            Leave(page);

            page = await Page();
            var failing = Open(page, Item(page, "F"));
            b = Open(page, Item(page, "B"));
            await failing;
            Check(string.IsNullOrEmpty(page.TestStatus.Text), "An error from an older selection cannot replace current page status.");
            await b;
            Check(page.Frame.Navigations.Count == 1 && StartedTrack(page) == "B-1", "A stale failed playlist cannot suppress the newer successful selection.");
            Leave(page);

            page = await Page();
            a = Open(page, Item(page, "A"));
            page.TestPivot.SelectedIndex = 0;
            Call(page, "OnTabChanged", null, new SelectionChangedEventArgs());
            await a;
            Check(page.Frame.Navigations.Count == 0, "Changing tabs invalidates pending playlist navigation.");
            Check(!page.TestStatus.Text.StartsWith("LoadFailed"), "A discarded playlist response does not affect the new tab status.");
            var beforeHidden = server.Requests.Count(request => request.Uri.AbsolutePath.EndsWith("getPlaylist.view"));
            await Open(page, Item(page, "B"));
            Check(server.Requests.Count(request => request.Uri.AbsolutePath.EndsWith("getPlaylist.view")) == beforeHidden,
                "An old playlist item click from a hidden tab cannot issue a playback request.");
            Leave(page);

            page = await Page();
            a = Open(page, Item(page, "A"));
            await (Task)Call(page, "Initialize");
            await a;
            Check(page.Frame.Navigations.Count == 0 && page.Playlists.Count == 4, "Refreshing discards pending playback without losing the refreshed library.");
            Leave(page);

            page = await Page();
            a = Open(page, Item(page, "A"));
            Leave(page);
            await a;
            Check(page.Frame.Navigations.Count == 0, "Leaving discards a late playlist completion.");

            page = await Page();
            await Open(page, Item(page, "F"));
            Check(page.Frame.Navigations.Count == 0 && page.TestStatus.Text.StartsWith("LoadFailed"), "The current failed selection reports a controlled error and does not navigate.");
            await Open(page, Item(page, "E"));
            Check(page.Frame.Navigations.Count == 0 && page.TestStatus.Text.StartsWith("PlaylistEmpty"), "The current empty playlist reports empty state without starting playback.");
            var handled = new BackPressedEventArgs { Handled = true };
            Call(page, "OnBackPressed", null, handled);
            Check(page.Frame.BackCount == 0, "An already handled Back event does not navigate twice.");
            Leave(page);

            page = await Page("albums");
            var albumItem = page.Albums[0];
            var albumRequests = server.Requests.Count(request => request.Uri.AbsolutePath.EndsWith("getAlbum.view"));
            await Open(page, albumItem);
            Check(page.Frame.Navigations.Single().Type == typeof(AlbumPage) && (string)page.Frame.Navigations.Single().Parameter == "album", "The album title opens its detail page.");
            Check(server.Requests.Count(request => request.Uri.AbsolutePath.EndsWith("getAlbum.view")) == albumRequests, "Opening an album title does not fetch or start a queue.");
            Leave(page);

            page = await Page("albums");
            await Play(page, page.Albums[0]);
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(page.Frame.Navigations.Single().Type == typeof(PlaybackPage) && parameter.StartIndex == 0, "The album artwork starts its queue at the beginning.");
            Check(parameter.Playlist.Select(song => song.Id).SequenceEqual(new[] { "dup", "dup" }) && parameter.Playlist[1].Title == "Duplicate", "Album playback preserves duplicate IDs and server song order.");
            Check(parameter.Playlist.All(song => song.Artist == "Album Artist" && song.Album == "album" && song.CoverArt == "album-art") && parameter.Playlist[1].Duration == 95, "Album-level metadata fills missing song fields without losing song duration.");
            Leave(page);

            page = await Page("artists");
            await Open(page, page.Artists[0]);
            Check(page.Frame.Navigations.Single().Type == typeof(ArtistAlbumsPage), "An artist title opens its albums.");
            Leave(page);
            page = await Page("artists");
            await Play(page, page.Artists[0]);
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.Playlist.Select(song => song.Id).SequenceEqual(new[] { "dup", "dup", "second" }), "Artist play resolves all albums in server order before replacing the queue.");
            Check(parameter.Playlist[2].Track == 3 && parameter.Playlist[2].Album == "album2", "Artist play retains album metadata and full context numbering.");
            Leave(page);

            page = await Page("genres");
            await Open(page, page.Genres[0]);
            Check(page.Frame.Navigations.Single().Type == typeof(GenreSongsPage) && (string)page.Frame.Navigations.Single().Parameter == "Genre & More", "A genre title opens its exact server-provided name.");
            Leave(page);
            page = await Page("genres");
            await Play(page, page.Genres[0]);
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.Playlist.Count == 125 && parameter.Playlist[100].Id == "genre-100" && parameter.Playlist[124].Track == 125, "Genre play resolves every page rather than truncating at 100 songs.");
            Check(server.Requests.Where(request => request.Uri.AbsolutePath.EndsWith("getSongsByGenre.view")).All(request => request.Parameter("genre") == "Genre & More"), "Genre playback passes the full encoded server name.");
            Leave(page);

            page = await Page("songs");
            await Open(page, page.Songs[1]);
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.StartIndex == 1 && parameter.Playlist.Select(song => song.Id).SequenceEqual(new[] { "song-0", "song-1", "song-2" }), "A song row plays the selected position in unchanged library order.");
            Leave(page);

            page = await Page("songs");
            songCount = 125;
            await Shuffle(page);
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.Shuffle == true && parameter.StartIndex >= 0 && parameter.StartIndex < 125, "Shuffle all requests background shuffle and selects a valid random first song.");
            Check(parameter.Playlist.Count == 125 && parameter.Playlist[100].Id == "song-100" && parameter.Playlist[124].Track == 125,
                "Shuffle all resolves the complete library beyond the currently browsed page, preserving server order and numbering.");
            Check(parameter.Playlist[124].Artist == "Song Artist" && parameter.Playlist[124].Album == "Song Album" && parameter.Playlist[124].Duration == 90,
                "Shuffle all preserves real song metadata in its playback queue.");
            Check(page.Songs.Count == 3, "Building a complete shuffle queue does not replace the visible paged collection.");
            Leave(page);

            songCount = 3; page = await Page("songs"); songCount = 125; songFailureOffset = 100;
            await Shuffle(page);
            Check(page.Frame.Navigations.Count == 0 && page.TestStatus.Text.StartsWith("LoadFailed"),
                "A later shuffle page failure leaves playback unchanged instead of starting a partial queue.");
            Leave(page);

            songCount = 3; songFailureOffset = -1; page = await Page("songs"); songDelay = 150;
            var pendingShuffle = Shuffle(page);
            await Open(page, page.Songs[1]);
            await pendingShuffle;
            parameter = (PlaybackNavigationParameter)page.Frame.Navigations.Single().Parameter;
            Check(parameter.StartIndex == 1 && parameter.Shuffle == null, "A newer song selection supersedes a pending shuffle request and preserves normal playback mode.");
            Leave(page);

            songDelay = 0; page = await Page("songs"); songDelay = 150;
            pendingShuffle = Shuffle(page);
            page.TestPivot.SelectedIndex = 0; Call(page, "OnTabChanged", null, new SelectionChangedEventArgs());
            await pendingShuffle;
            Check(page.Frame.Navigations.Count == 0, "A tab change invalidates an outstanding shuffle queue.");
            Leave(page);

            songDelay = 0; page = await Page("songs"); songDelay = 150;
            pendingShuffle = Shuffle(page); Leave(page); await pendingShuffle;
            Check(page.Frame.Navigations.Count == 0, "Leaving the library invalidates an outstanding shuffle queue.");
            songDelay = 0;

            page = await Page("albums");
            var pendingAlbum = Play(page, page.Albums[0]);
            await Open(page, page.Albums[1]);
            await pendingAlbum;
            Check(page.Frame.Navigations.Count == 1 && page.Frame.Navigations[0].Type == typeof(AlbumPage) && (string)page.Frame.Navigations[0].Parameter == "empty", "A newer detail navigation supersedes an outstanding album-play selection.");
            Leave(page);
            page = await Page("albums");
            var pendingFailure = Play(page, page.Albums[2]);
            pendingAlbum = Play(page, page.Albums[0]);
            await pendingFailure;
            Check(string.IsNullOrEmpty(page.TestStatus.Text), "An old direct-play failure cannot overwrite a newer album selection.");
            await pendingAlbum;
            Check(page.Frame.Navigations.Count == 1 && page.Frame.Navigations[0].Type == typeof(PlaybackPage), "A newer album play succeeds after a stale failure.");
            Leave(page);
            page = await Page("albums");
            await Play(page, page.Albums[1]);
            Check(page.Frame.Navigations.Count == 0 && page.TestStatus.Text.StartsWith("NoSongs"), "An empty direct-play album keeps the existing player queue unchanged and reports emptiness.");
            await Play(page, page.Albums[2]);
            Check(page.Frame.Navigations.Count == 0 && page.TestStatus.Text.StartsWith("LoadFailed"), "A direct-play request failure reports an error without navigating or replacing playback.");
            pendingAlbum = Play(page, page.Albums[0]);
            Leave(page);
            await pendingAlbum;
            Check(page.Frame.Navigations.Count == 0, "Leaving the library rejects a late direct-play queue.");
        }
    }
}

namespace Windows.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public class RoutedEventArgs : EventArgs { }
}
namespace Windows.UI.Xaml.Controls
{
    public class Control { public Visibility Visibility = Visibility.Visible; public bool IsEnabled = true; }
    public class Page : Control
    {
        public object DataContext;
        public readonly Frame Frame = new Frame();
        protected virtual void OnNavigatedTo(NavigationEventArgs e) { }
        protected virtual void OnNavigatedFrom(NavigationEventArgs e) { }
    }
    public sealed class NavigationRecord { public Type Type; public object Parameter; }
    public sealed class Frame
    {
        public bool CanGoBack = true; public int BackCount;
        public readonly List<NavigationRecord> Navigations = new List<NavigationRecord>();
        public bool Navigate(Type type, object parameter = null) { Navigations.Add(new NavigationRecord { Type = type, Parameter = parameter }); return true; }
        public void GoBack() { ++BackCount; }
    }
    public sealed class TextBlock : Control { public string Text = string.Empty; }
    public sealed class ProgressRing : Control { public bool IsActive; }
    public sealed class PivotItem : Control { public string Tag; }
    public sealed class Pivot : Control
    {
        public int SelectedIndex;
        public readonly List<PivotItem> Items = new List<PivotItem>();
        public object SelectedItem { get { return Items[SelectedIndex]; } }
    }
    public sealed class AppBarButton : Control { }
    public sealed class Button : Control { public object Tag; }
    public sealed class SelectionChangedEventArgs : EventArgs { }
    public sealed class ItemClickEventArgs : EventArgs { public object ClickedItem; }
}
namespace Windows.UI.Xaml.Navigation { public sealed class NavigationEventArgs : EventArgs { public object Parameter; } }
namespace Windows.Phone.UI.Input
{
    public sealed class BackPressedEventArgs : EventArgs { public bool Handled; }
    public static class HardwareButtons { public static event EventHandler<BackPressedEventArgs> BackPressed { add { } remove { } } }
}
namespace Mixdrome.Phone.Common
{
    public sealed class PhoneTitleBar : Windows.UI.Xaml.Controls.Control { public string Title; }
}
namespace Mixdrome.Phone.Views
{
    public sealed partial class LibraryPage
    {
        private readonly Mixdrome.Phone.Common.PhoneTitleBar SectionBar = new Mixdrome.Phone.Common.PhoneTitleBar();
        private readonly Pivot LibraryPivot = new Pivot();
        private readonly TextBlock StatusText = new TextBlock();
        private readonly ProgressRing LoadingIndicator = new ProgressRing();
        private readonly AppBarButton MoreButton = new AppBarButton();
        private readonly Button ShuffleSongsButton = new Button();
        public Pivot TestPivot { get { return LibraryPivot; } }
        public TextBlock TestStatus { get { return StatusText; } }
        private void InitializeComponent() { foreach (var tab in new[] { "artists", "albums", "songs", "genres", "playlists" }) LibraryPivot.Items.Add(new PivotItem { Tag = tab }); }
    }
    public sealed class ArtistAlbumsPage { }
    public sealed class AlbumPage { }
    public sealed class GenreSongsPage { }
    public sealed class PlaybackPage { }
    public sealed class SearchPage { }
    public sealed class PlaybackNavigationParameter { public List<SongDisplayItem> Playlist; public int StartIndex; public bool? Shuffle; }
    public sealed class ArtistDisplayItem { public string Id, Name; }
    public sealed class SongDisplayItem { public string Id, Title, Artist, Album, CoverArt; public int Track, Duration; }
}
