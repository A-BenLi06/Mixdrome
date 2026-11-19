namespace Mixdrome.Phone.Views
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Client.Common.Models.Navidrome;
    using Client.Common.Services;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Services;
    using Windows.Phone.UI.Input;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class CreateMixPage : Page
    {
        private bool active;
        private bool busy;
        private NavidromeApi api;
        private string account;
        private int generation;
        public CreateMixPage()
        {
            InitializeComponent();
            PageTitleBar.Title = CultureInfo.CurrentCulture.TextInfo.ToUpper(PhoneText.Get("UiMixes"));
            PageTitle.Text = PhoneText.Get("CreateMixTitle"); NameLabel.Text = PhoneText.Get("MixName");
            GenreLabel.Text = PhoneText.Get("UiGenre"); DescriptionText.Text = PhoneText.Get("CreateMixDescription");
            CreateButton.Content = PhoneText.Get("CreateMixAction");
        }
        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var scale = e.NewSize.Width / 480;
            if (scale <= 0) return;
            ContentPanel.Margin = new Thickness(24 * scale, 5 * scale, 24 * scale, 24 * scale);
            PageTitle.FontSize = 72 * scale;
            PageTitle.Margin = new Thickness(-3 * scale, 0, 0, 20 * scale);
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e); active = true; HardwareButtons.BackPressed += OnBackPressed;
            var version = ++generation; SetBusy(true);
            try
            {
                var configuration = await PhoneSubsonicServiceFactory.LoadConfigurationAsync();
                if (!active || version != generation) return;
                if (string.IsNullOrEmpty(configuration.BaseUrl)) throw new InvalidOperationException(PhoneText.Get("ConfigureServer"));
                api = new NavidromeApi(configuration); account = api.AccountKey;
                var genres = await api.GetGenres(null).ReadAsync();
                if (!active || version != generation) return;
                var choices = new List<PhoneMixGenre> { new PhoneMixGenre { Name = PhoneText.Get("AllGenres") } };
                choices.AddRange(genres.Where(genre => !string.IsNullOrWhiteSpace(genre.Name)).Select(genre => new PhoneMixGenre { Name = genre.Name, Value = genre.Name }));
                GenreBox.ItemsSource = choices; GenreBox.SelectedIndex = 0;
            }
            catch (Exception exception) { if (active && version == generation) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (active && version == generation) SetBusy(false); }
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            active = false; generation++; HardwareButtons.BackPressed -= OnBackPressed; base.OnNavigatedFrom(e);
        }
        private void SetBusy(bool value)
        {
            busy = value; LoadingIndicator.IsActive = value;
            CreateButton.IsEnabled = !value && api != null; NameBox.IsEnabled = !value; GenreBox.IsEnabled = !value;
        }
        private async void OnCreateClick(object sender, RoutedEventArgs e)
        {
            if (busy || api == null) return;
            var name = (NameBox.Text ?? string.Empty).Trim();
            if (name.Length == 0) { StatusText.Text = PhoneText.Get("MixNameRequired"); NameBox.Focus(FocusState.Programmatic); return; }
            var version = generation; SetBusy(true); StatusText.Text = string.Empty;
            try
            {
                var configuration = await PhoneSubsonicServiceFactory.LoadConfigurationAsync();
                if (!active || version != generation) return;
                if (AccountIdentity.For(configuration) != account) throw new OperationCanceledException();
                var selected = GenreBox.SelectedItem as PhoneMixGenre;
                var songs = await api.GetRandomSongs(new RandomSongQuery { Genre = selected == null ? null : selected.Value, Size = 30 }).ReadAsync();
                if (!active || version != generation) return;
                if (!PhoneOfflineMixes.IsCurrentAccount(account)) throw new OperationCanceledException();
                if (songs.Count == 0) { StatusText.Text = PhoneText.Get("PlaylistEmpty"); return; }
                // A mix is an actual server playlist. No proprietary recommendation state is implied.
                var saved = await api.SavePlaylist(name, songs.Select(song => song.Id)).ReadAsync();
                if (!active || version != generation) return;
                StatusText.Text = PhoneText.Format("MixCreated", name, songs.Count);
                Frame.Navigate(typeof(PlaybackPage), new PlaybackNavigationParameter
                {
                    Playlist = songs.Select((song, index) => PhoneCollectionPresentation.Song(song, index + 1)).ToList(),
                    StartIndex = 0, IsMix = true, MixTitle = saved == null || string.IsNullOrEmpty(saved.Name) ? name : saved.Name
                });
            }
            catch (OperationCanceledException) { if (active && version == generation) StatusText.Text = PhoneText.Get("OfflineAccountChanged"); }
            catch (Exception exception) { if (active && version == generation) StatusText.Text = PhoneText.Format("LoadFailed", exception.Message); }
            finally { if (active && version == generation) SetBusy(false); }
        }
        private void OnBackPressed(object sender, BackPressedEventArgs e)
        { if (!e.Handled && Frame.CanGoBack) { e.Handled = true; Frame.GoBack(); } }
    }
    public sealed class PhoneMixGenre
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
