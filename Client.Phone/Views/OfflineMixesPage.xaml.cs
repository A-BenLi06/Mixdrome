namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.Phone.UI.Input;
    using Windows.UI.Input;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.UI.Xaml.Navigation;

    public sealed class PhoneOfflineRequest
    {
        public SubsonicServiceConfiguration Configuration { get; set; }
        public PhoneMixDefinition Mix { get; set; }
    }

    public sealed partial class OfflineMixesPage : Page
    {
        private bool active;
        private CancellationTokenSource download;
        private PhoneOfflineCard pending;
        private int generation;
        private int selectionGeneration;
        private ItemsWrapGrid mixPanel;
        public ObservableCollection<PhoneOfflineCard> Cards { get; private set; } = new ObservableCollection<PhoneOfflineCard>();
        public OfflineMixesPage()
        {
            InitializeComponent(); DataContext = this;
            PageTitleBar.Title = CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiMixDrome"));
            PageTitle.Text = PhoneText.Get("OfflineMixesTitle"); HintText.Text = PhoneText.Get("OfflineMixesHint");
            CancelButton.Label = PhoneText.Get("CancelDownload");
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e); active = true; HardwareButtons.BackPressed += OnBackPressed;
            await Refresh();
            var request = e.Parameter as PhoneOfflineRequest;
            if (active && request != null && e.NavigationMode != NavigationMode.Back) await Download(request);
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            active = false; generation++; selectionGeneration++; if (download != null) download.Cancel();
            HardwareButtons.BackPressed -= OnBackPressed; base.OnNavigatedFrom(e);
        }
        private async Task Refresh()
        {
            var version = ++generation; selectionGeneration++;
            try
            {
                var saved = await PhoneOfflineMixes.GetSavedMixesAsync();
                if (!active || version != generation) return;
                Cards.Clear(); if (pending != null) Cards.Add(pending);
                foreach (var mix in saved) Cards.Add(new PhoneOfflineCard { Saved = mix, Name = mix.Name, Artwork = mix.Artwork, IsPlayMe = mix.IsPlayMe });
                ResizeCards(); HintText.Text = PhoneText.Get(pending != null ? "OfflineDownloadingHint" : "OfflineMixesHint");
                SetStatus(saved.Count == 0 && pending == null ? PhoneText.Get("NoOfflineMixes") : string.Empty);
            }
            catch (OperationCanceledException) { if (active && version == generation) SetStatus(PhoneText.Get("OfflineAccountChanged")); }
            catch (Exception exception) { if (active && version == generation) SetStatus(PhoneText.Format("LoadFailed", exception.Message)); }
        }
        private async Task Download(PhoneOfflineRequest request)
        {
            if (download != null || request.Mix == null) return;
            var cancellation = new CancellationTokenSource(); download = cancellation;
            var api = new NavidromeApi(request.Configuration);
            var artwork = request.Mix.Songs == null || request.Mix.Songs.Count == 0 ? PhoneCollectionPresentation.DefaultArtwork :
                PhoneCollectionPresentation.Artwork(api, request.Mix.Songs[0].CoverArt);
            pending = new PhoneOfflineCard { Name = request.Mix.Name, Artwork = artwork, IsPlayMe = request.Mix.IsPlayMe, IsDownloading = true };
            Cards.Insert(0, pending); ResizeCards(); HintText.Text = PhoneText.Get("OfflineDownloadingHint");
            SetStatus(string.Empty); CancelButton.Visibility = Visibility.Visible;
            var card = pending;
            try
            {
                await PhoneOfflineMixes.SaveMixAsync(request.Configuration, request.Mix,
                    new Progress<PhoneOfflineProgress>(value => { if (active && pending == card) card.Report(value); }), cancellation.Token);
                if (active) SetStatus(PhoneText.Get("OfflineDownloadComplete"));
            }
            catch (OperationCanceledException) { if (active) SetStatus(PhoneText.Get("OfflineDownloadCancelled")); }
            catch (Exception exception) { if (active) SetStatus(PhoneText.Format("OfflineDownloadFailed", exception.Message)); }
            finally
            {
                pending = null; download = null; cancellation.Dispose();
                if (active)
                {
                    var result = StatusText.Text; CancelButton.Visibility = Visibility.Collapsed;
                    HintText.Text = PhoneText.Get("OfflineMixesHint");
                    await Refresh(); SetStatus(result);
                }
            }
        }
        private async void OnMixClick(object sender, ItemClickEventArgs e)
        {
            await OpenMixAsync(e.ClickedItem as PhoneOfflineCard);
        }
        private bool CurrentSelection(int version, int selection)
        {
            return active && generation == version && selectionGeneration == selection;
        }
        private async Task OpenMixAsync(PhoneOfflineCard card)
        {
            var selection = ++selectionGeneration;
            var version = generation;
            if (!active || card == null || card.Saved == null || card.IsDownloading || !Cards.Contains(card)) return;
            try
            {
                var queue = card.Saved.CreateQueue();
                var valid = await PhoneOfflineMixes.ValidateLocalQueueAsync(queue);
                if (!CurrentSelection(version, selection)) return;
                if (!valid)
                {
                    // Refresh invalidates pending selections before its first await.
                    // Retain those new versions so a later click can supersede the
                    // missing-file message while the refreshed list is being read.
                    var refresh = Refresh();
                    var refreshVersion = generation;
                    var refreshSelection = selectionGeneration;
                    await refresh;
                    if (CurrentSelection(refreshVersion, refreshSelection)) SetStatus(PhoneText.Get("OfflineFileMissing"));
                    return;
                }
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter { Playlist = queue, StartIndex = 0,
                    IsMix = true, MixTitle = card.Saved.IsPlayMe ? null : card.Name });
            }
            catch (Exception exception) { if (CurrentSelection(version, selection)) SetStatus(PhoneText.Format("LoadFailed", exception.Message)); }
        }
        private async void OnMixHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != HoldingState.Started) return;
            var element = e.OriginalSource as FrameworkElement;
            var card = element == null ? null : element.DataContext as PhoneOfflineCard;
            if (card == null || card.Saved == null) return;
            e.Handled = true;
            var menu = new PopupMenu();
            menu.Commands.Add(new UICommand(PhoneText.Get("RemoveOfflineMix"), async command =>
            {
                try
                {
                    // Detach bound cover art and remove the tile so WP8.1 releases
                    // any read handle on cached files before folder deletion.
                    card.Artwork = null;
                    Cards.Remove(card);
                    await Task.Yield();
                    await PhoneOfflineMixes.DeleteMixAsync(card.Saved);
                    if (active) await Refresh();
                }
                catch (Exception exception)
                {
                    if (active)
                    {
                        await Refresh();
                        SetStatus(PhoneText.Format("LoadFailed", exception.Message));
                    }
                }
            }));
            await menu.ShowAsync(e.GetPosition(null));
        }
        private void OnGridSizeChanged(object sender, SizeChangedEventArgs e) { ResizeCards(); }
        private void OnPanelLoaded(object sender, RoutedEventArgs e) { mixPanel = sender as ItemsWrapGrid; ResizeCards(); }
        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) { mixPanel = sender as ItemsWrapGrid; ResizeCards(); }
        private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var scale = e.NewSize.Width / 480;
            if (scale <= 0) return;
            HeadingPanel.Margin = new Thickness(24 * scale, 5 * scale, 24 * scale, 12 * scale);
            PageTitle.FontSize = 72 * scale; PageTitle.Margin = new Thickness(-3 * scale, 0, 0, 16 * scale);
            HintText.FontSize = 21 * scale; HintText.LineHeight = 26 * scale; HintText.MinHeight = 52 * scale;
            MixGrid.Margin = new Thickness(24 * scale, 0, 6 * scale, 0);
            ResizeCards();
        }
        private void ResizeCards()
        {
            var scale = LayoutRoot.ActualWidth / 480;
            if (scale <= 0) scale = 1;
            var viewport = MixGrid.ActualWidth;
            for (DependencyObject parent = mixPanel; parent != null && !ReferenceEquals(parent, MixGrid); parent = VisualTreeHelper.GetParent(parent))
            {
                var scroll = parent as ScrollViewer;
                if (scroll != null && scroll.ViewportWidth > 0) { viewport = scroll.ViewportWidth; break; }
            }
            var cellWidth = Math.Floor((viewport > 0 ? viewport : 450 * scale) / 2);
            if (cellWidth <= 0) return;
            if (mixPanel != null) mixPanel.ItemWidth = cellWidth;
            var cardScale = cellWidth / 225;
            foreach (var card in Cards) card.Resize(207 * cardScale, cardScale, scale);
        }
        private void OnArtworkFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null) image.Source = new BitmapImage(new Uri(PhoneCollectionPresentation.DefaultArtwork));
        }
        private void OnCancelClick(object sender, RoutedEventArgs e) { if (download != null) download.Cancel(); }
        private void SetStatus(string message)
        {
            StatusText.Text = message ?? string.Empty;
            StatusText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        }
        private async void OnRefreshClick(object sender, RoutedEventArgs e) { await Refresh(); }
        private void OnBackPressed(object sender, BackPressedEventArgs e)
        { if (!e.Handled && Frame.CanGoBack) { e.Handled = true; Frame.GoBack(); } }
    }

    public sealed class PhoneOfflineCard : INotifyPropertyChanged
    {
        private string artwork;
        private double size;
        public PhoneSavedMix Saved { get; set; }
        public string Name { get; set; }
        public string Artwork { get { return artwork; } set { artwork = value; Changed("Artwork"); } }
        public bool IsPlayMe { get; set; }
        public bool IsDownloading { get; set; }
        public double Size { get { return size; } set { size = value; Changed("Size"); } }
        public double Progress { get; private set; }
        public double CaptionHeight { get; private set; }
        public double CaptionSize { get; private set; }
        public double CaptionLineHeight { get; private set; }
        public double CaptionMaxHeight { get; private set; }
        public double StatusFontSize { get; private set; }
        public double ProgressBarHeight { get; private set; }
        public Thickness LabelMargin { get; private set; }
        public Thickness Spacing { get; private set; }
        public bool IsIndeterminate { get; private set; }
        public string Status { get; private set; }
        public Visibility PlayMeVisibility { get { return IsPlayMe ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility LabelVisibility { get { return IsPlayMe ? Visibility.Collapsed : Visibility.Visible; } }
        public Visibility ProgressVisibility { get { return IsDownloading ? Visibility.Visible : Visibility.Collapsed; } }
        public event PropertyChangedEventHandler PropertyChanged;
        public void Resize(double width, double cardScale, double typographyScale = 0)
        {
            var typo = typographyScale > 0 ? typographyScale : cardScale;
            Size = width; CaptionHeight = 60 * cardScale; CaptionSize = 21 * typo; CaptionLineHeight = 22 * typo; CaptionMaxHeight = 44 * typo;
            Spacing = new Thickness(0, 0, 18 * cardScale, 18 * cardScale);
            StatusFontSize = 20 * typo; ProgressBarHeight = 4 * cardScale; LabelMargin = new Thickness(12 * cardScale, 0, 12 * cardScale, 0);
            Changed("CaptionHeight"); Changed("CaptionSize"); Changed("CaptionLineHeight"); Changed("CaptionMaxHeight"); Changed("Spacing");
            Changed("StatusFontSize"); Changed("ProgressBarHeight"); Changed("LabelMargin");
        }
        public void Report(PhoneOfflineProgress value)
        {
            var fraction = value.BytesTotal.HasValue && value.BytesTotal.Value > 0 ? Math.Min(1, (double)value.Received / value.BytesTotal.Value) : 0;
            Progress = value.Total <= 0 ? 0 : 100 * (value.Completed + fraction) / value.Total;
            IsIndeterminate = value.Received > 0 && !value.BytesTotal.HasValue;
            Status = PhoneText.Format("OfflineDownloadProgress", value.Completed, value.Total);
            Changed("Progress"); Changed("IsIndeterminate"); Changed("Status");
        }
        private void Changed(string property) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(property)); }
    }
}
