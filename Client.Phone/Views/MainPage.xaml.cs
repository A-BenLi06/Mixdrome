namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Navidrome;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services.Navidrome;
    using Client.Common.Services;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.UI.Xaml.Navigation;
    public sealed partial class MainPage : Page
    {
        private int generation;
        private bool active;
        private bool startingPlayback;
        private PhonePlaybackObserver observer;
        private string homeAccount;
        public MainPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Enabled; SizeChanged += OnSizeChanged; }
        protected override async void OnNavigatedTo(NavigationEventArgs args)
        {
            base.OnNavigatedTo(args); active = true; ResizeSections();
            var navigation = ++generation;
            if (!IsCurrent(navigation)) return;
            var nextObserver = new PhonePlaybackObserver(Dispatcher);
            observer = nextObserver; nextObserver.Changed += OnPlaybackChanged;
            await nextObserver.StartAsync();
            if (!IsCurrent(navigation) || !ReferenceEquals(observer, nextObserver)) { nextObserver.Dispose(); return; }
            await LoadHome();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs args)
        {
            active = false; generation++;
            if (observer != null) { observer.Dispose(); observer = null; }
            base.OnNavigatedFrom(args);
        }
        private void OnPlaybackChanged(object sender, EventArgs args)
        {
            if (active && observer != null && ReferenceEquals(sender, observer)) HomeTiles.SetCurrent(observer.Track, observer.Position);
        }
        private bool IsCurrent(int version) { return active && generation == version; }
        private void UpdateStatusBanner()
        {
            StatusBanner.Visibility = !string.IsNullOrEmpty(StatusText.Text) || LoadingIndicator.Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
        }
        private async Task LoadHome()
        {
            var version = ++generation;
            LoadingIndicator.IsActive = true; LoadingIndicator.Visibility = Visibility.Visible; StatusText.Text = string.Empty; UpdateStatusBanner();
            try
            {
                var account = AccountIdentity.For(PhoneSettings.Load());
                if (homeAccount != account)
                {
                    homeAccount = account;
                    ArtistBackdrop.Source = null;
                    HomeTiles.SetHistory(new List<Album>());
                    HomeTiles.SetMixes(new List<Playlist>());
                }
                var service = await PhoneSubsonicServiceFactory.CreateAsync();
                if (!IsCurrent(version)) return;
                if (string.IsNullOrEmpty(service.Configuration.BaseUrl)) { StatusText.Text = PhoneText.Get("ConfigureServer"); UpdateStatusBanner(); return; }
                var api = new NavidromeApi(service.Configuration);
                var albums = await api.GetAlbums(new AlbumQuery { Type = AlbumListType.Recent, Size = 3 }).ReadAsync();
                if (!IsCurrent(version)) return;
                foreach (var album in albums) album.CoverArt = service.GetCoverArtForId(album.CoverArt);
                HomeTiles.SetHistory(albums);
                var mixes = (await api.GetPlaylists().ReadAsync()).OrderByDescending(p => p.Changed).ThenByDescending(p => p.Created).Take(2).ToList();
                if (!IsCurrent(version)) return;
                foreach (var mix in mixes) mix.CoverArt = service.GetCoverArtForId(mix.CoverArt);
                HomeTiles.SetMixes(mixes);
                // Artist photos are supplied by the account's server, never
                // inferred from album artwork or a packaged stock portrait.
                var starred = await api.GetStarred(null).ReadAsync();
                var artistService = new WP8SubsonicService();
                artistService.Configure((Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration)service.Configuration);
                var artistIds = starred.Artists.Select(a => a.Id).Concat(albums.Select(a => a.ArtistId)).Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(5);
                foreach (var id in artistIds)
                {
                    var info = await artistService.GetArtistInfoAsync(id);
                    if (!IsCurrent(version)) return;
                    Uri photo;
                    if (Uri.TryCreate(info.ImageUrl, UriKind.Absolute, out photo) && (photo.Scheme == "http" || photo.Scheme == "https"))
                    { ArtistBackdrop.Source = new BitmapImage(photo); break; }
                }
            }
            catch (Exception ex) { if (IsCurrent(version)) { StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); UpdateStatusBanner(); } }
            finally { if (IsCurrent(version)) { LoadingIndicator.IsActive = false; LoadingIndicator.Visibility = Visibility.Collapsed; UpdateStatusBanner(); } }
        }
        private async void OnTileClicked(object sender, PhoneTileEventArgs args)
        {
            if (args.Route == "playme") { await PlayMe(); return; }
            if (args.Route == "album" && args.Album != null) { Frame.Navigate(typeof(AlbumPage), args.Album.Id); return; }
            if (args.Route == "playlist" && args.Playlist != null) { Frame.Navigate(typeof(MixesPage), args.Playlist.Id); return; }
            Navigate(args.Route);
        }
        private async Task PlayMe()
        {
            if (startingPlayback) return;
            var version = generation; startingPlayback = true; HomeTiles.IsBusy = true;
            try
            {
                var service = await PhoneSubsonicServiceFactory.CreateAsync();
                if (!IsCurrent(version)) return;
                if (string.IsNullOrEmpty(service.Configuration.BaseUrl)) { StatusText.Text = PhoneText.Get("ConfigureServer"); UpdateStatusBanner(); return; }
                var songs = await new NavidromeApi(service.Configuration).GetRandomSongs(new RandomSongQuery { Size = 30 }).ReadAsync();
                if (!IsCurrent(version)) return;
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("NoSongs"); UpdateStatusBanner(); return; }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { IsMix = true, Playlist = songs.Select((song, index) => new SongDisplayItem { Id = song.Id, Track = index + 1, Title = song.Title ?? song.Name, Artist = song.Artist, Album = song.Album, CoverArt = song.CoverArt, Duration = song.Duration }).ToList(), StartIndex = 0 });
            }
            catch (Exception ex) { if (IsCurrent(version)) { StatusText.Text = PhoneText.Format("LoadFailed", ex.Message); UpdateStatusBanner(); } }
            finally { startingPlayback = false; HomeTiles.IsBusy = false; }
        }
        private void Navigate(string route)
        {
            switch (route)
            {
                case "library": Frame.Navigate(typeof(LibraryPage), "artists"); break;
                case "recent": Frame.Navigate(typeof(LibraryPage), "recent"); break;
                case "mixes": Frame.Navigate(typeof(MixesPage)); break;
                case "create": Frame.Navigate(typeof(CreateMixPage)); break;
                case "offline": Frame.Navigate(typeof(OfflineMixesPage)); break;
                case "favourites": Frame.Navigate(typeof(FavouritesPage)); break;
                case "player": Frame.Navigate(typeof(PlaybackPage)); break;
            }
        }
        private void OnMenuClick(object sender, RoutedEventArgs args) { Navigate(((Button)sender).Tag as string); }
        private void OnSettingsClick(object sender, RoutedEventArgs args) { Frame.Navigate(typeof(SettingsPage)); }
        private async void OnHelpClick(object sender, RoutedEventArgs args) { await new MessageDialog(PhoneText.Get("AboutMessage"), PhoneText.Get("UiMixDrome")).ShowAsync(); }
        private void OnSizeChanged(object sender, SizeChangedEventArgs args) { ResizeSections(); }
        private void ResizeSections()
        {
            if (ActualWidth <= 0) return;
            var scale = ActualWidth / 480;
            Sections.Margin = new Thickness(24 * scale, 168 * scale, 96 * scale, 0);
            PlaySection.Width = 432 * scale; HomeTiles.Width = 420 * scale; MenuSection.Width = 360 * scale;
            Wordmark.FontSize = 64 * scale; Wordmark.Margin = new Thickness(20 * scale, 7 * scale, 0, 0);
            HomeHeader.Height = 168 * scale;
            MenuRowOne.Height = MenuRowTwo.Height = MenuRowThree.Height = new GridLength(180 * scale);
            foreach (var tile in new[] { MixesTile, CreateTile, FavouritesTile, MusicTile, OfflineTile })
            {
                tile.Height = 168 * scale; tile.Margin = new Thickness(0, 0, 12 * scale, 12 * scale);
                tile.Width = (tile == FavouritesTile ? 348 : 168) * scale;
                tile.HorizontalAlignment = HorizontalAlignment.Left;
                var grid = (Grid)tile.Content;
                foreach (var child in grid.Children.OfType<TextBlock>())
                {
                    child.FontSize = 21 * scale; child.LineHeight = 22 * scale; child.MaxHeight = 44 * scale;
                    child.Margin = new Thickness(12 * scale, 12 * scale, 12 * scale, 12 * scale);
                }
            }
            ArtistBackdrop.Width = ActualWidth * 1.18; ArtistBackdrop.Height = ActualHeight;
        }
        private void OnPanoramaViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
        {
            BackdropTranslation.X = -Panorama.HorizontalOffset * 0.18;
            MenuBar.Visibility = Panorama.HorizontalOffset >= PlaySection.Width * 0.5 ? Visibility.Visible : Visibility.Collapsed;
            if (!args.IsIntermediate)
            {
                var target = Panorama.HorizontalOffset >= PlaySection.Width * 0.5 ? Math.Min(PlaySection.Width, Panorama.ScrollableWidth) : 0;
                if (Math.Abs(Panorama.HorizontalOffset - target) > 0.5) Panorama.ChangeView(target, null, null, false);
            }
        }
    }
}
