namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Navidrome;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.Phone.UI.Input;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class LibraryPage : Page
    {
        private const int PageSize = 100;
        private NavidromeApi api;
        private bool active;
        private int generation;
        private int selectionGeneration;
        private int pendingPlaySelection;
        private string pendingPlayTab;
        private bool recent;
        private readonly Random random = new Random();
        private readonly HashSet<string> loaded = new HashSet<string>();
        private readonly HashSet<string> loading = new HashSet<string>();
        private readonly HashSet<string> more = new HashSet<string>();
        public ObservableCollection<PhoneLibraryItem> Artists { get; private set; } = new ObservableCollection<PhoneLibraryItem>();
        public ObservableCollection<PhoneLibraryItem> Albums { get; private set; } = new ObservableCollection<PhoneLibraryItem>();
        public ObservableCollection<PhoneLibraryItem> Songs { get; private set; } = new ObservableCollection<PhoneLibraryItem>();
        public ObservableCollection<PhoneLibraryItem> Genres { get; private set; } = new ObservableCollection<PhoneLibraryItem>();
        public ObservableCollection<PhoneLibraryItem> Playlists { get; private set; } = new ObservableCollection<PhoneLibraryItem>();

        public LibraryPage()
        {
            InitializeComponent(); DataContext = this;
            SectionBar.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiMyMusic"));
        }
        private string Tab { get { var item = LibraryPivot.SelectedItem as PivotItem; return item == null ? "artists" : item.Tag as string; } }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e); active = true; HardwareButtons.BackPressed += OnBackPressed;
            var route = e.Parameter as string;
            recent = route == "recent";
            LibraryPivot.SelectedIndex = route == "albums" || recent ? 1 : route == "songs" ? 2 : route == "genres" ? 3 : route == "playlists" ? 4 : 0;
            await Initialize();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e) { active = false; generation++; selectionGeneration++; HardwareButtons.BackPressed -= OnBackPressed; base.OnNavigatedFrom(e); }
        private bool Current(int version) { return active && generation == version; }
        private bool CurrentSelection(int version, int selection, string tab)
        { return Current(version) && selectionGeneration == selection && Tab == tab; }
        private async Task Initialize()
        {
            var version = ++generation; selectionGeneration++; loaded.Clear(); loading.Clear(); more.Clear(); api = null;
            Artists.Clear(); Albums.Clear(); Songs.Clear(); Genres.Clear(); Playlists.Clear(); StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                var service = await PhoneSubsonicServiceFactory.CreateAsync();
                if (!Current(version)) return;
                if (string.IsNullOrEmpty(service.Configuration.BaseUrl)) { StatusText.Text = PhoneText.Get("ConfigureServer"); return; }
                api = new NavidromeApi(service.Configuration); await Load(Tab, false);
            }
            catch (Exception ex) { if (Current(version)) StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); }
        }
        private ObservableCollection<PhoneLibraryItem> Items(string tab)
        {
            switch (tab) { case "albums": return Albums; case "songs": return Songs; case "genres": return Genres; case "playlists": return Playlists; default: return Artists; }
        }
        private async Task Load(string tab, bool append)
        {
            if (api == null || loading.Contains(tab) || (!append && loaded.Contains(tab))) return;
            var version = generation; var currentApi = api; var items = Items(tab); var offset = append ? items.Count : 0;
            loading.Add(tab); if (Tab == tab) StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                var page = new List<PhoneLibraryItem>();
                switch (tab)
                {
                    case "artists":
                        foreach (var artist in await currentApi.GetArtists(null).ReadAsync())
                            page.Add(new PhoneLibraryItem { Route = "artist", Id = artist.Id, Name = artist.Name, Subtitle = PhoneText.Format("AlbumCount", artist.AlbumCount), CoverArt = Artwork(currentApi, artist.CoverArt) });
                        break;
                    case "albums":
                        foreach (var album in await currentApi.GetAlbums(new AlbumQuery { Type = recent ? AlbumListType.Recent : AlbumListType.AlphabeticalByName, Offset = offset, Size = PageSize }).ReadAsync())
                            page.Add(new PhoneLibraryItem { Route = "album", Id = album.Id, Name = album.Name, Subtitle = album.Artist, CoverArt = Artwork(currentApi, album.CoverArt) });
                        break;
                    case "songs":
                        var results = await currentApi.Search(new SearchQuery { Query = string.Empty, ArtistCount = 0, AlbumCount = 0, SongCount = PageSize, SongOffset = offset }).ReadAsync();
                        foreach (var song in results.Songs) page.Add(new PhoneLibraryItem { Route = "song", Id = song.Id, Name = song.Title ?? song.Name, Subtitle = song.Artist, CoverArt = Artwork(currentApi, song.CoverArt), Song = ToSong(song, offset + page.Count + 1) });
                        break;
                    case "genres":
                        foreach (var genre in await currentApi.GetGenres(null).ReadAsync())
                            page.Add(new PhoneLibraryItem { Route = "genre", Name = genre.Name, Subtitle = PhoneText.Format("SongCount", genre.SongCount), CoverArt = "ms-appx:///Assets/MixRadio/MyMusicTileIcon.png" });
                        break;
                    case "playlists":
                        foreach (var playlist in await currentApi.GetPlaylists().ReadAsync())
                            page.Add(new PhoneLibraryItem { Route = "playlist", Id = playlist.Id, Name = playlist.Name, Subtitle = PhoneText.Format("SongCount", playlist.SongCount), CoverArt = Artwork(currentApi, playlist.CoverArt) });
                        break;
                }
                if (!Current(version)) return;
                if (!append) items.Clear();
                foreach (var item in page) items.Add(item);
                loaded.Add(tab);
                if ((tab == "albums" || tab == "songs") && page.Count == PageSize) more.Add(tab); else more.Remove(tab);
                if (Tab == tab) StatusText.Text = items.Count == 0 ? PhoneText.Get("LibraryEmpty") : string.Empty;
            }
            catch (Exception ex) { if (Current(version) && Tab == tab) StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); }
            finally { if (Current(version)) { loading.Remove(tab); UpdateBusy(); } }
        }
        private static string Artwork(NavidromeApi source, string id) { return string.IsNullOrEmpty(id) ? "ms-appx:///Assets/MixRadio/DefaultArtwork300.png" : source.CoverArtUrl(id); }
        private static SongDisplayItem ToSong(Song song, int index) { return new SongDisplayItem { Id = song.Id, Track = index, Title = song.Title ?? song.Name, Artist = song.Artist, Album = song.Album, CoverArt = song.CoverArt, Duration = song.Duration }; }
        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            await OpenItem(e.ClickedItem as PhoneLibraryItem);
        }
        private async Task OpenItem(PhoneLibraryItem item)
        {
            ++selectionGeneration;
            UpdateBusy();
            if (item == null || api == null || !active || !Items(Tab).Contains(item)) return;
            switch (item.Route)
            {
                case "artist": Frame.Navigate(typeof(ArtistAlbumsPage), new ArtistDisplayItem { Id = item.Id, Name = item.Name }); break;
                case "album": Frame.Navigate(typeof(AlbumPage), item.Id); break;
                case "genre": Frame.Navigate(typeof(GenreSongsPage), item.Name); break;
                case "song": Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = Songs.Select(row => row.Song).ToList(), StartIndex = Songs.IndexOf(item) }); break;
                case "playlist": await PlayItem(item); break;
            }
        }
        private async void OnOpenItemClick(object sender, RoutedEventArgs e) { await OpenItem(((Button)sender).Tag as PhoneLibraryItem); }
        private async void OnPlayItemClick(object sender, RoutedEventArgs e) { await PlayItem(((Button)sender).Tag as PhoneLibraryItem); }
        private async Task PlayItem(PhoneLibraryItem item)
        {
            var selection = ++selectionGeneration;
            if (item == null || api == null || !active || !Items(Tab).Contains(item)) { UpdateBusy(); return; }
            var version = generation;
            var tab = Tab;
            var source = api;
            pendingPlaySelection = selection; pendingPlayTab = tab;
            StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                var songs = new List<Song>();
                switch (item.Route)
                {
                    case "album":
                        var album = await source.GetAlbum(item.Id).ReadAsync();
                        if (!CurrentSelection(version, selection, tab)) return;
                        AddAlbumSongs(songs, album);
                        break;
                    case "artist":
                        var artist = await source.GetArtist(item.Id).ReadAsync();
                        if (!CurrentSelection(version, selection, tab)) return;
                        if (artist != null && artist.Albums != null)
                        {
                            foreach (var artistAlbum in artist.Albums)
                            {
                                var detail = await source.GetAlbum(artistAlbum.Id).ReadAsync();
                                if (!CurrentSelection(version, selection, tab)) return;
                                AddAlbumSongs(songs, detail);
                            }
                        }
                        break;
                    case "genre":
                        var offset = 0;
                        while (true)
                        {
                            var page = await source.GetGenreSongs(item.Name, null, offset, PageSize).ReadAsync();
                            if (!CurrentSelection(version, selection, tab)) return;
                            songs.AddRange(page);
                            if (page.Count < PageSize) break;
                            offset += page.Count;
                        }
                        break;
                    case "playlist":
                        var playlist = await source.GetPlaylist(item.Id).ReadAsync();
                        if (!CurrentSelection(version, selection, tab)) return;
                        if (playlist != null && playlist.Entries != null) songs.AddRange(playlist.Entries);
                        break;
                    default: return;
                }
                if (!CurrentSelection(version, selection, tab)) return;
                if (songs.Count == 0) { StatusText.Text = item.Route == "playlist" ? PhoneText.Get("PlaylistEmpty") : PhoneText.Get("NoSongs"); return; }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = songs.Select((song, index) => ToSong(song, index + 1)).ToList(), StartIndex = 0 });
            }
            catch (Exception ex) { if (CurrentSelection(version, selection, tab)) StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); }
            finally { if (pendingPlaySelection == selection) { pendingPlaySelection = 0; pendingPlayTab = null; } UpdateBusy(); }
        }
        private static void AddAlbumSongs(ICollection<Song> target, Album album)
        {
            if (album == null || album.Songs == null) return;
            foreach (var song in album.Songs)
            {
                song.Artist = song.Artist ?? album.Artist;
                song.Album = song.Album ?? album.Name;
                song.CoverArt = song.CoverArt ?? album.CoverArt;
                target.Add(song);
            }
        }
        private async void OnShuffleAllClick(object sender, RoutedEventArgs e) { await ShuffleAll(); }
        private async Task ShuffleAll()
        {
            if (!active || api == null || Tab != "songs" || Songs.Count == 0) return;
            var version = generation;
            var selection = ++selectionGeneration;
            var source = api;
            pendingPlaySelection = selection; pendingPlayTab = "songs";
            StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                // Resolve the complete library, including pages not yet browsed. Keep
                // its server ordering and let the background queue own shuffle order.
                var songs = new List<SongDisplayItem>();
                while (true)
                {
                    var page = await source.Search(new SearchQuery { Query = string.Empty, ArtistCount = 0, AlbumCount = 0, SongCount = PageSize, SongOffset = songs.Count }).ReadAsync();
                    if (!CurrentSelection(version, selection, "songs")) return;
                    foreach (var song in page.Songs) songs.Add(ToSong(song, songs.Count + 1));
                    if (page.Songs.Count < PageSize) break;
                }
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("NoSongs"); return; }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = songs, StartIndex = random.Next(songs.Count), Shuffle = true });
            }
            catch (Exception ex) { if (CurrentSelection(version, selection, "songs")) StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); }
            finally { if (pendingPlaySelection == selection) { pendingPlaySelection = 0; pendingPlayTab = null; } UpdateBusy(); }
        }
        private void UpdateBusy()
        {
            if (LoadingIndicator == null || MoreButton == null) return;
            LoadingIndicator.IsActive = loading.Contains(Tab) || pendingPlaySelection == selectionGeneration && pendingPlayTab == Tab;
            LoadingIndicator.Visibility = LoadingIndicator.IsActive ? Visibility.Visible : Visibility.Collapsed;
            MoreButton.Visibility = more.Contains(Tab) ? Visibility.Visible : Visibility.Collapsed; MoreButton.IsEnabled = !loading.Contains(Tab);
            if (ShuffleSongsButton != null)
            {
                ShuffleSongsButton.Visibility = Songs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ShuffleSongsButton.IsEnabled = !loading.Contains("songs") && !(pendingPlaySelection == selectionGeneration && pendingPlayTab == "songs");
            }
        }
        private async void OnTabChanged(object sender, SelectionChangedEventArgs e) { selectionGeneration++; if (!active) return; UpdateBusy(); StatusText.Text = loaded.Contains(Tab) && Items(Tab).Count == 0 ? PhoneText.Get("LibraryEmpty") : string.Empty; await Load(Tab, false); }
        private async void OnMoreClick(object sender, RoutedEventArgs e) { await Load(Tab, true); }
        private async void OnRefreshClick(object sender, RoutedEventArgs e) { await Initialize(); }
        private void OnSearchClick(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(SearchPage)); }
        private void OnBackPressed(object sender, BackPressedEventArgs e) { if (e.Handled || !Frame.CanGoBack) return; e.Handled = true; Frame.GoBack(); }
    }
    public sealed class PhoneLibraryItem
    {
        public string Route { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public string CoverArt { get; set; }
        public SongDisplayItem Song { get; set; }
        public string PlayLabel { get { return PhoneText.Format("PlayLibraryItem", Name); } }
        public string OpenLabel { get { return PhoneText.Format("OpenLibraryItem", Name); } }
    }
}
