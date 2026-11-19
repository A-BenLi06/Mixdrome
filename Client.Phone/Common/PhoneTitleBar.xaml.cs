namespace Mixdrome.Phone.Common
{
    using System;
    using Mixdrome.Phone.Services;
    using Mixdrome.Phone.Views;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Automation;
    using Windows.UI.Xaml.Controls;

    public sealed partial class PhoneTitleBar : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(PhoneTitleBar), new PropertyMetadata(string.Empty, OnTextChanged));
        private PhonePlaybackObserver observer;
        private bool loaded;
        private int loadedGeneration;
        private double scale = 1;
        public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
        public PhoneTitleBar()
        {
            InitializeComponent();
            AutomationProperties.SetName(HomeButton, PhoneText.Get("UiHome"));
            AutomationProperties.SetName(ElapsedButton, PhoneText.Get("UiNowPlaying"));
            ElapsedButton.IsEnabled = false;
            ElapsedButton.Visibility = Visibility.Collapsed;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var bar = (PhoneTitleBar)sender;
            if (bar.TitleText == null) return;
            bar.TitleText.Text = bar.Title ?? string.Empty;
        }
        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            loaded = true;
            var generation = ++loadedGeneration;
            if (!loaded || generation != loadedGeneration || observer != null) return;
            // A player owns its own live state. Other pages expose the current
            // session through the subdued elapsed-time shortcut.
            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.Content is PlaybackPage) return;
            var nextObserver = new PhonePlaybackObserver(Dispatcher);
            observer = nextObserver;
            nextObserver.Changed += OnPlaybackChanged;
            await nextObserver.StartAsync();
            if (!loaded || generation != loadedGeneration || !ReferenceEquals(observer, nextObserver)) nextObserver.Dispose();
        }
        private void StopObserver()
        {
            ++loadedGeneration;
            if (observer != null) { observer.Dispose(); observer = null; }
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            loaded = false;
            StopObserver();
        }
        private void OnPlaybackChanged(object sender, EventArgs args)
        {
            if (!loaded || observer == null || !ReferenceEquals(sender, observer)) return;
            var position = observer.Track == null ? null : observer.Position;
            ElapsedButton.IsEnabled = position.HasValue;
            ElapsedButton.Visibility = position.HasValue ? Visibility.Visible : Visibility.Collapsed;
            if (position.HasValue) ElapsedProgress.Position = position.Value;
            UpdateTitleMargin();
        }
        private void OnBarSizeChanged(object sender, SizeChangedEventArgs args)
        {
            scale = args.NewSize.Width / 480;
            if (scale <= 0) return;
            Bar.Height = 60 * scale;
            HomeColumn.Width = new GridLength(60 * scale);
            HomeGlyph.Width = HomeGlyph.Height = 25 * scale;
            Divider.Width = scale; Divider.Height = 44 * scale;
            TitleText.FontSize = 20 * scale;
            UpdateTitleMargin();
            ElapsedProgress.Scale = scale;
        }
        private void UpdateTitleMargin() { TitleText.Margin = new Thickness(9 * scale, 0, ElapsedButton.IsEnabled ? -60 * scale : 0, 0); }
        private void OnHomeClick(object sender, RoutedEventArgs args)
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null || frame.Content is MainPage) return;
            StopObserver();
            // Home is a root navigation. Let Frame select the cached home page
            // before clearing its journal, rather than pruning a live back
            // journal and then asking the legacy Frame to resolve GoBack.
            if (frame.Navigate(typeof(MainPage))) frame.BackStack.Clear();
        }
        private void OnElapsedClick(object sender, RoutedEventArgs args)
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null || frame.Content is PlaybackPage || !ElapsedButton.IsEnabled) return;
            StopObserver();
            frame.Navigate(typeof(PlaybackPage));
        }
    }
}
