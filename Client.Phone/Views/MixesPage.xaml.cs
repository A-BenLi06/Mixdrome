namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Client.Common.Models.Navidrome;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.Phone.UI.Input;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class MixesPage : Page
    {
        private NavidromeApi api;
        private SubsonicServiceConfiguration configuration;
        private CancellationTokenSource saveCancellation;
        private bool active;
        private int generation;
        private int selectionGeneration;
        private int pendingPlaySelection;
        private ItemsWrapGrid featuredPanel;
        private ItemsWrapGrid categoriesPanel;
        private readonly HashSet<string> loaded = new HashSet<string>();
        private readonly HashSet<string> loading = new HashSet<string>();
        public ObservableCollection<PhoneCollectionItem> Featured { get; private set; } = new ObservableCollection<PhoneCollectionItem>();
        public ObservableCollection<PhoneCollectionItem> Categories { get; private set; } = new ObservableCollection<PhoneCollectionItem>();
        private string Tab { get { return MixesPivot.SelectedIndex == 1 ? "categories" : "featured"; } }

        public MixesPage()
        {
            InitializeComponent(); DataContext = this;
            FeaturedTab.Header = new PhoneCollectionHeading { Name = PhoneText.Get("UiFeatured") }; CategoriesTab.Header = new PhoneCollectionHeading { Name = PhoneText.Get("UiCategories") };
            SectionBar.Title = CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiMixes"));
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e); active = true; HardwareButtons.BackPressed += OnBackPressed; await Initialize();
            var playlistId = e.Parameter as string;
            if (active && api != null && e.NavigationMode != NavigationMode.Back && !string.IsNullOrEmpty(playlistId)) await OpenPlaylist(playlistId);
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            active = false; generation++; selectionGeneration++; if (saveCancellation != null) saveCancellation.Cancel(); HardwareButtons.BackPressed -= OnBackPressed; base.OnNavigatedFrom(e);
        }
        private bool Current(int version) { return active && generation == version; }
        private async Task Initialize()
        {
            var version = ++generation; selectionGeneration++; pendingPlaySelection = 0; if (saveCancellation != null) saveCancellation.Cancel(); api = null; loaded.Clear(); loading.Clear(); Featured.Clear(); Categories.Clear();
            StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                var service = await PhoneSubsonicServiceFactory.CreateAsync();
                if (!Current(version)) return;
                if (string.IsNullOrEmpty(service.Configuration.BaseUrl)) { StatusText.Text = PhoneText.Get("ConfigureServer"); return; }
                configuration = (SubsonicServiceConfiguration)service.Configuration; api = new NavidromeApi(configuration); await Load(Tab);
            }
            catch (Exception exception) { if (Current(version)) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version)) UpdateBusy(); }
        }
        private async Task Load(string tab)
        {
            if (api == null || loaded.Contains(tab) || loading.Contains(tab)) return;
            var version = generation; var source = api; var target = tab == "featured" ? Featured : Categories;
            loading.Add(tab); StatusText.Text = string.Empty; UpdateBusy();
            try
            {
                var rows = new List<PhoneCollectionItem>();
                if (tab == "featured")
                {
                    foreach (var playlist in await source.GetPlaylists().ReadAsync())
                        rows.Add(new PhoneCollectionItem { Route = "playlist", Id = playlist.Id, Name = playlist.Name, Artwork = PhoneCollectionPresentation.Artwork(source, playlist.CoverArt) });
                    // A server without playlists can still expose its real recently played albums.
                    if (rows.Count == 0)
                        foreach (var album in await source.GetAlbums(new AlbumQuery { Type = AlbumListType.Recent, Size = 100 }).ReadAsync())
                            rows.Add(new PhoneCollectionItem { Route = "album", Id = album.Id, Name = album.Name, Artwork = PhoneCollectionPresentation.Artwork(source, album.CoverArt) });
                }
                else
                    foreach (var genre in await source.GetGenres(null).ReadAsync())
                        rows.Add(new PhoneCollectionItem { Route = "genre", Name = genre.Name, Artwork = PhoneCollectionPresentation.DefaultArtwork });
                if (!Current(version)) return;
                target.Clear(); foreach (var row in rows) target.Add(row); loaded.Add(tab); Resize(tab);
                if (Tab == tab) StatusText.Text = rows.Count == 0 ? PhoneText.Get("LibraryEmpty") : string.Empty;
                // Metadata is usable immediately. Retrieve a genre's actual first cover lazily.
                if (tab == "categories")
                    foreach (var row in rows)
                    {
                        if (!Current(version)) return;
                        try
                        {
                            var song = (await source.GetGenreSongs(row.Name, null, 0, 1).ReadAsync()).FirstOrDefault();
                            if (Current(version) && song != null) row.Artwork = PhoneCollectionPresentation.Artwork(source, song.CoverArt);
                        }
                        catch (Exception) { /* Keep the usable category and packaged fallback when its optional cover cannot load. */ }
                    }
            }
            catch (Exception exception) { if (Current(version) && Tab == tab) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version)) { loading.Remove(tab); UpdateBusy(); } }
        }
        private void UpdateBusy()
        {
            LoadingIndicator.IsActive = loading.Contains(Tab) || saveCancellation != null || pendingPlaySelection != 0 && pendingPlaySelection == selectionGeneration;
            LoadingIndicator.Visibility = LoadingIndicator.IsActive ? Visibility.Visible : Visibility.Collapsed;
            StatusPanel.Visibility = LoadingIndicator.IsActive || !string.IsNullOrWhiteSpace(StatusText.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        private async void OnTabChanged(object sender, SelectionChangedEventArgs e) { if (StatusText == null) return; selectionGeneration++; StatusText.Text = string.Empty; UpdateBusy(); if (active) await Load(Tab); }
        private void OnGridSizeChanged(object sender, SizeChangedEventArgs e) { Resize(ReferenceEquals(sender, CategoriesGrid) ? "categories" : "featured"); }
        private void OnGridLoaded(object sender, RoutedEventArgs e) { Resize(ReferenceEquals(sender, CategoriesGrid) ? "categories" : "featured"); }
        private void OnPanelLoaded(object sender, RoutedEventArgs e) { RegisterPanel(sender as ItemsWrapGrid); }
        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) { RegisterPanel(sender as ItemsWrapGrid); }
        private void RegisterPanel(ItemsWrapGrid panel)
        {
            if (panel == null) return;
            for (DependencyObject parent = panel; parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (ReferenceEquals(parent, FeaturedGrid)) { featuredPanel = panel; Resize("featured"); return; }
                if (ReferenceEquals(parent, CategoriesGrid)) { categoriesPanel = panel; Resize("categories"); return; }
            }
        }
        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MixesPivot == null) return;
            foreach (var tab in MixesPivot.Items.OfType<PivotItem>()) { var heading = tab.Header as PhoneCollectionHeading; if (heading != null) heading.Size = 72 * ActualWidth / 480; }
            Resize("featured"); Resize("categories");
        }
        private void Resize(string tab)
        {
            var grid = tab == "featured" ? FeaturedGrid : CategoriesGrid; if (grid == null || ActualWidth <= 0) return;
            var scale = ActualWidth / 480;
            grid.Margin = new Thickness(24 * scale, 0, 6 * scale, 0);
            var panel = tab == "featured" ? featuredPanel : categoriesPanel;
            var viewport = grid.ActualWidth;
            for (DependencyObject parent = panel; parent != null && !ReferenceEquals(parent, grid); parent = VisualTreeHelper.GetParent(parent))
            {
                var scroll = parent as ScrollViewer;
                if (scroll != null && scroll.ViewportWidth > 0) { viewport = scroll.ViewportWidth; break; }
            }
            // Native WP layout rounds fractional DIPs when it measures cells.
            // Whole-DIP cells fit twice within the measured viewport, including
            // the card's proportional right-hand spacing, without overflow.
            var cellWidth = Math.Floor((viewport > 0 ? viewport : 450 * scale) / 2);
            if (cellWidth <= 0) return;
            if (panel != null) { panel.ItemWidth = cellWidth; panel.ItemHeight = cellWidth; }
            foreach (var item in tab == "featured" ? Featured : Categories) { item.Size = cellWidth * 207 / 225; item.TypographyScale = scale; }
        }
        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var selection = ++selectionGeneration; var item = e.ClickedItem as PhoneCollectionItem;
            if (item == null || api == null || !active) { UpdateBusy(); return; }
            if (item.Route == "album") { Frame.Navigate(typeof(AlbumPage), item.Id); return; }
            var version = generation; var source = api; var tab = Tab; pendingPlaySelection = selection; UpdateBusy();
            try
            {
                var entries = await PhoneCollectionPresentation.LoadSongsAsync(source, item, () => Current(version) && selection == selectionGeneration && Tab == tab);
                if (!Current(version) || selection != selectionGeneration || Tab != tab) return;
                var songs = entries.Select((song, index) => PhoneCollectionPresentation.Song(song, index + 1)).ToList();
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("PlaylistEmpty"); return; }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = songs, StartIndex = 0, IsMix = true, MixTitle = item.Name });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { if (Current(version) && selection == selectionGeneration) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version) && selection == selectionGeneration) { pendingPlaySelection = 0; UpdateBusy(); } }
        }
        private async Task OpenPlaylist(string id)
        {
            var version = generation; var selection = ++selectionGeneration; var source = api;
            pendingPlaySelection = selection; UpdateBusy();
            try
            {
                var playlist = await source.GetPlaylist(id).ReadAsync();
                if (!Current(version) || selection != selectionGeneration) return;
                var songs = playlist.Entries.Select((song, index) => PhoneCollectionPresentation.Song(song, index + 1)).ToList();
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("PlaylistEmpty"); return; }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = songs, StartIndex = 0, IsMix = true, MixTitle = playlist.Name });
            }
            catch (Exception exception) { if (Current(version) && selection == selectionGeneration) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (Current(version) && selection == selectionGeneration) { pendingPlaySelection = 0; UpdateBusy(); } }
        }
        private void OnArtworkFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            var item = image == null ? null : image.DataContext as PhoneCollectionItem;
            if (item != null && item.Artwork != PhoneCollectionPresentation.DefaultArtwork) item.Artwork = PhoneCollectionPresentation.DefaultArtwork;
        }
        private async void OnCardHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != Windows.UI.Input.HoldingState.Started || saveCancellation != null || api == null) return;
            e.Handled = true; var item = ((FrameworkElement)sender).DataContext as PhoneCollectionItem; if (item == null) return;
            var version = generation; var source = api; var settings = configuration; var cancellation = new CancellationTokenSource(); saveCancellation = cancellation; UpdateBusy();
            try
            {
                var songs = await PhoneCollectionPresentation.LoadSongsAsync(source, item, () => Current(version) && !cancellation.IsCancellationRequested);
                if (!Current(version) || cancellation.IsCancellationRequested) return;
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("PlaylistEmpty"); return; }
                Frame.Navigate(typeof(OfflineMixesPage), new PhoneOfflineRequest { Configuration = settings,
                    Mix = new PhoneMixDefinition { Id = item.Route + ":" + (item.Id ?? item.Name), Name = item.Name, Songs = songs } });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { if (Current(version)) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (ReferenceEquals(saveCancellation, cancellation)) saveCancellation = null; cancellation.Dispose(); if (Current(version)) UpdateBusy(); }
        }
        private void OnBackPressed(object sender, BackPressedEventArgs e) { if (!e.Handled && Frame.CanGoBack) { e.Handled = true; Frame.GoBack(); } }
    }
}
