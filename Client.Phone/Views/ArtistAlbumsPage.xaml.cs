namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Mixdrome.Phone.Services;
    using Windows.Phone.UI.Input;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class ArtistAlbumsPage : Page
    {
        private bool _pageActive;
        private ISubsonicService _subsonicService;
        private ArtistDisplayItem _artist;
        private ProgressRing _loadingIndicator;
        private TextBlock _emptyStateText;
        private TextBlock _artistNameText;
        private TextBlock _albumCountText;
        private Image _artistBackgroundImage;

        public ObservableCollection<Album> Albums { get; private set; }

        public ArtistAlbumsPage()
        {
            Albums = new ObservableCollection<Album>();
            InitializeComponent();
            DataContext = this;

            var sectionBar = FindName("SectionBar") as Mixdrome.Phone.Common.PhoneTitleBar;
            if (sectionBar != null) sectionBar.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(Mixdrome.Phone.Common.PhoneText.Get("UiMyMusic"));
            _loadingIndicator = FindName("LoadingIndicator") as ProgressRing;
            _emptyStateText = FindName("EmptyStateText") as TextBlock;
            _artistNameText = FindName("ArtistNameText") as TextBlock;
            _albumCountText = FindName("AlbumCountText") as TextBlock;
            _artistBackgroundImage = FindName("ArtistBackgroundImage") as Image;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _pageActive = true;
            HardwareButtons.BackPressed += OnHardwareBackPressed;
            _artist = e.Parameter as ArtistDisplayItem;
            try
            {
                _subsonicService = await PhoneSubsonicServiceFactory.CreateAsync();
                if (!_subsonicService.HasValidSubsonicUrl)
                    throw new InvalidOperationException(Mixdrome.Phone.Common.PhoneText.Get("ConfigureServer"));
                if (!_pageActive) return;

                if (_artistNameText != null) _artistNameText.Text = _artist != null ? _artist.Name : "artist";
                await LoadAlbums();
            }
            catch (Exception exception)
            {
                if (_pageActive && _emptyStateText != null)
                {
                    _emptyStateText.Text = exception.Message;
                    _emptyStateText.Visibility = Visibility.Visible;
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _pageActive = false;
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

        private async Task LoadAlbums()
        {
            if (_artist == null || string.IsNullOrEmpty(_artist.Id))
            {
                if (_emptyStateText != null) _emptyStateText.Visibility = Visibility.Visible;
                return;
            }

            if (_loadingIndicator != null) _loadingIndicator.IsActive = true;

            try
            {
                var result = _subsonicService.GetArtist(_artist.Id);
                await result.Execute();
                if (!_pageActive) return;
                if (result.Error != null) throw result.Error;

                Albums.Clear();
                if (result.Result != null && result.Result.Albums != null)
                {
                    string firstCover = null;
                    foreach (var album in result.Result.Albums)
                    {
                        album.CoverArt = _subsonicService.GetCoverArtForId(album.CoverArt);
                        if (firstCover == null)
                        {
                            firstCover = album.CoverArt;
                        }

                        Albums.Add(album);
                    }

                    SetImageSource(_artistBackgroundImage, firstCover);
                }

                if (_albumCountText != null) _albumCountText.Text = string.Format(Mixdrome.Phone.Common.PhoneText.Get("AlbumCount"), Albums.Count);
                if (_emptyStateText != null) _emptyStateText.Visibility = Albums.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (!_pageActive) return;
                if (_emptyStateText != null)
                {
                    _emptyStateText.Text = Mixdrome.Phone.Common.PhoneText.Get("UiFailedToLoadAlbums");
                    _emptyStateText.Visibility = Visibility.Visible;
                }

                await new MessageDialog(Mixdrome.Phone.Common.PhoneText.Get("UiFailedToLoadArtist") + ex.Message).ShowAsync();
            }
            finally
            {
                if (_loadingIndicator != null) _loadingIndicator.IsActive = false;
            }
        }

        private static void SetImageSource(Image image, string source)
        {
            if (image == null || string.IsNullOrEmpty(source))
            {
                return;
            }

            var uriKind = source.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                              ? UriKind.Absolute
                              : UriKind.Relative;
            image.Source = new BitmapImage(new Uri(source, uriKind));
        }

        private void OnAlbumClick(object sender, ItemClickEventArgs e)
        {
            var album = e.ClickedItem as Album;
            if (album != null)
            {
                Frame.Navigate(typeof(AlbumPage), album.Id);
            }
        }
    }
}
