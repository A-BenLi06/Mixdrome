namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
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

    public sealed partial class FavouritesPage : Page
    {
        private NavidromeApi api;
        private bool active;
        private bool busy;
        private bool selecting;
        private bool updatesLoaded;
        private bool updatesMore;
        private int updatesOffset;
        private int generation;
        private string selectedTab;
        private IList<ExpandedArtist> starredArtists = new List<ExpandedArtist>();
        public ObservableCollection<PhoneArtistGroup> ArtistGroups { get; private set; } = new ObservableCollection<PhoneArtistGroup>();
        public ObservableCollection<PhoneCollectionItem> Updates { get; private set; } = new ObservableCollection<PhoneCollectionItem>();
        public ObservableCollection<PhoneCollectionItem> Albums { get; private set; } = new ObservableCollection<PhoneCollectionItem>();
        public ObservableCollection<PhoneCollectionItem> Songs { get; private set; } = new ObservableCollection<PhoneCollectionItem>();
        private string Tab { get { var item = FavouritesPivot.SelectedItem as PivotItem; return item == null ? "artists" : item.Tag as string; } }

        public FavouritesPage()
        {
            InitializeComponent(); DataContext = this; ArtistGroupsSource.Source = ArtistGroups;
            ArtistsTab.Header = Heading("UiArtists"); UpdatesTab.Header = Heading("UiWhatsNew"); AlbumsTab.Header = Heading("UiAlbums"); SongsTab.Header = Heading("UiSongs");
            SectionBar.Title = CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiFavourites"));
            selectedTab = Tab;
        }
        private static PhoneCollectionHeading Heading(string key) { return new PhoneCollectionHeading { Name = PhoneText.Get(key) }; }
        protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); active = true; HardwareButtons.BackPressed += OnBackPressed; await Initialize(); }
        protected override void OnNavigatedFrom(NavigationEventArgs e) { active = false; generation++; HardwareButtons.BackPressed -= OnBackPressed; base.OnNavigatedFrom(e); }
        private bool Current(int version) { return active && generation == version; }
        private async Task Initialize()
        {
            var version = ++generation; var released = false; api = null; busy = true; selecting = false; updatesLoaded = updatesMore = false; updatesOffset = 0;
            ArtistGroups.Clear(); Albums.Clear(); Songs.Clear(); Updates.Clear(); StatusText.Text = string.Empty; UpdateCommands();
            try
            {
                var service = await PhoneSubsonicServiceFactory.CreateAsync(); if (!Current(version)) return;
                if (string.IsNullOrEmpty(service.Configuration.BaseUrl)) { StatusText.Text = PhoneText.Get("ConfigureServer"); return; }
                var source = new NavidromeApi(service.Configuration); var starred = await source.GetStarred(null).ReadAsync(); if (!Current(version)) return;
                api = source; starredArtists = starred.Artists;
                var artists = starred.Artists.Select(artist => new PhoneCollectionItem { Id = artist.Id, Name = artist.Name, Route = "artist", Artist = artist, Artwork = PhoneCollectionPresentation.Artwork(source, string.IsNullOrWhiteSpace(artist.ArtistImageUrl) ? artist.CoverArt : artist.ArtistImageUrl, true) });
                foreach (var group in PhoneCollectionPresentation.GroupArtists(artists)) ArtistGroups.Add(group);
                foreach (var album in starred.Albums) Albums.Add(AlbumItem(source, album));
                foreach (var song in starred.Songs) Songs.Add(new PhoneCollectionItem { Route = "song", Id = song.Id, Name = song.Title ?? song.Name, Subtitle = song.Artist, Song = PhoneCollectionPresentation.Song(song, Songs.Count + 1) });
                Resize(); ShowEmpty();
                busy = false; released = true; UpdateCommands();
                if (Tab == "updates") await LoadUpdates(false);
                // Artwork is optional metadata; the artist list remains usable while it arrives.
                foreach (var item in ArtistGroups.SelectMany(group => group).ToList())
                {
                    if (!Current(version)) return;
                    try
                    {
                        var info = await source.GetArtistInfo(item.Id).ReadAsync();
                        if (!Current(version)) return;
                        if (string.IsNullOrWhiteSpace(info.ImageUrl))
                        {
                            var artist = await source.GetArtist(item.Id).ReadAsync();
                            if (artist != null) info.ImageUrl = artist.ArtistImageUrl;
                        }
                        if (Current(version) && !string.IsNullOrWhiteSpace(info.ImageUrl)) item.Artwork = info.ImageUrl;
                    }
                    catch (Exception) { /* Retain server cover art or the packaged artist fallback. */ }
                }
            }
            catch (Exception exception) { if (Current(version)) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version) && !released) { busy = false; UpdateCommands(); } }
        }
        private PhoneCollectionItem AlbumItem(NavidromeApi source, Album album) { return new PhoneCollectionItem { Route = "album", Id = album.Id, Name = album.Name, Subtitle = album.Artist, Artwork = PhoneCollectionPresentation.Artwork(source, album.CoverArt) }; }
        private async Task LoadUpdates(bool append)
        {
            if (api == null || busy || (!append && updatesLoaded) || append && !updatesMore) return;
            var version = generation; var source = api; busy = true; StatusText.Text = string.Empty; UpdateCommands();
            try
            {
                var found = 0;
                // Newest is library-added order supplied by the server, not a fabricated release date.
                while (starredArtists.Count > 0)
                {
                    var page = await source.GetAlbums(new AlbumQuery { Type = AlbumListType.Newest, Size = 100, Offset = updatesOffset }).ReadAsync();
                    if (!Current(version)) return;
                    updatesOffset += page.Count; updatesMore = page.Count == 100;
                    foreach (var album in PhoneCollectionPresentation.FavouriteUpdates(page, starredArtists))
                        if (Updates.All(item => item.Id != album.Id)) { Updates.Add(AlbumItem(source, album)); found++; }
                    Resize();
                    if (!updatesMore || found >= 25) break;
                }
                updatesLoaded = true; if (Tab == "updates") ShowEmpty();
            }
            catch (Exception exception) { if (Current(version) && Tab == "updates") StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version)) { busy = false; UpdateCommands(); } }
        }
        private void Resize()
        {
            if (ActualWidth <= 0 || FavouritesPivot == null) return;
            var scale = ActualWidth / 480;
            UpdatesList.Margin = new Thickness(0, 5 * scale, 0, 0);
            foreach (var tab in FavouritesPivot.Items.OfType<PivotItem>()) { var heading = tab.Header as PhoneCollectionHeading; if (heading != null) heading.Size = 72 * scale; }
            foreach (var group in ArtistGroups) { group.Size = 68 * scale; foreach (var item in group) item.Size = 90 * scale; }
            foreach (var item in Albums.Concat(Updates)) item.Size = 120 * scale;
        }
        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) { Resize(); }
        private void ShowEmpty()
        {
            var empty = Tab == "artists" ? ArtistGroups.Count == 0 : Tab == "updates" ? Updates.Count == 0 : Tab == "albums" ? Albums.Count == 0 : Songs.Count == 0;
            StatusText.Text = empty ? (Tab == "updates" ? PhoneText.Get("FavouriteUpdatesEmpty") : PhoneText.Get("UiNoFavouritesFound")) : string.Empty;
        }
        private void UpdateCommands()
        {
            if (LoadingIndicator == null) return;
            LoadingIndicator.IsActive = busy; LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            StatusPanel.Visibility = busy || !string.IsNullOrWhiteSpace(StatusText.Text) ? Visibility.Visible : Visibility.Collapsed;
            FavouritesCommandBar.ClosedDisplayMode = Tab == "artists" ? AppBarClosedDisplayMode.Compact : AppBarClosedDisplayMode.Minimal;
            AddArtistButton.Visibility = SelectArtistsButton.Visibility = Tab == "artists" && !selecting ? Visibility.Visible : Visibility.Collapsed;
            RemoveArtistsButton.Visibility = DoneButton.Visibility = selecting && Tab == "artists" ? Visibility.Visible : Visibility.Collapsed;
            AddArtistButton.IsEnabled = SelectArtistsButton.IsEnabled = RemoveArtistsButton.IsEnabled = !busy;
            MoreButton.Visibility = Tab == "updates" && updatesMore ? Visibility.Visible : Visibility.Collapsed; MoreButton.IsEnabled = !busy;
            PlayAllButton.Visibility = ShuffleButton.Visibility = Tab == "songs" ? Visibility.Visible : Visibility.Collapsed;
            PlayAllButton.IsEnabled = ShuffleButton.IsEnabled = Songs.Count > 0 && !busy;
            ArtistsList.SelectionMode = selecting ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None; ArtistsList.IsItemClickEnabled = !selecting;
        }
        private async void OnTabChanged(object sender, SelectionChangedEventArgs e) { if (StatusText == null || Tab == selectedTab) return; selectedTab = Tab; selecting = false; ShowEmpty(); UpdateCommands(); if (active && Tab == "updates") await LoadUpdates(false); }
        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as PhoneCollectionItem; if (item == null || !active || !(ArtistGroups.SelectMany(group => group).Contains(item) || Albums.Contains(item) || Updates.Contains(item) || Songs.Contains(item))) return;
            if (item.Route == "artist") Frame.Navigate(typeof(ArtistAlbumsPage), new ArtistDisplayItem { Id = item.Id, Name = item.Name });
            else if (item.Route == "album") Frame.Navigate(typeof(AlbumPage), item.Id);
            else Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = Songs.Select(row => row.Song).ToList(), StartIndex = Songs.IndexOf(item) });
        }
        private async void OnAddArtistClick(object sender, RoutedEventArgs e)
        {
            if (api == null || busy) return; var version = generation; var source = api; busy = true; UpdateCommands();
            try
            {
                var artists = await source.GetArtists(null).ReadAsync(); if (!Current(version)) return;
                var ids = new HashSet<string>(starredArtists.Select(artist => artist.Id), StringComparer.Ordinal);
                var candidates = artists.Where(artist => !ids.Contains(artist.Id)).OrderBy(artist => artist.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                if (candidates.Count == 0) { StatusText.Text = PhoneText.Get("NoArtistsToAdd"); return; }
                var list = new ListView { ItemsSource = candidates, DisplayMemberPath = "Name", SelectionMode = ListViewSelectionMode.Single, MaxHeight = 360 };
                var dialog = new ContentDialog { Title = PhoneText.Get("AddArtist"), Content = list, PrimaryButtonText = PhoneText.Get("Add"), SecondaryButtonText = PhoneText.Get("Cancel"), IsPrimaryButtonEnabled = false };
                list.SelectionChanged += (s, args) => dialog.IsPrimaryButtonEnabled = list.SelectedItem != null;
                var result = await dialog.ShowAsync(); if (!Current(version)) return;
                var chosenArtist = list.SelectedItem as ExpandedArtist;
                if (result != ContentDialogResult.Primary || chosenArtist == null) return;
                await source.SetStarred(chosenArtist, true).ReadAsync(); if (Current(version)) await Initialize();
            }
            catch (Exception exception) { if (Current(version)) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version)) { busy = false; UpdateCommands(); } }
        }
        private void OnSelectArtistsClick(object sender, RoutedEventArgs e) { selecting = true; UpdateCommands(); }
        private void OnDoneClick(object sender, RoutedEventArgs e) { selecting = false; UpdateCommands(); }
        private async void OnRemoveArtistsClick(object sender, RoutedEventArgs e)
        {
            if (api == null || busy) return; var selected = ArtistsList.SelectedItems.OfType<PhoneCollectionItem>().ToList(); if (selected.Count == 0) return;
            var version = generation; var source = api; busy = true; UpdateCommands(); Exception failure = null;
            try
            {
                foreach (var item in selected) { await source.SetStarred(item.Artist, false).ReadAsync(); if (!Current(version)) return; }
            }
            catch (Exception exception) { failure = exception; }
            finally { if (Current(version)) { await Initialize(); if (failure != null) { StatusText.Text = PhoneText.Format("LoadFailed", failure.Message); UpdateCommands(); } } }
        }
        private void OnPlayAllClick(object sender, RoutedEventArgs e) { PlaySongs(false); }
        private void OnShuffleClick(object sender, RoutedEventArgs e) { PlaySongs(true); }
        private void PlaySongs(bool shuffle) { if (Songs.Count > 0) Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = Songs.Select(item => item.Song).ToList(), StartIndex = shuffle ? new Random().Next(Songs.Count) : 0, Shuffle = shuffle }); }
        private async void OnMoreClick(object sender, RoutedEventArgs e) { await LoadUpdates(true); }
        private async void OnRefreshClick(object sender, RoutedEventArgs e) { await Initialize(); }
        private void OnArtistArtworkFailed(object sender, ExceptionRoutedEventArgs e) { Fallback(sender, PhoneCollectionPresentation.DefaultArtist); }
        private void OnArtworkFailed(object sender, ExceptionRoutedEventArgs e) { Fallback(sender, PhoneCollectionPresentation.DefaultArtwork); }
        private static void Fallback(object sender, string source)
        {
            var image = sender as Image;
            var item = image == null ? null : image.DataContext as PhoneCollectionItem;
            // Preserve Image.Source's binding so later real artist metadata can
            // replace a failed initial cover instead of leaving the fallback stuck.
            if (item != null && item.Artwork != source) item.Artwork = source;
        }
        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return; if (selecting) { e.Handled = true; selecting = false; UpdateCommands(); }
            else if (Frame.CanGoBack) { e.Handled = true; Frame.GoBack(); }
        }
    }
}
