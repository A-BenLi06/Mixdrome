namespace Mixdrome.Phone
{
    using System;
    using Mixdrome.Phone.Views;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Activation;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
    using Windows.UI.ViewManagement;

    public sealed partial class App : Application
    {
        private int statusBarNavigationVersion;
        private bool updatingStatusBar;
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 2;
                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            rootFrame.Navigated -= OnRootFrameNavigated;
            rootFrame.Navigated += OnRootFrameNavigated;

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
            UpdateStatusBarVisibility();
        }

        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (ReferenceEquals(sender, Window.Current.Content)) UpdateStatusBarVisibility();
        }

        private async void UpdateStatusBarVisibility()
        {
            ++statusBarNavigationVersion;
            if (updatingStatusBar) return;
            updatingStatusBar = true;
            try
            {
                // Serialize native transitions. If navigation changes while an
                // operation awaits completion, apply the latest page immediately
                // afterwards instead of leaving the old page's visibility behind.
                while (true)
                {
                    var frame = Window.Current.Content as Frame;
                    if (frame == null || frame.Content == null) return;
                    var version = statusBarNavigationVersion;
                    var hide = frame.Content is MainPage;
                    var player = frame.Content is PlaybackPage;
                    ApplicationView.GetForCurrentView().SetDesiredBoundsMode(player
                        ? ApplicationViewBoundsMode.UseCoreWindow : ApplicationViewBoundsMode.UseVisible);
                    var bar = StatusBar.GetForCurrentView();
                    bar.BackgroundColor = Windows.UI.Color.FromArgb(255, 48, 48, 48);
                    bar.ForegroundColor = Windows.UI.Colors.White;
                    bar.BackgroundOpacity = player ? 0 : 1;
                    if (hide) await bar.HideAsync(); else await bar.ShowAsync();
                    if (version == statusBarNavigationVersion && ReferenceEquals(frame, Window.Current.Content)
                        && hide == (frame.Content is MainPage)) return;
                }
            }
            catch (Exception) { }
            finally { updatingStatusBar = false; }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
