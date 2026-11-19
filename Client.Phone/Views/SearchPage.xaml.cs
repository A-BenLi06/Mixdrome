namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Models.Navidrome;
    using Client.Common.Services;
    using Client.Common.Services.Navidrome;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Windows.Phone.UI.Input;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class SearchPage : Page
    {
        private ISubsonicService _subsonicService;
        private TextBox _searchTextBox;
        private ProgressRing _loadingIndicator;
        private TextBlock _emptyStateText;
        private Pivot _resultsListView;
        private int _generation;
        private bool _active;
        private int _navigationGeneration;
        private Task _initializing;
        private NavidromeApi _api;
        private AppBarButton _moreButton;
        private bool _isSearching;
        private string _query;
        private const int PageSize = 100;
        private readonly HashSet<SearchResultKind> _hasMore = new HashSet<SearchResultKind>();

        public ObservableCollection<SearchResultDisplayItem> Results { get; private set; }
        public List<SongDisplayItem> SongResults { get; private set; }
        public ObservableCollection<SearchResultDisplayItem> ArtistResults { get; private set; } = new ObservableCollection<SearchResultDisplayItem>();
        public ObservableCollection<SearchResultDisplayItem> AlbumResults { get; private set; } = new ObservableCollection<SearchResultDisplayItem>();
        public ObservableCollection<SearchResultDisplayItem> TrackResults { get; private set; } = new ObservableCollection<SearchResultDisplayItem>();

        public SearchPage()
        {
            Results = new ObservableCollection<SearchResultDisplayItem>();
            SongResults = new List<SongDisplayItem>();

            InitializeComponent();
            DataContext = this;

            var sectionBar = FindName("SectionBar") as Mixdrome.Phone.Common.PhoneTitleBar;
            if (sectionBar != null) sectionBar.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(Mixdrome.Phone.Common.PhoneText.Get("UiSearch"));
            _searchTextBox = FindName("SearchTextBox") as TextBox;
            _loadingIndicator = FindName("LoadingIndicator") as ProgressRing;
            _emptyStateText = FindName("EmptyStateText") as TextBlock;
            _resultsListView = FindName("ResultsPivot") as Pivot;
            _moreButton = FindName("MoreButton") as AppBarButton;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnHardwareBackPressed;

            _active = true;
            var version = ++_generation;
            _api = null; _subsonicService = null; _query = null;
            ClearResults();
            _initializing = InitializeService(++_navigationGeneration);
            SetBusy(true);
            try { await _initializing; }
            catch (Exception ex) { if (_active && version == _generation) ShowEmpty(Mixdrome.Phone.Common.PhoneText.Format("LoadFailed", ex.Message)); return; }
            finally { if (_active && version == _generation) SetBusy(false); }
            if (!_active || version != _generation) return;

            var query = e.Parameter as string;
            if (!string.IsNullOrWhiteSpace(query) && _searchTextBox != null)
            {
                _searchTextBox.Text = query;
                await Search(query);
            }
            else if (_searchTextBox != null)
            {
                _searchTextBox.Focus(FocusState.Programmatic);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _active = false; _generation++; _navigationGeneration++;
            SetBusy(false);
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private async Task InitializeService(int navigationVersion)
        {
            var service = await Mixdrome.Phone.Services.PhoneSubsonicServiceFactory.CreateAsync();
            if (!_active || navigationVersion != _navigationGeneration) return;
            _subsonicService = service;
            if (HasConfiguredServer()) _api = new NavidromeApi(service.Configuration);
        }

        private bool HasConfiguredServer()
        {
            return _subsonicService != null
                   && _subsonicService.Configuration != null
                   && !string.IsNullOrEmpty(_subsonicService.Configuration.BaseUrl);
        }

        private async Task Search(string query, bool append = false)
        {
            var version = ++_generation;
            if (string.IsNullOrWhiteSpace(query))
            {
                _query = null; ClearResults(); SetBusy(false);
                ShowEmpty(Mixdrome.Phone.Common.PhoneText.Get("UiTypeSomethingToSearchYourMusic"));
                return;
            }
            var kind = SelectedKind;
            if (!append) { _query = query.Trim(); ClearResults(); }
            SetBusy(true);
            if (_emptyStateText != null) _emptyStateText.Visibility = Visibility.Collapsed;

            try
            {
                if (_initializing != null) await _initializing;
                if (!_active || version != _generation) return;
                if (_api == null)
                {
                    ShowEmpty(Mixdrome.Phone.Common.PhoneText.Get("ConfigureServer"));
                    return;
                }
                var request = new SearchQuery
                {
                    Query = _query,
                    ArtistCount = !append || kind == SearchResultKind.Artist ? PageSize : 0,
                    AlbumCount = !append || kind == SearchResultKind.Album ? PageSize : 0,
                    SongCount = !append || kind == SearchResultKind.Song ? PageSize : 0,
                    ArtistOffset = append ? ArtistResults.Count : 0,
                    AlbumOffset = append ? AlbumResults.Count : 0,
                    SongOffset = append ? TrackResults.Count : 0
                };
                var collection = await _api.Search(request).ReadAsync();
                if (!_active || version != _generation) return;
                if (collection != null)
                {
                    if (!append || kind == SearchResultKind.Artist)
                    { AddArtists(collection.Artists); SetMore(SearchResultKind.Artist, collection.Artists == null ? 0 : collection.Artists.Count); }
                    if (!append || kind == SearchResultKind.Album)
                    { AddAlbums(collection.Albums); SetMore(SearchResultKind.Album, collection.Albums == null ? 0 : collection.Albums.Count); }
                    if (!append || kind == SearchResultKind.Song)
                    { AddSongs(collection.Songs); SetMore(SearchResultKind.Song, collection.Songs == null ? 0 : collection.Songs.Count); }
                }

                if (_resultsListView != null)
                {
                    _resultsListView.Visibility = Results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                if (Results.Count == 0)
                {
                    ShowEmpty(Mixdrome.Phone.Common.PhoneText.Get("UiNoResultsFound"));
                }
            }
            catch (Exception ex)
            {
                if (!_active || version != _generation) return;
                ShowEmpty(Mixdrome.Phone.Common.PhoneText.Get("UiSearchFailed") + ex.Message);
            }
            finally
            {
                if (_active && version == _generation) SetBusy(false);
            }
        }

        private void AddResult(SearchResultDisplayItem item)
        {
            Results.Add(item);
            if (item.Kind == SearchResultKind.Artist) ArtistResults.Add(item);
            else if (item.Kind == SearchResultKind.Album) AlbumResults.Add(item);
            else TrackResults.Add(item);
        }

        private void AddArtists(IEnumerable<ExpandedArtist> artists)
        {
            if (artists == null)
            {
                return;
            }

            foreach (var artist in artists)
            {
                AddResult(new SearchResultDisplayItem
                {
                    Kind = SearchResultKind.Artist,
                    KindLabel = Mixdrome.Phone.Common.PhoneText.Get("UiArtist"),
                    Id = artist.Id,
                    Title = artist.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownArtist"),
                    Subtitle = string.Format(Mixdrome.Phone.Common.PhoneText.Get("AlbumCount"), artist.AlbumCount),
                    Glyph = "\uE13D",
                    AccentBrush = App.Current.Resources["MixRadioYellowBrush"] as Brush,
                    ImageOpacity = 0
                });
            }
        }

        private void AddAlbums(IEnumerable<Album> albums)
        {
            if (albums == null)
            {
                return;
            }

            foreach (var album in albums)
            {
                AddResult(new SearchResultDisplayItem
                {
                    Kind = SearchResultKind.Album,
                    KindLabel = Mixdrome.Phone.Common.PhoneText.Get("UiAlbum"),
                    Id = album.Id,
                    Title = album.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownAlbum"),
                    Subtitle = album.Artist ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownArtist"),
                    CoverArt = GetCoverArtUrl(album.CoverArt),
                    Glyph = string.Empty,
                    AccentBrush = App.Current.Resources["MixRadioTealBrush"] as Brush,
                    ImageOpacity = 1
                });
            }
        }

        private void AddSongs(IEnumerable<Song> songs)
        {
            if (songs == null)
            {
                return;
            }

            var track = SongResults.Count + 1;
            foreach (var song in songs)
            {
                SongResults.Add(new SongDisplayItem
                {
                    Id = song.Id,
                    Track = track,
                    Title = song.Title ?? song.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownTrack"),
                    Artist = song.Artist ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownArtist"),
                    Album = song.Album,
                    Duration = song.Duration,
                    DurationString = FormatDuration(song.Duration),
                    CoverArt = song.CoverArt
                });

                AddResult(new SearchResultDisplayItem
                {
                    Kind = SearchResultKind.Song,
                    KindLabel = Mixdrome.Phone.Common.PhoneText.Get("UiSong"),
                    Id = song.Id,
                    Title = song.Title ?? song.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownTrack"),
                    Subtitle = song.Artist ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownArtist"),
                    CoverArt = GetCoverArtUrl(song.CoverArt),
                    Glyph = string.IsNullOrEmpty(song.CoverArt) ? "\uE102" : string.Empty,
                    AccentBrush = App.Current.Resources["MixRadioAccentBrush"] as Brush,
                    ImageOpacity = string.IsNullOrEmpty(song.CoverArt) ? 0 : 1
                });
                track++;
            }
        }

        private string GetCoverArtUrl(string coverArt)
        {
            return !string.IsNullOrEmpty(coverArt)
                ? _subsonicService.GetCoverArtForId(coverArt)
                : "/Assets/CoverArtPlaceholder.jpg";
        }

        private static string FormatDuration(int seconds)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            return string.Format("{0}:{1:D2}", minutes, secs);
        }

        private void ShowEmpty(string text)
        {
            if (_emptyStateText != null)
            {
                _emptyStateText.Text = text;
                _emptyStateText.Visibility = Visibility.Visible;
            }

            if (_resultsListView != null)
            {
                _resultsListView.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnSearchButtonClick(object sender, RoutedEventArgs e)
        {
            await Search(_searchTextBox != null ? _searchTextBox.Text : string.Empty);
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            _generation++; _query = null; SetBusy(false);
            if (_searchTextBox != null)
            {
                _searchTextBox.Text = string.Empty;
                _searchTextBox.Focus(FocusState.Programmatic);
            }

            ClearResults();
            ShowEmpty(Mixdrome.Phone.Common.PhoneText.Get("UiTypeSomethingToSearchYourMusic"));
        }

        private void ClearResults()
        {
            Results.Clear(); ArtistResults.Clear(); AlbumResults.Clear(); TrackResults.Clear(); SongResults.Clear();
            _hasMore.Clear(); UpdateMore();
        }

        private SearchResultKind SelectedKind
        {
            get { var index = _resultsListView == null ? 0 : _resultsListView.SelectedIndex;
                return index == 1 ? SearchResultKind.Album : index == 2 ? SearchResultKind.Song : SearchResultKind.Artist; }
        }

        private void SetMore(SearchResultKind kind, int count)
        { if (count == PageSize) _hasMore.Add(kind); else _hasMore.Remove(kind); }

        private void SetBusy(bool value)
        { _isSearching = value; if (_loadingIndicator != null) _loadingIndicator.IsActive = value; UpdateMore(); }

        private void UpdateMore()
        {
            if (_moreButton == null) return;
            _moreButton.Visibility = _hasMore.Contains(SelectedKind) ? Visibility.Visible : Visibility.Collapsed;
            _moreButton.IsEnabled = !_isSearching;
        }

        private void OnResultsTabChanged(object sender, SelectionChangedEventArgs e) { UpdateMore(); }
        private async void OnMoreClick(object sender, RoutedEventArgs e)
        { if (!_isSearching && _hasMore.Contains(SelectedKind)) await Search(_query, true); }

        private async void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await Search(_searchTextBox != null ? _searchTextBox.Text : string.Empty);
            }
        }

        private void OnResultClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as SearchResultDisplayItem;
            if (item == null)
            {
                return;
            }

            if (item.Kind == SearchResultKind.Album)
            {
                Frame.Navigate(typeof(AlbumPage), item.Id);
                return;
            }

            if (item.Kind == SearchResultKind.Song)
            {
                var startIndex = Math.Max(0, SongResults.FindIndex(song => song.Id == item.Id));
                Frame.Navigate(
                    typeof(PlaybackPage),
                    new PlaybackNavigationParameter
                    {
                        Playlist = SongResults.ToList(),
                        StartIndex = startIndex
                    });
                return;
            }

            Frame.Navigate(
                typeof(ArtistAlbumsPage),
                new ArtistDisplayItem
                {
                    Id = item.Id,
                    Name = item.Title,
                    Subtitle = item.Subtitle,
                    Initial = GetInitial(item.Title)
                });
        }

        private static string GetInitial(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }

    public enum SearchResultKind
    {
        Artist,
        Album,
        Song
    }

    public class SearchResultDisplayItem
    {
        public SearchResultKind Kind { get; set; }
        public string KindLabel { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string CoverArt { get; set; }
        public string Glyph { get; set; }
        public Brush AccentBrush { get; set; }
        public double ImageOpacity { get; set; }
    }
}
