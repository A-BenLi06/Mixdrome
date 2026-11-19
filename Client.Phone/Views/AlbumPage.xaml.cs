namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Windows.Phone.UI.Input;
    using Windows.Storage;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class AlbumPage : Page
    {
        private ISubsonicService _subsonicService;
        private string _albumId;
        private Album _album;
        private int _generation;
        private bool _active;

        // Control references
        private ProgressRing _loadingIndicator;
        private TextBlock _albumNameText;
        private TextBlock _artistNameText;
        private TextBlock _songCountText;
        private ListView _songListView;
        private Image _albumArtImage;
        private Image _albumBackgroundImage;

        public ObservableCollection<SongDisplayItem> Songs { get; set; }

        public AlbumPage()
        {
            Songs = new ObservableCollection<SongDisplayItem>();
            InitializeComponent();
            DataContext = this;

            var sectionBar = FindName("SectionBar") as Mixdrome.Phone.Common.PhoneTitleBar;
            if (sectionBar != null) sectionBar.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(Mixdrome.Phone.Common.PhoneText.Get("UiMyMusic"));

            // Get control references after InitializeComponent
            _loadingIndicator = FindName("LoadingIndicator") as ProgressRing;
            _albumNameText = FindName("AlbumNameText") as TextBlock;
            _artistNameText = FindName("ArtistNameText") as TextBlock;
            _songCountText = FindName("SongCountText") as TextBlock;
            _songListView = FindName("SongListView") as ListView;
            _albumArtImage = FindName("AlbumArtImage") as Image;
            _albumBackgroundImage = FindName("AlbumBackgroundImage") as Image;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnHardwareBackPressed;

            _albumId = e.Parameter as string;
            _active = true;
            var version = ++_generation;
            try { await InitializeService(); if (_active && version == _generation) await LoadAlbum(); }
            catch (Exception ex) { if (_active && version == _generation) AlbumStatusText.Text = Mixdrome.Phone.Common.PhoneText.Format("LoadFailed", ex.Message); }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _active = false; _generation++;
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private async Task InitializeService()
        {
            _subsonicService = await Mixdrome.Phone.Services.PhoneSubsonicServiceFactory.CreateAsync();
        }

        private async Task LoadAlbum()
        {
            var version = _generation;
            if (string.IsNullOrEmpty(_albumId))
            {
                return;
            }

            if (_loadingIndicator != null) _loadingIndicator.IsActive = true;

            try
            {
                var result = _subsonicService.GetAlbum(_albumId);
                await result.Execute();
                if (!_active || version != _generation) return;
                if (result.Error != null) throw result.Error;

                if (result.Error == null && result.Result != null)
                {
                    _album = result.Result;
                    if (_albumNameText != null) _albumNameText.Text = _album.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownAlbum");
                    if (_artistNameText != null) _artistNameText.Text = _album.Artist ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownArtist");
                    var coverArt = _subsonicService.GetCoverArtForId(_album.CoverArt, ImageType.Original);
                    SetImageSource(_albumArtImage, coverArt);
                    SetImageSource(_albumBackgroundImage, coverArt);

                    Songs.Clear();
                    if (_album.Songs != null)
                    {
                        int trackNum = 1;
                        foreach (var song in _album.Songs)
                        {
                            Songs.Add(new SongDisplayItem
                            {
                                Id = song.Id,
                                Track = trackNum++,
                                Title = song.Title ?? song.Name ?? Mixdrome.Phone.Common.PhoneText.Get("UnknownTrack"),
                                Artist = song.Artist ?? _album.Artist,
                                Album = song.Album ?? _album.Name,
                                Duration = song.Duration,
                                DurationString = FormatDuration(song.Duration),
                                CoverArt = song.CoverArt ?? _album.CoverArt
                            });
                        }
                    }

                    if (_songCountText != null) _songCountText.Text = string.Format(Mixdrome.Phone.Common.PhoneText.Get("SongCount"), Songs.Count);
                }
            }
            catch (Exception ex)
            {
                if (_active && version == _generation) AlbumStatusText.Text = Mixdrome.Phone.Common.PhoneText.Format("LoadFailed", ex.Message);
            }
            finally
            {
                if (_active && version == _generation && _loadingIndicator != null) _loadingIndicator.IsActive = false;
            }
        }

        private static void SetImageSource(Image image, string source)
        {
            if (image == null || string.IsNullOrEmpty(source))
            {
                return;
            }

            image.Source = new BitmapImage(new Uri(source.StartsWith("/") ? "ms-appx://" + source : source));
        }

        private string FormatDuration(int seconds)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            return string.Format("{0}:{1:D2}", minutes, secs);
        }

        private void NavigateToPlayback(int startIndex)
        {
            if (Songs.Count == 0) return;

            var navParam = new PlaybackNavigationParameter
            {
                Playlist = Songs.ToList(),
                StartIndex = startIndex
            };
            Frame.Navigate(typeof(PlaybackPage), navParam);
        }

        private void OnSongClick(object sender, ItemClickEventArgs e)
        {
            var song = e.ClickedItem as SongDisplayItem;
            if (song != null)
            {
                var index = Songs.IndexOf(song);
                NavigateToPlayback(index >= 0 ? index : 0);
            }
        }

        private void OnPlayAllClick(object sender, RoutedEventArgs e)
        {
            NavigateToPlayback(0);
        }

        private void OnShuffleClick(object sender, RoutedEventArgs e)
        {
            if (Songs.Count > 0)
            {
                var random = new Random();
                var shuffledList = Songs.OrderBy(x => random.Next()).ToList();
                var navParam = new PlaybackNavigationParameter
                {
                    Playlist = shuffledList,
                    StartIndex = 0
                };
                Frame.Navigate(typeof(PlaybackPage), navParam);
            }
        }
    }

    public class SongDisplayItem
    {
        public string Id { get; set; }
        public int Track { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Duration { get; set; }
        public string DurationString { get; set; }
        public string CoverArt { get; set; }
        public string StreamUrl { get; set; }
    }
}
