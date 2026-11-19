namespace Mixdrome.Main
{
    using System;
    using System.Collections.Generic;
    using Mixdrome.Navidrome;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;

    public sealed partial class MainView
    {
        private readonly List<MixRadioHomePanel> _panels = new List<MixRadioHomePanel>();
        private MixRadioPlayerSurface _player;
        private MainViewModel Model { get { return DataContext as MainViewModel; } }
        public MainView()
        {
            InitializeComponent();
            Loaded += (sender, args) => Reflow();
            SizeChanged += (sender, args) => Reflow();
            Unloaded += (sender, args) => { _panels.Clear(); _player = null; CloseQueue(); };
        }
        private void Reflow()
        {
            var narrow = ActualWidth < 800;
            MainHub.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
            HeaderIndent.Width = new GridLength(narrow ? 0 : 80);
            WordmarkHost.Height = narrow ? 34 : 41;
            WordmarkHost.MaxWidth = narrow ? Math.Max(160, ActualWidth - 132) : 360;
            WordmarkHost.Margin = new Thickness(0, 0, narrow ? 18 : 24, 0);
            SearchControl.Margin = new Thickness(0, narrow ? 112 : 70, narrow ? 20 : 32, 0);
            SearchControl.Width = Math.Min(270, Math.Max(220, ActualWidth - 40));
            ConnectionStatus.Width = Math.Min(320, Math.Max(220, ActualWidth - 40));
            ConnectionStatus.Margin = new Thickness(0, narrow ? 156 : 112, narrow ? 20 : 32, 0);
            PlayerSection.Margin = narrow ? new Thickness(0, 44, 0, 0) : new Thickness(0);
            if (_player != null) _player.SetViewport(ActualWidth, ActualHeight);
            foreach (var panel in _panels) panel.SetViewport(ActualWidth, ActualHeight);
        }
        private void OnPanelLoaded(object sender, RoutedEventArgs args)
        {
            var panel = (MixRadioHomePanel)sender;
            if (!_panels.Contains(panel)) _panels.Add(panel);
            panel.SetViewport(ActualWidth, ActualHeight);
        }
        private void OnPlayerLoaded(object sender, RoutedEventArgs args)
        { _player = (MixRadioPlayerSurface)sender; _player.SetViewport(ActualWidth, ActualHeight); }
        private async void OnTileInvoked(object sender, ItemClickEventArgs args)
        { if (Model != null && await Model.OpenTile(args.ClickedItem)) MainHub.ScrollToSection(PlayerSection); }
        private void OnSectionHeaderClicked(object sender, HubSectionHeaderClickEventArgs args)
        { if (Model != null) Model.OpenDestination((LibraryDestination)args.Section.Tag); }
        private void OnLibraryClicked(object sender, RoutedEventArgs args)
        {
            if (Model == null) return;
            var model = Model;
            var menu = new MenuFlyout();
            foreach (var destination in model.Destinations)
            {
                var target = destination;
                var item = new MenuFlyoutItem { Text = target.Label };
                item.Click += (button, clicked) => model.OpenDestination(target);
                menu.Items.Add(item);
            }
            menu.ShowAt((FrameworkElement)sender);
        }
        private void OnBrowseRequested(object sender, EventArgs args) { if (Model != null) Model.PlayMe(); }
        private void OnPlayerClicked(object sender, RoutedEventArgs args) { if (Model != null) Model.OpenPlayer(); }
        private void OnSettingsClicked(object sender, RoutedEventArgs args) { if (Model != null) Model.Settings(); }
        private async void OnReloadClicked(object sender, RoutedEventArgs args) { if (Model != null) await Model.Reload(); }
        private void OnQueueRequested(object sender, EventArgs args)
        { QueueDismiss.Visibility = QueueDrawer.Visibility = Visibility.Visible; QueueDrawer.FocusCloseButton(); }
        private void OnQueueClosed(object sender, EventArgs args) { CloseQueue(); }
        private void OnQueueDismissed(object sender, RoutedEventArgs args) { CloseQueue(); }
        private void CloseQueue() { QueueDismiss.Visibility = QueueDrawer.Visibility = Visibility.Collapsed; }
        private void OnPageKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key == VirtualKey.Escape && QueueDrawer.Visibility == Visibility.Visible)
            { CloseQueue(); args.Handled = true; return; }
            SearchControl.HandleShortcut(args);
        }
    }
}
