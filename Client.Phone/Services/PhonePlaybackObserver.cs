namespace Mixdrome.Phone.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Windows.Foundation.Collections;
    using Windows.Media.Playback;
    using Windows.Storage;
    using Windows.UI.Core;
    using Windows.UI.Xaml;

    // Read-only projection of the background session. Observing home/chrome
    // never replaces a queue, starts a song, or guesses an index from the file.
    internal sealed class PhonePlaybackObserver : IDisposable
    {
        private readonly CoreDispatcher dispatcher;
        private readonly AudioStateVersionGate gate = new AudioStateVersionGate();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private bool active;
        private string version;
        private List<TrackInfo> tracks = new List<TrackInfo>();
        public event EventHandler Changed;
        public TrackInfo Track { get; private set; }
        public TimeSpan? Position { get; private set; }
        public PhonePlaybackObserver(CoreDispatcher dispatcher) { this.dispatcher = dispatcher; timer.Tick += OnTick; }
        public async Task StartAsync()
        {
            if (active) return;
            active = true;
            try { await ApplicationData.Current.LocalFolder.GetFileAsync(BackgroundAudioMessages.SessionFile); }
            catch (Exception) { active = false; return; }
            if (!active) return;
            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessage;
            try { var player = BackgroundMediaPlayer.Current; RequestState(); timer.Start(); }
            catch (Exception) { Dispose(); }
        }
        private static void RequestState() { BackgroundMediaPlayer.SendMessageToBackground(new ValueSet { { BackgroundAudioMessages.RequestState, true } }); }
        private async void OnMessage(object sender, MediaPlayerDataReceivedEventArgs args)
        {
            try { await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => { try { await ApplyAsync(args.Data); } catch (Exception) { } }); }
            catch (Exception) { }
        }
        private async Task ApplyAsync(ValueSet data)
        {
            if (!active) return;
            var session = data.ContainsKey(BackgroundAudioMessages.Session) ? data[BackgroundAudioMessages.Session] as string : null;
            var sequence = data.ContainsKey(BackgroundAudioMessages.Sequence) && data[BackgroundAudioMessages.Sequence] is int ? (int)data[BackgroundAudioMessages.Sequence] : -1;
            if (!gate.Accept(session, sequence)) return;
            var nextVersion = data.ContainsKey(BackgroundAudioMessages.QueueVersion) ? data[BackgroundAudioMessages.QueueVersion] as string : null;
            if (nextVersion != version && !string.IsNullOrEmpty(nextVersion))
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(BackgroundAudioMessages.SessionFile);
                var snapshot = AudioJson.Deserialize<AudioQueueSnapshot>(await FileIO.ReadTextAsync(file));
                if (!active || !gate.IsCurrent(session, sequence)) return;
                if (snapshot.Version != nextVersion) { RequestState(); return; }
                tracks = snapshot.Tracks ?? new List<TrackInfo>(); version = nextVersion;
            }
            if (!active || !gate.IsCurrent(session, sequence)) return;
            if (string.IsNullOrEmpty(nextVersion)) { tracks.Clear(); version = nextVersion; }
            var index = data.ContainsKey(BackgroundAudioMessages.CurrentIndex) && data[BackgroundAudioMessages.CurrentIndex] is int ? (int)data[BackgroundAudioMessages.CurrentIndex] : -1;
            Track = index >= 0 && index < tracks.Count ? tracks[index] : null;
            OnTick(null, null);
        }
        private void OnTick(object sender, object args)
        {
            if (!active) return;
            Position = null;
            try
            {
                if (Track != null)
                {
                    var player = BackgroundMediaPlayer.Current;
                    var state = player.CurrentState;
                    if (state != MediaPlayerState.Closed && state != MediaPlayerState.Opening && state != MediaPlayerState.Buffering)
                    {
                        var position = player.Position;
                        Position = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(position.TotalSeconds)));
                    }
                }
            }
            catch (Exception) { }
            var handler = Changed; if (handler != null) handler(this, EventArgs.Empty);
        }
        public void Dispose()
        {
            active = false; timer.Stop(); BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessage;
        }
    }
}
