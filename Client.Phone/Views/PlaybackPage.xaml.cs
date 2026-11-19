namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.UI.ViewManagement;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.Foundation.Collections;
    using Windows.Media.Playback;
    using Windows.Phone.UI.Input;
    using Windows.Storage;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Automation;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.UI.Xaml.Navigation;

    public sealed class PlaybackNavigationParameter
    {
        public List<SongDisplayItem> Playlist { get; set; }
        public int StartIndex { get; set; }
        public bool? Shuffle { get; set; }
        public bool IsMix { get; set; }
        public string MixTitle { get; set; }
    }

    public sealed partial class PlaybackPage : Page
    {
        private const string DefaultArtworkUri = "ms-appx:///Assets/MixRadio/DefaultArtwork300.png";
        private readonly DispatcherTimer progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private TaskCompletionSource<bool> ready;
        private CancellationTokenSource activation;
        private bool active;
        private bool updatingProgress;
        private bool seeking;
        private uint seekPointer;
        private bool shuffle;
        private int repeat;
        private int currentIndex = -1;
        private string queueVersion = string.Empty;
        private string latestVersion = string.Empty;
        private MediaPlayer player;
        private readonly AudioStateVersionGate stateGate = new AudioStateVersionGate();
        private bool canNext;
        private bool playbackFailed;
        private TrackInfo displayedTrack;
        private BitmapImage artworkImage;
        private ItemCollection observedQueueItems;
        private bool queueListLoaded;
        private bool isMix;
        private bool showingMixDetails;
        private bool feedbackBusy;
        private bool offlineRequestBusy;
        private double closedCommandBarHeight;
        private bool feedbackAvailable;
        private bool songStarred;
        private int songRating;
        private BitmapImage artistImage;
        private BitmapImage pendingArtistImage;
        private TrackInfo pendingArtistTrack;
        private CancellationTokenSource pendingArtistActivation;
        private string pendingArtistId;
        private string loadedArtistId;
        private PhoneReportingProfile loadedArtistAccount;
        private DataTransferManager shareManager;
        public ObservableCollection<TrackInfo> QueueItems { get; private set; } = new ObservableCollection<TrackInfo>();

        public PlaybackPage()
        {
            InitializeComponent();
            DataContext = this;
            progressTimer.Tick += OnProgressTimerTick;
            ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnSeekPressed), true);
            ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnSeekReleased), true);
            ProgressSlider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnSeekReleased), true);
            NowPlayingMenu.Label = PhoneText.Get("UiNowPlaying");
            AboutMenu.Label = PhoneText.Get("UiAbout");
            QueueMenu.Label = PhoneText.Get("UiQueue");
            SaveOfflineMixMenu.Label = PhoneText.Get("SaveOfflineMix");
            FeedbackOkButton.Content = PhoneText.Get("UiOk");
            AutomationProperties.SetName(ShareButton, PhoneText.Get("ShareSong"));
            AutomationProperties.SetName(LyricsButton, PhoneText.Get("Lyrics"));
            AutomationProperties.SetName(CloseLyricsButton, PhoneText.Get("Lyrics"));
            AutomationProperties.SetName(LikeButton, PhoneText.Get("LikeSong"));
            AutomationProperties.SetName(DislikeButton, PhoneText.Get("DislikeSong"));
            AutomationProperties.SetName(FavouriteButton, PhoneText.Get("FavouriteSong"));
            ResetDisplay();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs args)
        {
            base.OnNavigatedTo(args);
            active = true;
            displayedTrack = null;
            feedbackBusy = false;
            offlineRequestBusy = false;
            activation = new CancellationTokenSource();
            var token = activation.Token;
            ready = new TaskCompletionSource<bool>();
            HardwareButtons.BackPressed += OnBackPressed;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
            // Subscribe before creating the player, which may start a cold audio task.
            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessage;
            try
            {
                player = BackgroundMediaPlayer.Current;
                player.CurrentStateChanged += OnMediaStateChanged;
                Send(BackgroundAudioMessages.RequestState, true);
                var timeout = Task.Delay(TimeSpan.FromSeconds(10), token);
                if (await Task.WhenAny(ready.Task, timeout) != ready.Task)
                {
                    token.ThrowIfCancellationRequested();
                    PlaybackStatusText.Text = PhoneText.Get("PlaybackUnavailable");
                    return;
                }
                token.ThrowIfCancellationRequested();
                // A failed initial source/state update must not prevent later
                // native progress observations from recovering this activation.
                progressTimer.Start();
                // Returning from another page reconnects; it never replays the navigation queue.
                if (args.NavigationMode != NavigationMode.Back)
                {
                    var parameter = args.Parameter as PlaybackNavigationParameter;
                    if (parameter != null) await StartQueueAsync(parameter, token);
                    else
                    {
                        var id = args.Parameter as string;
                        if (!string.IsNullOrEmpty(id))
                            await StartQueueAsync(new PlaybackNavigationParameter
                            {
                                Playlist = new List<SongDisplayItem> { new SongDisplayItem { Id = id } }, StartIndex = 0
                            }, token);
                    }
                }
                token.ThrowIfCancellationRequested();
                UpdatePlaybackState();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.WriteLine("Phone playback failed: " + exception.GetType().Name);
                if (active) PlaybackStatusText.Text = PhoneText.Get("PlaybackFailed");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs args)
        {
            active = false;
            seeking = false;
            if (activation != null) { activation.Cancel(); activation.Dispose(); activation = null; }
            HardwareButtons.BackPressed -= OnBackPressed;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged -= OnVisibleBoundsChanged;
            BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessage;
            if (player != null) player.CurrentStateChanged -= OnMediaStateChanged;
            progressTimer.Stop();
            DetachQueueItems();
            DetachArtwork();
            DetachArtistImage();
            if (shareManager != null) { shareManager.DataRequested -= OnShareDataRequested; shareManager = null; }
            LyricsOverlay.Visibility = Visibility.Collapsed;
            FeedbackOverlay.Visibility = Visibility.Collapsed;
            PlayerCommandBar.IsEnabled = true;
            base.OnNavigatedFrom(args);
        }

        private void OnBackPressed(object sender, BackPressedEventArgs args)
        {
            if (args.Handled) return;
            if (FeedbackOverlay.Visibility == Visibility.Visible)
            { args.Handled = true; FeedbackOverlay.Visibility = Visibility.Collapsed; PlayerCommandBar.IsEnabled = true; return; }
            if (LyricsOverlay.Visibility == Visibility.Visible)
            { args.Handled = true; LyricsOverlay.Visibility = Visibility.Collapsed; return; }
            if (isMix && showingMixDetails)
            { args.Handled = true; ShowMixDetails(false, 0); return; }
            if (Frame.CanGoBack) { args.Handled = true; Frame.GoBack(); }
        }

        private async Task StartQueueAsync(PlaybackNavigationParameter parameter, CancellationToken token)
        {
            if (parameter.Playlist == null || parameter.Playlist.Count == 0
                || parameter.StartIndex < 0 || parameter.StartIndex >= parameter.Playlist.Count)
            {
                PlaybackStatusText.Text = PhoneText.Get("PlaybackEmpty");
                return;
            }
            var localQueue = true;
            var containsLocalTrack = false;
            foreach (var song in parameter.Playlist)
            {
                Uri stream;
                if (song == null || !Uri.TryCreate(song.StreamUrl, UriKind.Absolute, out stream)
                    || stream.Scheme != "ms-appdata") localQueue = false;
                else containsLocalTrack = true;
            }
            if (containsLocalTrack && !localQueue) throw new InvalidOperationException("Local media requires a validated offline queue.");
            WP8SubsonicService service;
            if (localQueue) service = await CreateLocalQueueServiceAsync(parameter.Playlist);
            else service = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync();
            token.ThrowIfCancellationRequested();
            var tracks = new List<TrackInfo>();
            foreach (var song in parameter.Playlist)
            {
                if (song == null) throw new InvalidOperationException("Missing queue item.");
                tracks.Add(new TrackInfo
                {
                    Id = song.Id,
                    Title = string.IsNullOrEmpty(song.Title) ? PhoneText.Get("UnknownTrack") : song.Title,
                    Artist = string.IsNullOrEmpty(song.Artist) ? PhoneText.Get("UnknownArtist") : song.Artist,
                    Album = song.Album,
                    Duration = song.Duration,
                    IsMix = parameter.IsMix,
                    MixTitle = parameter.MixTitle,
                    CoverArt = ResolveArtwork(song.CoverArt, service),
                    StreamUrl = string.IsNullOrEmpty(song.StreamUrl) ? service.GetStreamUri(song.Id).ToString() : song.StreamUrl,
                    // Explicit external streams are radio, not catalog song IDs.
                    Reporting = localQueue || string.IsNullOrEmpty(song.StreamUrl) ? service.CreateReportingProfile() : null
                });
            }
            var validation = new AudioQueueState();
            if (!validation.Replace(tracks, parameter.StartIndex)) throw new InvalidOperationException("Invalid playback queue.");
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                BackgroundAudioMessages.TransferFilePrefix + Guid.NewGuid().ToString("N") + ".json", CreationCollisionOption.FailIfExists);
            var sent = false;
            try
            {
                await FileIO.WriteTextAsync(file, AudioJson.Serialize(tracks));
                token.ThrowIfCancellationRequested();
                var command = new ValueSet
                {
                    { BackgroundAudioMessages.ReplaceQueue, true },
                    { BackgroundAudioMessages.QueueFile, file.Name },
                    { BackgroundAudioMessages.CurrentIndex, parameter.StartIndex }
                };
                if (parameter.Shuffle.HasValue) command.Add(BackgroundAudioMessages.Shuffle, parameter.Shuffle.Value);
                BackgroundMediaPlayer.SendMessageToBackground(command);
                sent = true;
            }
            finally { if (!sent) await file.DeleteAsync(); }
        }

        private static async Task<WP8SubsonicService> CreateLocalQueueServiceAsync(List<SongDisplayItem> songs)
        {
            // Preserve the validated cache's source route for listening reports;
            // the currently preferred route may refer to a different library.
            var configuration = await PhoneOfflineMixes.ResolveLocalQueueConfigurationAsync(songs);
            if (configuration == null) throw new InvalidOperationException("The offline playback queue is no longer available.");
            var service = new WP8SubsonicService();
            service.Configure(configuration);
            return service;
        }

        private static string ResolveArtwork(string artwork, WP8SubsonicService service)
        {
            Uri uri;
            if (string.IsNullOrEmpty(artwork)) return "ms-appx:///Assets/MixRadio/DefaultArtwork300.png";
            // Cover IDs are opaque and can contain a colon. Parsing alone also
            // accepts them as custom URI schemes, which BitmapImage cannot load.
            if (Uri.TryCreate(artwork, UriKind.Absolute, out uri)
                && (uri.Scheme == "http" || uri.Scheme == "https"
                    || uri.Scheme == "ms-appx" || uri.Scheme == "ms-appdata")) return uri.ToString();
            return service.GetCoverArtUrl(artwork);
        }

        private async void OnMessage(object sender, MediaPlayerDataReceivedEventArgs args)
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (!active) return;
                    try { await ApplyStateAsync(args.Data); }
                    catch (Exception exception)
                    {
                        Debug.WriteLine("Phone audio state failed: " + exception.GetType().Name);
                        if (active) PlaybackStatusText.Text = PhoneText.Get("PlaybackFailed");
                    }
                });
            }
            catch (Exception exception) { Debug.WriteLine("Phone audio dispatcher unavailable: " + exception.GetType().Name); }
        }

        private async Task ApplyStateAsync(ValueSet data)
        {
            var stateSession = data.ContainsKey(BackgroundAudioMessages.Session) ? data[BackgroundAudioMessages.Session] as string : string.Empty;
            var stateSequence = data.ContainsKey(BackgroundAudioMessages.Sequence) && data[BackgroundAudioMessages.Sequence] is int
                ? (int)data[BackgroundAudioMessages.Sequence] : -1;
            if (!stateGate.Accept(stateSession, stateSequence)) return;
            if (data.ContainsKey(BackgroundAudioMessages.Ready) || data.ContainsKey(BackgroundAudioMessages.Snapshot))
                ready.TrySetResult(true);
            var failed = data.ContainsKey(BackgroundAudioMessages.Failure);
            var stateVersion = data.ContainsKey(BackgroundAudioMessages.QueueVersion)
                ? data[BackgroundAudioMessages.QueueVersion] as string ?? string.Empty : string.Empty;
            var replaced = stateVersion != queueVersion;
            latestVersion = stateVersion;
            var currentActivation = activation;
            if (!string.IsNullOrEmpty(stateVersion) && stateVersion != queueVersion)
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(BackgroundAudioMessages.SessionFile);
                var snapshot = AudioJson.Deserialize<AudioQueueSnapshot>(await FileIO.ReadTextAsync(file));
                if (!active || activation != currentActivation || latestVersion != stateVersion || !stateGate.IsCurrent(stateSession, stateSequence)) return;
                // The file is replaced atomically. Reject a newer queue read for an older message.
                if (snapshot.Version != stateVersion) { Send(BackgroundAudioMessages.RequestState, true); return; }
                QueueItems.Clear();
                foreach (var track in snapshot.Tracks) QueueItems.Add(track);
                queueVersion = stateVersion;
            }
            if (!active || activation != currentActivation || latestVersion != stateVersion || !stateGate.IsCurrent(stateSession, stateSequence)) return;
            if (string.IsNullOrEmpty(stateVersion)) { QueueItems.Clear(); queueVersion = string.Empty; }
            if (failed) playbackFailed = true;
            else if (data.ContainsKey(BackgroundAudioMessages.TrackChanged) || replaced) playbackFailed = false;
            canNext = data.ContainsKey(BackgroundAudioMessages.CanNext) && data[BackgroundAudioMessages.CanNext] is bool
                && (bool)data[BackgroundAudioMessages.CanNext];
            currentIndex = data.ContainsKey(BackgroundAudioMessages.CurrentIndex) && data[BackgroundAudioMessages.CurrentIndex] is int
                ? (int)data[BackgroundAudioMessages.CurrentIndex] : -1;
            shuffle = data.ContainsKey(BackgroundAudioMessages.Shuffle) && data[BackgroundAudioMessages.Shuffle] is bool
                && (bool)data[BackgroundAudioMessages.Shuffle];
            repeat = data.ContainsKey(BackgroundAudioMessages.Repeat) && data[BackgroundAudioMessages.Repeat] is int
                ? (int)data[BackgroundAudioMessages.Repeat] : 0;
            if (currentIndex >= 0 && currentIndex < QueueItems.Count)
            {
                ShowTrack(QueueItems[currentIndex]);
            }
            else ResetDisplay();
            UpdatePlaybackState();
            SynchronizeQueueSelection();
        }

        private void OnQueueListLoaded(object sender, RoutedEventArgs args)
        {
            DetachQueueItems();
            if (!active) return;
            queueListLoaded = true;
            observedQueueItems = QueueListView.Items;
            if (observedQueueItems != null) observedQueueItems.VectorChanged += OnQueueItemsChanged;
            SynchronizeQueueSelection();
        }

        private void OnQueueListUnloaded(object sender, RoutedEventArgs args)
        {
            DetachQueueItems();
        }

        private void DetachQueueItems()
        {
            queueListLoaded = false;
            if (observedQueueItems != null) observedQueueItems.VectorChanged -= OnQueueItemsChanged;
            observedQueueItems = null;
        }

        private void OnQueueItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs args)
        {
            SynchronizeQueueSelection();
        }

        private void SynchronizeQueueSelection()
        {
            // Pivot content can receive queue state before its ItemsSource binding
            // has populated Items. Select only the matching displayed occurrence;
            // duplicate media IDs do not identify a position in the queue.
            if (!active || !queueListLoaded || currentIndex < 0 || currentIndex >= QueueItems.Count) return;
            var items = QueueListView.Items;
            if (items == null || currentIndex >= items.Count || !ReferenceEquals(items[currentIndex], QueueItems[currentIndex])) return;
            if (QueueListView.SelectedIndex != currentIndex) QueueListView.SelectedIndex = currentIndex;
        }

        private void ShowTrack(TrackInfo track)
        {
            if (ReferenceEquals(displayedTrack, track)) return;
            displayedTrack = track;
            BeginTrackPresentation(track);
            SongTitleText.Text = AboutTitleText.Text = track.Title ?? PhoneText.Get("UnknownTrack");
            ArtistText.Text = AboutArtistText.Text = track.Artist ?? PhoneText.Get("UnknownArtist");
            AboutAlbumText.Text = string.IsNullOrEmpty(track.Album) ? PhoneText.Get("UnknownAlbum") : track.Album;
            DetachArtwork();
            var source = string.IsNullOrEmpty(track.CoverArt) ? DefaultArtworkUri : track.CoverArt;
            var image = new BitmapImage();
            if (!string.Equals(source, DefaultArtworkUri, StringComparison.Ordinal))
            {
                artworkImage = image;
                image.ImageFailed += OnArtworkImageFailed;
            }
            AlbumArtImage.Source = image;
            MixAlbumArtImage.Source = image;
            image.UriSource = new Uri(source, UriKind.Absolute);
        }

        private async void BeginTrackPresentation(TrackInfo track)
        {
            isMix = track.IsMix;
            LyricsOverlay.Visibility = Visibility.Collapsed;
            LyricsText.Text = string.Empty;
            showingMixDetails = false;
            MixSongTitleText.Text = track.Title ?? PhoneText.Get("UnknownTrack");
            MixArtistText.Text = track.Artist ?? PhoneText.Get("UnknownArtist");
            PlayerTitleBar.Title = isMix
                ? (string.IsNullOrEmpty(track.MixTitle) ? PhoneText.Get("PlayMeTitle") : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(track.MixTitle))
                : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiMyMusic"));
            UpdatePlayerLayout();
            feedbackAvailable = false;
            songRating = 0;
            songStarred = false;
            UpdateFeedbackControls();
            DetachPendingArtistImage();
            if (!MatchesPlaybackAccount(track.Reporting, loadedArtistAccount)) ClearArtistBackground();
            var currentActivation = activation;
            if (track.Reporting == null || string.IsNullOrEmpty(track.Id)) return;
            try
            {
                using (var service = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync())
                {
                    if (!IsCurrentPresentation(track, currentActivation) || !MatchesPlaybackAccount(track.Reporting, service.CreateReportingProfile())) return;
                    var song = await service.GetSongAsync(track.Id);
                    if (!IsCurrentPresentation(track, currentActivation)) return;
                    songRating = song.UserRating;
                    songStarred = !string.IsNullOrEmpty(song.Starred);
                    feedbackAvailable = true;
                    UpdateFeedbackControls();
                    if (string.IsNullOrEmpty(song.ArtistId)) { ClearArtistBackground(); return; }
                    if (song.ArtistId == loadedArtistId && BackgroundImage.Source != null
                        && MatchesPlaybackAccount(track.Reporting, loadedArtistAccount)) return;
                    var artist = await service.GetArtistInfoAsync(song.ArtistId);
                    if (!IsCurrentPresentation(track, currentActivation)) return;
                    Uri imageUri;
                    if (artist == null || !Uri.TryCreate(artist.ImageUrl, UriKind.Absolute, out imageUri)
                        || (imageUri.Scheme != "http" && imageUri.Scheme != "https")) { ClearArtistBackground(); return; }
                    var image = new BitmapImage();
                    pendingArtistImage = image; pendingArtistTrack = track;
                    pendingArtistActivation = currentActivation; pendingArtistId = song.ArtistId;
                    image.ImageOpened += OnArtistImageOpened;
                    image.ImageFailed += OnPendingArtistImageFailed;
                    BackgroundPreloadImage.Source = image;
                    image.UriSource = imageUri;
                }
            }
            catch (Exception exception)
            {
                // Metadata/image enrichment never changes the actual audio state.
                Debug.WriteLine("Phone player enrichment unavailable: " + exception.GetType().Name);
            }
        }

        private bool IsCurrentPresentation(TrackInfo track, CancellationTokenSource currentActivation)
        {
            return active && activation == currentActivation && ReferenceEquals(displayedTrack, track);
        }

        private static bool MatchesPlaybackAccount(PhoneReportingProfile playback, PhoneReportingProfile current)
        {
            Uri playbackUri, currentUri;
            if (playback == null || current == null
                || !Uri.TryCreate(playback.BaseUrl, UriKind.Absolute, out playbackUri)
                || !Uri.TryCreate(current.BaseUrl, UriKind.Absolute, out currentUri)
                || (playbackUri.Scheme != "http" && playbackUri.Scheme != "https")
                || (currentUri.Scheme != "http" && currentUri.Scheme != "https")) return false;
            var playbackIdentity = AccountIdentity.For(new SubsonicServiceConfiguration
            { BaseUrl = playbackUri.AbsoluteUri, Username = playback.Username });
            var currentIdentity = AccountIdentity.For(new SubsonicServiceConfiguration
            { BaseUrl = currentUri.AbsoluteUri, Username = current.Username });
            return !string.IsNullOrEmpty(playbackIdentity) && !string.IsNullOrEmpty(currentIdentity)
                && string.Equals(playbackIdentity, currentIdentity, StringComparison.Ordinal)
                && string.Equals(playback.AuthenticationMode, current.AuthenticationMode, StringComparison.Ordinal);
        }

        private void OnArtistImageFailed(object sender, ExceptionRoutedEventArgs args)
        {
            if (!active || !ReferenceEquals(sender, artistImage)) return;
            ClearArtistBackground();
        }

        private void OnArtistImageOpened(object sender, RoutedEventArgs args)
        {
            var image = sender as BitmapImage;
            if (image == null || !ReferenceEquals(image, pendingArtistImage)) return;
            var track = pendingArtistTrack; var id = pendingArtistId; var currentActivation = pendingArtistActivation;
            if (!IsCurrentPresentation(track, currentActivation)) { DetachPendingArtistImage(); return; }
            DetachArtistImage();
            artistImage = image; artistImage.ImageFailed += OnArtistImageFailed;
            loadedArtistId = id; loadedArtistAccount = track.Reporting;
            BackgroundImage.Source = image;
        }

        private void OnPendingArtistImageFailed(object sender, ExceptionRoutedEventArgs args)
        {
            if (!ReferenceEquals(sender, pendingArtistImage)) return;
            var current = IsCurrentPresentation(pendingArtistTrack, pendingArtistActivation);
            DetachPendingArtistImage();
            if (current) ClearArtistBackground();
        }

        private void DetachPendingArtistImage()
        {
            if (pendingArtistImage != null)
            {
                pendingArtistImage.ImageOpened -= OnArtistImageOpened;
                pendingArtistImage.ImageFailed -= OnPendingArtistImageFailed;
            }
            pendingArtistImage = null; pendingArtistTrack = null;
            pendingArtistActivation = null; pendingArtistId = null;
            BackgroundPreloadImage.Source = null;
        }

        private void ClearArtistBackground()
        {
            DetachArtistImage(); BackgroundImage.Source = null;
            loadedArtistId = null; loadedArtistAccount = null;
        }

        private void DetachArtistImage()
        {
            DetachPendingArtistImage();
            if (artistImage != null) artistImage.ImageFailed -= OnArtistImageFailed;
            artistImage = null;
        }

        private void UpdateFeedbackControls()
        {
            var enabled = displayedTrack != null && feedbackAvailable && !feedbackBusy;
            LikeButton.IsEnabled = FavouriteButton.IsEnabled = enabled;
            ShareButton.IsEnabled = displayedTrack != null;
            LyricsButton.IsEnabled = displayedTrack != null && feedbackAvailable;
            DislikeButton.IsEnabled = enabled && canNext;
            var accent = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["MixRadioAccentBrush"];
            var neutral = (Windows.UI.Xaml.Media.Brush)Resources["PlayerFeedbackNeutralBrush"];
            LikeButton.Background = songRating == 5 ? accent : neutral;
            DislikeButton.Background = songRating == 1 ? accent : neutral;
            FavouriteButton.Background = songStarred ? accent : neutral;
            UpdateOfflineMixCommand();
        }

        private bool CanSaveOfflineMix()
        {
            if (!active || !isMix || displayedTrack == null || !string.IsNullOrEmpty(displayedTrack.MixTitle)
                || string.IsNullOrEmpty(queueVersion) || QueueItems.Count == 0) return false;
            var account = displayedTrack.Reporting;
            foreach (var track in QueueItems)
            {
                Uri stream;
                if (track == null || string.IsNullOrWhiteSpace(track.Id) || !track.IsMix || !string.IsNullOrEmpty(track.MixTitle)
                    || !MatchesPlaybackAccount(account, track.Reporting)
                    || !Uri.TryCreate(track.StreamUrl, UriKind.Absolute, out stream)
                    || (stream.Scheme != "http" && stream.Scheme != "https")) return false;
            }
            return true;
        }

        private void UpdateOfflineMixCommand()
        {
            var eligible = CanSaveOfflineMix();
            SaveOfflineMixMenu.Visibility = eligible ? Visibility.Visible : Visibility.Collapsed;
            SaveOfflineMixMenu.IsEnabled = eligible && !offlineRequestBusy;
        }

        private async void OnSaveOfflineMixClick(object sender, RoutedEventArgs args) { await StartOfflineMixAsync(); }
        private async Task StartOfflineMixAsync()
        {
            if (offlineRequestBusy || !CanSaveOfflineMix()) return;
            var currentActivation = activation;
            var version = queueVersion;
            var account = displayedTrack.Reporting;
            var tracks = new List<TrackInfo>(QueueItems);
            offlineRequestBusy = true; UpdateOfflineMixCommand();
            try
            {
                var configuration = await PhoneSubsonicServiceFactory.LoadConfigurationAsync();
                if (!active || activation != currentActivation || queueVersion != version || !CanSaveOfflineMix()) return;
                using (var service = new WP8SubsonicService())
                {
                    service.Configure(configuration);
                    if (!MatchesPlaybackAccount(account, service.CreateReportingProfile()))
                    { PlaybackStatusText.Text = PhoneText.Get("OfflineAccountChanged"); return; }
                }
                // Preserve the full queue's occurrences; download deduplication
                // belongs to the cache, whose manifest keeps playback order.
                if (QueueItems.Count != tracks.Count) return;
                var songs = new List<Song>();
                for (var index = 0; index < tracks.Count; index++)
                {
                    if (!ReferenceEquals(QueueItems[index], tracks[index])) return;
                    var track = tracks[index];
                    songs.Add(new Song { Id = track.Id, Title = track.Title, Artist = track.Artist, Album = track.Album,
                        CoverArt = track.CoverArt, Duration = track.Duration });
                }
                PlayerCommandBar.IsOpen = false;
                Frame.Navigate(typeof(OfflineMixesPage), new PhoneOfflineRequest
                {
                    Configuration = configuration,
                    Mix = new PhoneMixDefinition { Id = "playme", Name = PhoneText.Get("UiPlayMe"), IsPlayMe = true, Songs = songs }
                });
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Phone player offline request failed: " + exception.GetType().Name);
                if (active && activation == currentActivation && queueVersion == version)
                    PlaybackStatusText.Text = PhoneText.Format("OfflineDownloadFailed", exception.Message);
            }
            finally
            {
                if (active && activation == currentActivation) { offlineRequestBusy = false; UpdateOfflineMixCommand(); }
            }
        }

        private async Task SaveFeedbackAsync(bool favourite, int rating)
        {
            var track = displayedTrack;
            var currentActivation = activation;
            if (track == null || !feedbackAvailable || feedbackBusy || !active) return;
            feedbackBusy = true;
            UpdateFeedbackControls();
            try
            {
                using (var service = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync())
                {
                    if (!IsCurrentPresentation(track, currentActivation) || !MatchesPlaybackAccount(track.Reporting, service.CreateReportingProfile())) return;
                    if (favourite) await service.SetSongStarredAsync(track.Id, !songStarred);
                    else await service.SetSongRatingAsync(track.Id, rating);
                    if (!IsCurrentPresentation(track, currentActivation)) return;
                    if (favourite) songStarred = !songStarred;
                    else
                    {
                        songRating = rating;
                        FeedbackTitleText.Text = PhoneText.Get(rating == 5 ? "LikedSongTitle" : "DislikedSongTitle");
                        FeedbackMessageText.Text = PhoneText.Get(rating == 5 ? "LikedSongMessage" : "DislikedSongMessage");
                        FeedbackIcon.RenderTransform = new Windows.UI.Xaml.Media.RotateTransform { Angle = rating == 5 ? 0 : 180 };
                        FeedbackOverlay.Visibility = Visibility.Visible;
                        PlayerCommandBar.IsOpen = false;
                        PlayerCommandBar.IsEnabled = false;
                        if (rating == 1 && canNext) Send(BackgroundAudioMessages.Next, true);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Phone player feedback failed: " + exception.GetType().Name);
                if (IsCurrentPresentation(track, currentActivation)) PlaybackStatusText.Text = PhoneText.Get("FeedbackFailed");
            }
            finally
            {
                if (active && activation == currentActivation)
                { feedbackBusy = false; UpdateFeedbackControls(); }
            }
        }

        private void OnShareClick(object sender, RoutedEventArgs args)
        {
            if (displayedTrack == null) return;
            if (shareManager == null)
            {
                shareManager = DataTransferManager.GetForCurrentView();
                shareManager.DataRequested += OnShareDataRequested;
            }
            DataTransferManager.ShowShareUI();
        }
        private void OnShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var track = displayedTrack;
            if (!active || track == null) { args.Request.FailWithDisplayText(PhoneText.Get("PlaybackEmpty")); return; }
            args.Request.Data.Properties.Title = track.Title ?? PhoneText.Get("UnknownTrack");
            // Server stream/artwork URLs can include credentials. Only public
            // track metadata belongs in the platform sharing contract.
            args.Request.Data.SetText((track.Title ?? PhoneText.Get("UnknownTrack")) + Environment.NewLine
                + (track.Artist ?? PhoneText.Get("UnknownArtist")));
        }
        private async void OnLyricsClick(object sender, RoutedEventArgs args) { await LoadLyricsAsync(); }
        private async Task LoadLyricsAsync()
        {
            var track = displayedTrack;
            var currentActivation = activation;
            if (track == null || !feedbackAvailable) return;
            LyricsOverlay.Visibility = Visibility.Visible;
            LyricsText.Text = PhoneText.Get("PlaybackLoading");
            try
            {
                using (var service = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync())
                {
                    if (!IsCurrentPresentation(track, currentActivation) || !MatchesPlaybackAccount(track.Reporting, service.CreateReportingProfile())) return;
                    var variants = await service.GetSongLyricsAsync(track.Id, track.Artist, track.Title);
                    if (!IsCurrentPresentation(track, currentActivation)) return;
                    var text = new System.Text.StringBuilder();
                    if (variants != null && variants.Count > 0 && variants[0].Lines != null)
                        foreach (var line in variants[0].Lines) text.AppendLine(line.Text);
                    LyricsText.Text = text.Length == 0 ? PhoneText.Get("NoLyrics") : text.ToString();
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Phone player lyrics failed: " + exception.GetType().Name);
                if (IsCurrentPresentation(track, currentActivation)) LyricsText.Text = PhoneText.Get("LyricsFailed");
            }
        }
        private void OnCloseLyricsClick(object sender, RoutedEventArgs args) { LyricsOverlay.Visibility = Visibility.Collapsed; }

        private async void OnLikeClick(object sender, RoutedEventArgs args) { await SaveFeedbackAsync(false, 5); }
        private async void OnDislikeClick(object sender, RoutedEventArgs args) { await SaveFeedbackAsync(false, 1); }
        private async void OnFavouriteClick(object sender, RoutedEventArgs args) { await SaveFeedbackAsync(true, 0); }
        private void OnFeedbackOkClick(object sender, RoutedEventArgs args) { FeedbackOverlay.Visibility = Visibility.Collapsed; PlayerCommandBar.IsEnabled = true; }
        private void OnNowPlayingMenuClick(object sender, RoutedEventArgs args) { ShowMixDetails(false, 0); }
        private void OnAboutMenuClick(object sender, RoutedEventArgs args) { ShowMixDetails(true, 1); }
        private void OnQueueMenuClick(object sender, RoutedEventArgs args) { ShowMixDetails(true, 2); }
        private void ShowMixDetails(bool show, int index)
        {
            showingMixDetails = show;
            PlayerPivot.SelectedIndex = index;
            PlayerCommandBar.IsOpen = false;
            UpdatePlayerLayout();
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args) { if (active) UpdatePlayerLayout(); }
        private void OnPlayerCommandBarSizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (!PlayerCommandBar.IsOpen) closedCommandBarHeight = args.NewSize.Height;
            UpdatePlayerLayout();
        }
        private void OnPlayerSizeChanged(object sender, SizeChangedEventArgs args) { UpdatePlayerLayout(); }
        private void UpdatePlayerLayout()
        {
            if (PlayerRoot == null || PlayerRoot.ActualWidth <= 0) return;
            var scale = PlayerRoot.ActualWidth / 480;
            var window = Window.Current.Bounds;
            var top = Math.Max(0, StatusBar.GetForCurrentView().OccludedRect.Bottom - window.Top);
            var systemBottom = Math.Max(0, window.Bottom - ApplicationView.GetForCurrentView().VisibleBounds.Bottom);
            var appBarBottom = PlayerCommandBar.IsOpen ? closedCommandBarHeight : PlayerCommandBar.ActualHeight;
            PlayerContent.Margin = new Thickness(0, top, 0, Math.Max(systemBottom, appBarBottom));
            BackgroundImage.Opacity = showingMixDetails ? 0.75 : 1;
            MixNowPlayingPanel.Margin = new Thickness(24 * scale, 0, 24 * scale, -3 * scale);
            MixAlbumArtBorder.Width = MixAlbumArtBorder.Height = 194 * scale;
            MixSongTitleText.FontSize = 32 * scale;
            MixSongTitleText.LineHeight = 36 * scale;
            MixArtistText.FontSize = 22 * scale;
            PlayerSidebar.Visibility = isMix && !showingMixDetails ? Visibility.Visible : Visibility.Collapsed;
            ShuffleButton.Visibility = RepeatButton.Visibility = isMix ? Visibility.Collapsed : Visibility.Visible;
            PlayerSidebar.Margin = new Thickness(0, 10 * scale, 12 * scale, 0);
            ShareButton.Width = ShareButton.Height = LyricsButton.Width = LyricsButton.Height = 48 * scale;
            ShareIcon.Width = ShareIcon.Height = 45 * scale;
            LyricsOverlay.Margin = new Thickness(0, top + 60 * scale, 0, 0);
            PlayerPivot.Visibility = !isMix || showingMixDetails ? Visibility.Visible : Visibility.Collapsed;
            MixNowPlayingPanel.Visibility = isMix && !showingMixDetails ? Visibility.Visible : Visibility.Collapsed;
            PreviousButton.Visibility = isMix ? Visibility.Collapsed : Visibility.Visible;
            PreviousColumn.Width = new GridLength(isMix ? 0 : 120 * scale);
            PlayPauseColumn.Width = NextColumn.Width = new GridLength(120 * scale);
            PreviousButton.Width = PreviousButton.Height = PlayPauseButton.Width = PlayPauseButton.Height = NextButton.Width = NextButton.Height = 58 * scale;
            TransportButtons.Height = 74 * scale;
            LikeButton.Width = LikeButton.Height = DislikeButton.Width = DislikeButton.Height = FavouriteButton.Width = FavouriteButton.Height = 44 * scale;
            LikeIcon.Width = LikeIcon.Height = DislikeIcon.Width = DislikeIcon.Height = 44 * scale;
            FavouriteIcon.Width = FavouriteIcon.Height = 25 * scale;
            MixDetailsGrid.Margin = new Thickness(12 * scale, 0, 0, 0);
            MixFeedbackRow.Margin = new Thickness(0, 0, 0, 5 * scale);
            TransportPanel.Margin = new Thickness(24 * scale, 0, 24 * scale, 8 * scale);
            FeedbackPanel.Height = 328 * scale;
            FeedbackTitleText.FontSize = 31 * scale;
            FeedbackTitleText.Margin = new Thickness(24 * scale, 16 * scale, 24 * scale, 0);
            FeedbackMessageText.FontSize = 22 * scale;
            FeedbackMessageText.Width = 320 * scale;
            FeedbackMessageText.Margin = new Thickness(24 * scale, 88 * scale, 0, 0);
            FeedbackIcon.Width = FeedbackIcon.Height = 80 * scale;
            FeedbackIcon.Margin = new Thickness(0, 68 * scale, 24 * scale, 0);
            FeedbackOkButton.Width = 204 * scale; FeedbackOkButton.Height = 48 * scale;
            FeedbackOkButton.FontSize = 24 * scale;
            FeedbackOkButton.Margin = new Thickness(26 * scale, 256 * scale, 0, 0);
        }

        private void OnArtworkImageFailed(object sender, ExceptionRoutedEventArgs args)
        {
            var failed = sender as BitmapImage;
            if (!active || failed == null || !ReferenceEquals(failed, artworkImage)) return;
            var replaceAlbum = ReferenceEquals(AlbumArtImage.Source, failed);
            var replaceMixAlbum = ReferenceEquals(MixAlbumArtImage.Source, failed);
            if (!replaceAlbum && !replaceMixAlbum) return;
            DetachArtwork();
            var fallback = new BitmapImage(new Uri(DefaultArtworkUri, UriKind.Absolute));
            if (replaceAlbum) AlbumArtImage.Source = fallback;
            if (replaceMixAlbum) MixAlbumArtImage.Source = fallback;
        }

        private void DetachArtwork()
        {
            if (artworkImage != null) artworkImage.ImageFailed -= OnArtworkImageFailed;
            artworkImage = null;
        }

        private void ResetDisplay()
        {
            DetachArtwork();
            displayedTrack = null;
            feedbackAvailable = false;
            isMix = showingMixDetails = false;
            MixSongTitleText.Text = MixArtistText.Text = string.Empty;
            PlayerTitleBar.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiNowPlaying"));
            UpdatePlayerLayout();
            ClearArtistBackground();
            UpdateFeedbackControls();
            SongTitleText.Text = PhoneText.Get("PlaybackEmpty");
            ArtistText.Text = AboutTitleText.Text = AboutArtistText.Text = AboutAlbumText.Text = string.Empty;
            PlaybackStatusText.Text = string.Empty;
            PreviousButton.IsEnabled = PlayPauseButton.IsEnabled = NextButton.IsEnabled = false;
            ShuffleButton.IsEnabled = RepeatButton.IsEnabled = false;
        }

        private async void OnMediaStateChanged(MediaPlayer sender, object args)
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (!active) return;
                    try { UpdatePlaybackState(); }
                    catch (Exception exception)
                    {
                        Debug.WriteLine("Phone audio state failed: " + exception.GetType().Name);
                        if (active) PlaybackStatusText.Text = PhoneText.Get("PlaybackFailed");
                    }
                });
            }
            catch (Exception exception) { Debug.WriteLine("Phone audio dispatcher unavailable: " + exception.GetType().Name); }
        }

        private void UpdatePlaybackState()
        {
            if (player == null) return;
            var hasTrack = currentIndex >= 0 && currentIndex < QueueItems.Count;
            var state = player.CurrentState;
            var playing = state == MediaPlayerState.Playing;
            PlayPauseGlyph.Text = playing ? "\uE103" : "\uE102";
            AutomationProperties.SetName(PlayPauseButton, PhoneText.Get(playing ? "Pause" : "Play"));
            PreviousButton.IsEnabled = PlayPauseButton.IsEnabled = hasTrack;
            NextButton.IsEnabled = hasTrack && canNext;
            ShuffleButton.IsEnabled = RepeatButton.IsEnabled = hasTrack;
            ShuffleButton.Foreground = shuffle ? (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["MixRadioAccentBrush"]
                : (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationForegroundThemeBrush"];
            RepeatButton.Foreground = repeat != 0 ? (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["MixRadioAccentBrush"]
                : (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationForegroundThemeBrush"];
            RepeatOneBadge.Visibility = repeat == 2 ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(RepeatButton, PhoneText.Get(repeat == 0 ? "RepeatOff" : repeat == 1 ? "RepeatAll" : "RepeatOne"));
            PlaybackStatusText.Text = playbackFailed ? PhoneText.Get("PlaybackFailed")
                : state == MediaPlayerState.Opening || state == MediaPlayerState.Buffering ? PhoneText.Get("PlaybackLoading") : string.Empty;
            UpdateFeedbackControls();
            UpdateProgress();
        }

        private void OnProgressTimerTick(object sender, object args)
        {
            if (active) UpdateProgress();
        }

        private void UpdateProgress()
        {
            if (player == null) return;
            if (IsMediaNotReady(player.CurrentState)) { ResetProgress(); return; }
            double duration, position;
            bool canSeek;
            try
            {
                duration = player.NaturalDuration.TotalSeconds;
                position = player.Position.TotalSeconds;
                canSeek = player.CanSeek;
            }
            catch (COMException exception) when (exception.HResult == unchecked((int)0x80004005) && IsMediaNotReady(player.CurrentState))
            {
                // The background source can switch after CurrentState was read.
                // E_FAIL is transient only when the native player is now unready.
                ResetProgress();
                return;
            }
            ApplyProgress(duration, position, canSeek);
        }

        private static bool IsMediaNotReady(MediaPlayerState state)
        {
            return state != MediaPlayerState.Playing && state != MediaPlayerState.Paused && state != MediaPlayerState.Stopped;
        }

        private void ResetProgress()
        {
            seeking = false;
            ApplyProgress(0, 0, false);
        }

        private void ApplyProgress(double duration, double position, bool canSeek)
        {
            updatingProgress = true;
            try
            {
                ProgressSlider.IsEnabled = canSeek && duration > 0;
                if (!seeking)
                {
                    ProgressSlider.Maximum = duration > 0 ? duration : 1;
                    ProgressSlider.Value = Math.Max(0, Math.Min(position, ProgressSlider.Maximum));
                }
                CurrentTimeText.Text = FormatTime(position);
                TotalTimeText.Text = FormatTime(duration);
            }
            finally { updatingProgress = false; }
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds) || seconds < 0) seconds = 0;
            var time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

        private void OnProgressSliderChanged(object sender, RangeBaseValueChangedEventArgs args)
        {
            if (active && !updatingProgress && !seeking && player != null && player.CanSeek) Send(BackgroundAudioMessages.Seek, args.NewValue);
        }
        private void OnSeekPressed(object sender, PointerRoutedEventArgs args)
        {
            if (ProgressSlider.IsEnabled) { seeking = true; seekPointer = args.Pointer.PointerId; }
        }
        private void OnSeekReleased(object sender, PointerRoutedEventArgs args)
        {
            if (!seeking || args.Pointer.PointerId != seekPointer) return;
            seeking = false;
            if (player != null && player.CanSeek) Send(BackgroundAudioMessages.Seek, ProgressSlider.Value);
        }
        private void OnPlayPauseClick(object sender, RoutedEventArgs args) { Send(player.CurrentState == MediaPlayerState.Playing ? BackgroundAudioMessages.Pause : BackgroundAudioMessages.Play, true); }
        private void OnPreviousClick(object sender, RoutedEventArgs args) { Send(BackgroundAudioMessages.Previous, true); }
        private void OnNextClick(object sender, RoutedEventArgs args) { Send(BackgroundAudioMessages.Next, true); }
        private void OnShuffleClick(object sender, RoutedEventArgs args) { Send(BackgroundAudioMessages.Shuffle, !shuffle); }
        private void OnRepeatClick(object sender, RoutedEventArgs args) { Send(BackgroundAudioMessages.Repeat, (repeat + 1) % 3); }
        private void OnQueueItemClick(object sender, ItemClickEventArgs args)
        {
            var index = QueueItems.IndexOf(args.ClickedItem as TrackInfo);
            if (index >= 0) Send(BackgroundAudioMessages.SelectTrack, index);
        }
        private void OnArtworkSizeChanged(object sender, SizeChangedEventArgs args) { AlbumArtBorder.Height = args.NewSize.Width; }

        private void Send(string key, object value)
        {
            if (!active) return;
            try { BackgroundMediaPlayer.SendMessageToBackground(new ValueSet { { key, value } }); }
            catch (Exception exception)
            {
                Debug.WriteLine("Phone audio command failed: " + exception.GetType().Name);
                PlaybackStatusText.Text = PhoneText.Get("PlaybackUnavailable");
            }
        }
    }
}
