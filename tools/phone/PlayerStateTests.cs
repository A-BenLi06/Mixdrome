using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Mixdrome.Phone.Common;
using Client.Common.Models.Subsonic;
using Client.Common.Services;
using Client.Common.Services.DataStructures.SubsonicService;
using Mixdrome.Phone.Services;
using Mixdrome.Phone.Views;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Playback;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

// run-playback.ps1 inserts the production page's exact state/rendering/lifecycle
// methods into this managed shell. These controls observe visible values and
// messages; they do not certify native XAML, media decoding or device lifetime.
public static class PhonePlayerStateTests
{
    private static int assertions;
    private static void Check(bool condition, string message) { ++assertions; if (!condition) throw new Exception(message); }
    private static ValueSet State(string version, int sequence, string kind = BackgroundAudioMessages.Snapshot)
    {
        return new ValueSet
        {
            { kind, kind == BackgroundAudioMessages.Failure ? (object)"PlaybackFailed" : true },
            { BackgroundAudioMessages.Session, "session" }, { BackgroundAudioMessages.Sequence, sequence },
            { BackgroundAudioMessages.QueueVersion, version }, { BackgroundAudioMessages.CurrentIndex, 1 },
            { BackgroundAudioMessages.CanNext, true }, { BackgroundAudioMessages.Shuffle, true },
            { BackgroundAudioMessages.Repeat, 1 }, { BackgroundAudioMessages.PlayerState, "Stopped" }
        };
    }
    private static string Snapshot(string version, string title, string artwork = null)
    {
        return AudioJson.Serialize(new AudioQueueSnapshot { Version = version, Tracks = new List<TrackInfo>
        {
            new TrackInfo { Id = "duplicate", Title = "First " + title, Artist = "First Artist", Album = "Album" },
            new TrackInfo { Id = "duplicate", Title = title, Artist = "Selected Artist", Album = "Selected Album", CoverArt = artwork }
        } });
    }
    private static PlaybackPage Page()
    {
        BackgroundMediaPlayer.Messages.Clear();
        BackgroundMediaPlayer.Command = null;
        BackgroundMediaPlayer.Current.Reset();
        return new PlaybackPage();
    }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production phone player failure/snapshot, sequence, version, metadata and departure assertions. Native XAML/audio remain unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        var page = Page();
        var pendingRead = new TaskCompletionSource<string>();
        FileIO.Read = () => pendingRead.Task;
        var oldSnapshot = page.Accept(State("queue", 1));
        Check(!oldSnapshot.IsCompleted, "An initial production snapshot is held at the actual awaited file read.");
        FileIO.Read = () => Task.FromResult(Snapshot("queue", "Failed track"));
        await page.Accept(State("queue", 2, BackgroundAudioMessages.Failure));
        Check(page.QueueItems.Count == 2 && page.SelectedIndex == -1, "Failure loads the queue without selecting an unrealized Pivot list.");
        Check(page.Title == "Failed track" && page.Artist == "Selected Artist" && page.Album == "Selected Album", "Failure displays the actual selected title, artist and album.");
        Check(page.Status == "PlaybackFailed" && page.Failed, "Loading a replacement snapshot cannot clear the current failure.");
        Check(page.PlayEnabled && page.NextEnabled, "Failure retains the real queue's transport controls.");
        Check(page.SelectionWrites == 0, "Metadata and transport can update before the queue view is loaded.");
        page.LoadQueue();
        Check(page.SelectedIndex == -1 && page.SelectionWrites == 0, "A loaded queue with an empty Items binding does not receive an out-of-range index.");
        page.PublishUnrelatedDuplicates();
        Check(page.SelectedIndex == -1 && page.SelectionWrites == 0, "Matching media IDs in a stale Items collection cannot select the wrong occurrence.");
        page.PublishQueue();
        Check(page.SelectedIndex == 1 && ReferenceEquals(page.SelectedQueueObject, page.QueueItems[1]), "VectorChanged selects the exact second duplicate occurrence once the Items binding is ready.");
        pendingRead.SetResult(Snapshot("queue", "Older title")); await oldSnapshot;
        Check(page.Title == "Failed track" && page.Failed, "The interrupted older snapshot cannot overwrite metadata or clear the newer failure.");
        Check(BackgroundMediaPlayer.Messages.Count == 0, "Displaying failure never replays, clears or replaces audio to hide the error.");
        var selectionWrites = page.SelectionWrites;
        page.UnloadQueue(); page.PublishQueue();
        Check(page.SelectionWrites == selectionWrites, "An unloaded queue view is detached and cannot receive selection writes.");
        page.LoadQueue();
        Check(ReferenceEquals(page.SelectedQueueObject, page.QueueItems[1]), "Loading the queue again synchronizes its retained duplicate occurrence.");

        await page.Accept(State("queue", 1, BackgroundAudioMessages.TrackChanged));
        Check(page.Title == "Failed track" && page.Failed, "An out-of-order older track change cannot clear a newer failure.");
        FileIO.Read = () => Task.FromResult(Snapshot("next", "Next track"));
        await page.Accept(State("next", 3, BackgroundAudioMessages.TrackChanged));
        Check(page.Title == "Next track" && !page.Failed && page.Status == string.Empty, "A genuinely newer track change can replace metadata and clear the prior failure.");

        page = Page(); pendingRead = new TaskCompletionSource<string>(); FileIO.Read = () => pendingRead.Task;
        oldSnapshot = page.Accept(State("old", 1));
        FileIO.Read = () => Task.FromResult(Snapshot("new", "Newest track"));
        await page.Accept(State("new", 2));
        pendingRead.SetResult(Snapshot("old", "Stale track")); await oldSnapshot;
        Check(page.Title == "Newest track" && page.QueueItems.Count == 2, "A late read from an older queue version cannot replace the current queue.");

        page = Page(); FileIO.Read = () => Task.FromResult(Snapshot("actual", "Actual track"));
        await page.Accept(State("expected", 1, BackgroundAudioMessages.Failure));
        Check(page.QueueItems.Count == 0 && page.Title == "PlaybackEmpty", "A mismatched file version is not committed under a failed message's queue identity.");
        Check(BackgroundMediaPlayer.Messages.Count == 1 && BackgroundMediaPlayer.Messages[0].ContainsKey(BackgroundAudioMessages.RequestState), "A mismatched file requests authoritative state instead of guessing the queue.");
        await page.Accept(State("actual", 2, BackgroundAudioMessages.Failure));
        Check(page.Title == "Actual track" && page.Failed, "A matching authoritative failure restores metadata without losing the error.");

        page = Page(); pendingRead = new TaskCompletionSource<string>(); FileIO.Read = () => pendingRead.Task;
        var leavingFailure = page.Accept(State("queue", 1, BackgroundAudioMessages.Failure));
        page.Leave(); pendingRead.SetResult(Snapshot("queue", "Late track")); await leavingFailure;
        Check(page.QueueItems.Count == 0 && page.Title == "PlaybackEmpty" && page.Status == string.Empty, "Leaving during a failure snapshot prevents late metadata and error UI updates.");
        Check(BackgroundMediaPlayer.Messages.Count == 0, "An abandoned page cannot issue playback or recovery commands after its file read completes.");

        page = Page(); FileIO.Read = () => Task.FromResult(Snapshot("queue", "Leaving loaded queue"));
        await page.Accept(State("queue", 1)); page.LoadQueue();
        page.Leave(); page.PublishQueue();
        Check(page.SelectionWrites == 0, "Leaving a loaded page detaches Items changes before the binding can finish.");

        page = Page(); FileIO.Read = () => Task.FromResult(Snapshot("webp", "Actual failed track", "https://example.invalid/artwork.webp"));
        await page.Accept(State("webp", 1, BackgroundAudioMessages.Failure));
        var unsupportedImage = page.Artwork;
        Check(unsupportedImage.FailureSubscribers == 1, "A non-default cover subscribes to the public BitmapImage.ImageFailed event.");
        unsupportedImage.Fail();
        var defaultImage = page.Artwork;
        Check(defaultImage.UriSource.AbsoluteUri == "ms-appx:///Assets/MixRadio/DefaultArtwork300.png" && page.BackgroundArtwork == null, "An unsupported cover falls back to the packaged PNG while the artist background stays empty.");
        Check(page.Title == "Actual failed track" && page.QueueItems.Count == 2 && page.Failed && page.Status == "PlaybackFailed", "Image fallback preserves queue metadata and the existing audio failure state.");
        Check(unsupportedImage.FailureSubscribers == 0 && defaultImage.FailureSubscribers == 0, "The failed bitmap is detached and the default PNG has no failure subscription or retry loop.");
        defaultImage.Fail();
        Check(ReferenceEquals(defaultImage, page.Artwork) && BackgroundMediaPlayer.Messages.Count == 0, "Default image failure cannot replay audio, alter the queue or loop through image requests.");

        FileIO.Read = () => Task.FromResult(Snapshot("real", "Real cover", "https://example.invalid/real-cover.png"));
        await page.Accept(State("real", 2));
        var realImage = page.Artwork;
        Check(realImage.UriSource.AbsoluteUri == "https://example.invalid/real-cover.png" && page.BackgroundArtwork == null, "A real cover remains the artwork without masquerading as an artist photo.");
        FileIO.Read = () => Task.FromResult(Snapshot("newest", "Newest cover", "https://example.invalid/newest-cover.png"));
        await page.Accept(State("newest", 3));
        var newestImage = page.Artwork;
        page.DeliverImageFailure(realImage);
        Check(realImage.FailureSubscribers == 0 && ReferenceEquals(newestImage, page.Artwork), "A queued failure callback from an old bitmap cannot replace a newer cover.");
        newestImage.Fail();
        Check(!page.Failed && page.Status == string.Empty && page.Title == "Newest cover" && BackgroundMediaPlayer.Messages.Count == 0, "Image decode failure does not invent an audio failure or restart playback.");

        FileIO.Read = () => Task.FromResult(Snapshot("leaving", "Leaving cover", "https://example.invalid/leaving-cover.png"));
        await page.Accept(State("leaving", 4));
        var leavingImage = page.Artwork; page.Leave();
        page.DeliverImageFailure(leavingImage);
        Check(leavingImage.FailureSubscribers == 0 && ReferenceEquals(leavingImage, page.Artwork), "Leaving detaches the image event and ignores a previously queued failure callback.");

        page = Page(); FileIO.Read = () => Task.FromResult(Snapshot("reset", "Reset cover", "https://example.invalid/reset-cover.png"));
        await page.Accept(State("reset", 1));
        var resetImage = page.Artwork; page.ResetView(); page.DeliverImageFailure(resetImage);
        Check(resetImage.FailureSubscribers == 0 && page.Title == "PlaybackEmpty" && ReferenceEquals(resetImage, page.Artwork), "ResetDisplay detaches old artwork and ignores its late callbacks.");

        page = Page();
        var native = BackgroundMediaPlayer.Current;
        native.NaturalDuration = TimeSpan.FromSeconds(90); native.Position = TimeSpan.FromSeconds(17); native.CanSeek = true;
        foreach (var state in new[] { MediaPlayerState.Closed, MediaPlayerState.Opening, MediaPlayerState.Buffering })
        {
            native.CurrentState = state; native.ClearReadCounts(); page.Poll();
            Check(native.ProgressReads == 0, "Closed/Opening/Buffering polling never touches native duration, position or seek capabilities before they are ready.");
            Check(!page.SeekEnabled && page.Progress == 0 && page.Elapsed == "0:00" && page.Duration == "0:00", "Unready media exposes disabled zero progress without reporting a playback failure.");
        }
        native.CurrentState = MediaPlayerState.Playing; page.Poll();
        Check(page.SeekEnabled && page.Progress == 17 && page.Elapsed == "0:17" && page.Duration == "1:30", "Playing media supplies its actual native position and duration.");
        native.CurrentState = MediaPlayerState.Paused; native.Position = TimeSpan.FromSeconds(73); page.Poll();
        Check(page.Progress == 73 && page.Elapsed == "1:13", "Paused media continues exposing its real position.");

        native.CurrentState = MediaPlayerState.Playing;
        native.DurationRead = () => { native.CurrentState = MediaPlayerState.Opening; throw new COMException("Source changed before the duration read.", unchecked((int)0x80004005)); };
        page.Poll();
        Check(!page.SeekEnabled && page.Progress == 0 && !page.Failed, "E_FAIL during a confirmed transition to Opening resets progress without inventing a media failure.");
        native.CurrentState = MediaPlayerState.Playing;
        native.DurationRead = () => { throw new COMException("Failure while the native source remains ready.", unchecked((int)0x80004005)); };
        var propagated = false;
        try { page.Poll(); } catch (COMException) { propagated = true; }
        Check(propagated, "E_FAIL while the player remains ready is not hidden as a transient state.");
        native.CurrentState = MediaPlayerState.Playing;
        native.DurationRead = () => { native.CurrentState = MediaPlayerState.Opening; throw new COMException("An unrelated native error.", unchecked((int)0x80070057)); };
        propagated = false;
        try { page.Poll(); } catch (COMException) { propagated = true; }
        Check(propagated, "An unrelated COM error is not swallowed even when source state changes.");

        page = Page(); native = BackgroundMediaPlayer.Current; native.CurrentState = MediaPlayerState.Closed;
        BackgroundMediaPlayer.Command = message =>
        {
            if (!message.ContainsKey(BackgroundAudioMessages.RequestState)) return;
            var readyState = State(string.Empty, 1, BackgroundAudioMessages.Ready);
            readyState[BackgroundAudioMessages.CurrentIndex] = -1;
            page.Accept(readyState).GetAwaiter().GetResult();
            native.CurrentState = MediaPlayerState.Playing;
            native.DurationRead = () => { throw new COMException("Initial ready-state update failed.", unchecked((int)0x80004005)); };
        };
        page.ActivateBack();
        Check(page.TimerRunning && page.Status == "PlaybackFailed", "An activation starts progress polling after readiness even if its initial native state update fails.");
        Check(page.QueueStartCalls == 0, "Reconnecting through Back never starts the navigation queue again.");
        native.DurationRead = null; native.NaturalDuration = TimeSpan.FromSeconds(90); native.Position = TimeSpan.FromSeconds(41); native.CanSeek = true;
        page.Poll();
        Check(page.TimerRunning && page.Progress == 41 && page.Duration == "1:30", "Later timer ticks recover real progress after an initial activation read error.");
        page.Leave();
        Check(!page.TimerRunning, "Leaving the page stops polling even after activation recovery.");
        await FeedbackChecks();
        await OfflineEntryChecks();
        await AccountPathChecks();
        PhoneOfflineMixes.Resolve = songs => Task.FromResult(new TestPlaybackConfiguration { BaseUrl = "https://secondary.invalid/", Username = "listener", AuthenticationMode = "Password" });
        var offlineProfile = await PlaybackPage.PrepareLocalQueue();
        Check(offlineProfile.BaseUrl == "https://secondary.invalid/" && offlineProfile.Username == "listener", "The production local-queue branch preserves the validated cache source route for listening reports.");
        PhoneOfflineMixes.Resolve = songs => Task.FromResult<TestPlaybackConfiguration>(null);
        var offlineRejected = false;
        try { await PlaybackPage.PrepareLocalQueue(); } catch (InvalidOperationException) { offlineRejected = true; }
        Check(offlineRejected, "A failed local manifest/route validation cannot construct a playback service or guess the preferred route.");
    }
    private static PhoneReportingProfile AccountProfile(string url, string user = "listener", string authentication = "Password")
    { return new PhoneReportingProfile { BaseUrl = url, Username = user, AuthenticationMode = authentication }; }
    private static async Task AccountPathChecks()
    {
        var lower = AccountProfile("https://music.invalid/music/");
        Check(PlaybackPage.SameAccount(lower, AccountProfile("HTTPS://MUSIC.INVALID:443/music")), "Account identity canonicalizes scheme, host, default port and trailing slash while preserving the path.");
        Check(!PlaybackPage.SameAccount(lower, AccountProfile("https://music.invalid/Music/")), "Case-sensitive server path differences identify separate accounts.");
        Check(!PlaybackPage.SameAccount(lower, AccountProfile("https://music.invalid/music/", "Listener"))
            && !PlaybackPage.SameAccount(lower, AccountProfile("https://music.invalid/music/", "listener", "ApiKey")), "User identity and authentication mode retain ordinal matching.");
        Check(!PlaybackPage.SameAccount(lower, AccountProfile(null)) && !PlaybackPage.SameAccount(lower, AccountProfile("invalid")), "Empty and invalid server identities cannot authorize feedback.");
        var previousProfile = WP8SubsonicService.Profile;
        try
        {
            WP8SubsonicService.Profile = lower;
            WP8SubsonicService.ReadSong = id => Task.FromResult(new TestSong { UserRating = 3 });
            var track = OfflineTrack("case-sensitive"); track.Reporting = lower;
            var page = Page(); page.OfflineQueue(track);
            Check(page.FeedbackEnabled && page.OfflineEnabled, "The path-isolation test starts with a valid confirmed server context.");
            var writes = 0;
            WP8SubsonicService.Profile = AccountProfile("https://music.invalid/Music/");
            WP8SubsonicService.WriteRating = (id, value) => { ++writes; return Task.FromResult(true); };
            await page.Rate(5);
            Check(writes == 0 && page.Rating == 3 && BackgroundMediaPlayer.Messages.Count == 0, "Changing only server path case blocks feedback before any rating write or playback command.");
            PhoneSubsonicServiceFactory.Load = () => Task.FromResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid/Music/", Username = "listener", AuthenticationMode = "Password" });
            await page.SaveOffline();
            Check(page.Frame.Navigations == 0 && page.Status == "OfflineAccountChanged", "Changing only server path case also blocks the real Play Me offline navigation workflow.");
        }
        finally { WP8SubsonicService.Profile = previousProfile; }
    }

    private static TrackInfo OfflineTrack(string id)
    {
        var track = FeedbackTrack(id); track.StreamUrl = "https://music.invalid/stream/" + id; return track;
    }
    private static async Task OfflineEntryChecks()
    {
        WP8SubsonicService.ReadSong = id => Task.FromResult(new TestSong());
        var page = Page(); page.OfflineQueue(OfflineTrack("one"), OfflineTrack("one"), OfflineTrack("two"));
        Check(page.OfflineEnabled, "A complete online Play Me queue exposes the real make-available-offline command.");
        var pending = new TaskCompletionSource<TestPlaybackConfiguration>(); var calls = 0;
        PhoneSubsonicServiceFactory.Load = () => { ++calls; return pending.Task; };
        var save = page.SaveOffline(); var duplicate = page.SaveOffline();
        Check(!page.OfflineEnabled && calls == 1, "Preparing offline navigation disables the command and ignores repeated clicks.");
        pending.SetResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid", Username = "listener", AuthenticationMode = "Password" }); await save; await duplicate;
        var request = page.Frame.Parameter as PhoneOfflineRequest;
        Check(page.Frame.Navigations == 1 && page.Frame.Destination == typeof(OfflineMixesPage) && request != null && request.Mix.IsPlayMe && request.Mix.Id == "playme", "The command navigates to the real download workflow with a Play Me definition.");
        Check(request.Mix.Songs.Count == 3 && request.Mix.Songs[0].Id == "one" && request.Mix.Songs[1].Id == "one" && request.Mix.Songs[2].Id == "two", "Offline navigation preserves every queue occurrence and its original server ID.");
        page = Page(); var named = OfflineTrack("named"); named.MixTitle = "Named mix"; page.OfflineQueue(named);
        Check(!page.OfflineEnabled, "Named mixes keep their existing collection download entry instead of exposing the Play Me command.");
        page = Page(); var local = OfflineTrack("local"); local.StreamUrl = "ms-appdata:///local/phone-offline-mixes/cache.mp3"; page.OfflineQueue(local);
        Check(!page.OfflineEnabled, "An already-local Play Me queue cannot create a misleading download request.");
        page = Page(); var radio = OfflineTrack("radio"); radio.Reporting = null; page.OfflineQueue(radio);
        Check(!page.OfflineEnabled, "External radio streams without a server reporting identity are not downloadable catalog Play Me.");
        page = Page(); var foreign = OfflineTrack("foreign"); foreign.Reporting.Username = "other-user"; page.OfflineQueue(OfflineTrack("one"), foreign);
        Check(!page.OfflineEnabled, "A mixed-account queue cannot be offered as a single downloadable mix.");
        page = Page(); page.OfflineQueue(OfflineTrack("one")); pending = new TaskCompletionSource<TestPlaybackConfiguration>(); PhoneSubsonicServiceFactory.Load = () => pending.Task;
        save = page.SaveOffline(); page.ChangeQueueVersion(); pending.SetResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid", Username = "listener", AuthenticationMode = "Password" }); await save;
        Check(page.Frame.Navigations == 0, "A replaced queue invalidates an in-flight offline command before navigation.");
        page = Page(); page.OfflineQueue(OfflineTrack("one")); PhoneSubsonicServiceFactory.Load = () => Task.FromResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid", Username = "other-user", AuthenticationMode = "Password" }); await page.SaveOffline();
        Check(page.Frame.Navigations == 0 && page.Status == "OfflineAccountChanged", "The selected connection must still match the queue's actual account before downloading.");
        page = Page(); page.OfflineQueue(OfflineTrack("one")); pending = new TaskCompletionSource<TestPlaybackConfiguration>(); PhoneSubsonicServiceFactory.Load = () => pending.Task;
        save = page.SaveOffline(); page.Leave(); pending.SetResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid", Username = "listener", AuthenticationMode = "Password" }); await save;
        Check(page.Frame.Navigations == 0, "Leaving the player cancels stale offline navigation even when routing completes later.");
    }

    private static TrackInfo FeedbackTrack(string id, string user = "listener")
    {
        return new TrackInfo { Id = id, Title = id, Artist = "Artist", CoverArt = "https://music.invalid/cover.png", IsMix = true,
            Reporting = new PhoneReportingProfile { BaseUrl = "https://music.invalid", Username = user, AuthenticationMode = "Password" } };
    }
    private static async Task FeedbackChecks()
    {
        var reads = 0;
        WP8SubsonicService.ReadSong = id => { ++reads; return Task.FromResult(new TestSong { ArtistId = "artist", Starred = "2026-11-17", UserRating = 5 }); };
        WP8SubsonicService.ReadArtist = id => Task.FromResult(new TestArtist { ImageUrl = "https://music.invalid/artist.jpg" });
        var page = Page(); page.AllowNext(); page.SetTrack(FeedbackTrack("song"));
        Check(page.MixMode && page.FeedbackEnabled && page.Starred && page.Rating == 5, "Mix mode reads existing server star/rating values before enabling feedback.");
        Check(page.BackgroundArtwork != null && page.BackgroundArtwork.UriSource.AbsoluteUri == "https://music.invalid/artist.jpg", "The player uses getArtistInfo's real artist image for its background.");
        var artistImage = page.BackgroundArtwork; artistImage.Fail();
        Check(page.BackgroundArtwork == null && page.Artwork.UriSource.AbsoluteUri == "https://music.invalid/cover.png", "An artist-image failure clears the backdrop without replacing album art or affecting playback.");
        string writtenId = null; bool writtenStar = true; var ratingWrites = new List<int>();
        WP8SubsonicService.WriteStar = (id, value) => { writtenId = id; writtenStar = value; return Task.FromResult(true); };
        WP8SubsonicService.WriteRating = (id, value) => { writtenId = id; ratingWrites.Add(value); return Task.FromResult(true); };
        await page.Star();
        Check(writtenId == "song" && !writtenStar && !page.Starred, "The star command toggles the selected song through the server API and changes UI only on success.");
        await page.Rate(5);
        Check(ratingWrites.Count == 1 && ratingWrites[0] == 5 && page.Rating == 5 && page.FeedbackVisible, "Like saves a real five-star server rating and shows the feedback panel.");
        Check(BackgroundMediaPlayer.Messages.Count == 0, "A successful like does not replace or skip the queue.");
        await page.Rate(1);
        Check(ratingWrites.Count == 2 && ratingWrites[1] == 1 && BackgroundMediaPlayer.Messages.Count == 1
            && BackgroundMediaPlayer.Messages[0].ContainsKey(BackgroundAudioMessages.Next), "Dislike saves a real one-star rating before issuing one Next command.");
        var failed = new TaskCompletionSource<bool>(); failed.SetException(new InvalidOperationException("server unavailable"));
        WP8SubsonicService.WriteRating = (id, value) => failed.Task;
        await page.Rate(5);
        Check(page.Rating == 1 && page.Status == "FeedbackFailed" && BackgroundMediaPlayer.Messages.Count == 1, "Failed feedback preserves the last confirmed rating and cannot skip audio.");

        page = Page(); page.AllowNext(); page.SetTrack(FeedbackTrack("old"));
        var pendingWrite = new TaskCompletionSource<bool>(); WP8SubsonicService.WriteRating = (id, value) => pendingWrite.Task;
        var lateRating = page.Rate(1);
        page.SetTrack(FeedbackTrack("new")); pendingWrite.SetResult(true); await lateRating;
        Check(page.Title == "new" && page.Rating == 5 && BackgroundMediaPlayer.Messages.Count == 0, "A rating completed after a track change cannot overwrite or skip the new track.");

        var pendingSong = new TaskCompletionSource<TestSong>();
        WP8SubsonicService.ReadSong = id => id == "delayed" ? pendingSong.Task : Task.FromResult(new TestSong { ArtistId = "current", UserRating = 4 });
        WP8SubsonicService.ReadArtist = id => Task.FromResult(new TestArtist { ImageUrl = "https://music.invalid/" + id + ".jpg" });
        page.SetTrack(FeedbackTrack("delayed")); page.SetTrack(FeedbackTrack("current"));
        pendingSong.SetResult(new TestSong { ArtistId = "obsolete", UserRating = 1 });
        await Task.Yield();
        Check(page.Title == "current" && page.Rating == 4 && page.BackgroundArtwork.UriSource.AbsoluteUri == "https://music.invalid/current.jpg", "Late enrichment for an older track cannot replace current rating or artist imagery.");
        reads = 0; WP8SubsonicService.ReadSong = id => { ++reads; return Task.FromResult(new TestSong()); };
        page.SetTrack(FeedbackTrack("another-account", "other-user"));
        Check(!page.FeedbackEnabled && reads == 0 && page.BackgroundArtwork == null, "A queue from another account cannot read or mutate the current account's feedback.");

        page = Page(); WP8SubsonicService.ReadSong = id => Task.FromResult(new TestSong { UserRating = 3 });
        page.SetTrack(FeedbackTrack("leaving")); pendingWrite = new TaskCompletionSource<bool>();
        WP8SubsonicService.WriteRating = (id, value) => pendingWrite.Task;
        var leavingWrite = page.Rate(1); page.Leave(); pendingWrite.SetResult(true); await leavingWrite;
        Check(page.Rating == 3 && BackgroundMediaPlayer.Messages.Count == 0, "A departed page ignores feedback completion and never sends Next after leaving.");
        page = Page(); var sharedTrack = FeedbackTrack("public title"); sharedTrack.StreamUrl = "https://music.invalid/stream?apiKey=private";
        page.SetTrack(sharedTrack);
        Check(page.ShareText() == "public title" + Environment.NewLine + "Artist", "Native sharing contains only track metadata and never authenticated stream URLs.");
        WP8SubsonicService.ReadLyrics = id => Task.FromResult(new List<TestLyric> { new TestLyric { Lines = new List<TestLine> { new TestLine { Text = "First line" }, new TestLine { Text = "Second line" } } } });
        await page.ReadLyrics();
        Check(page.Lyrics.Contains("First line") && page.Lyrics.Contains("Second line"), "The lyrics panel renders lines returned by the server.");
        var pendingLyrics = new TaskCompletionSource<List<TestLyric>>(); WP8SubsonicService.ReadLyrics = id => pendingLyrics.Task;
        var lateLyrics = page.ReadLyrics(); page.SetTrack(FeedbackTrack("following track"));
        pendingLyrics.SetResult(new List<TestLyric> { new TestLyric { Lines = new List<TestLine> { new TestLine { Text = "Obsolete lyrics" } } } });
        await lateLyrics;
        Check(page.Lyrics == string.Empty, "Lyrics arriving after a track change cannot appear over the following track.");
        WP8SubsonicService.ReadLyrics = id => Task.FromResult(new List<TestLyric>());
        await page.ReadLyrics();
        Check(page.Lyrics == "NoLyrics", "An empty server lyric response shows the localized empty state.");

        var artistReads = 0;
        WP8SubsonicService.ReadSong = id => Task.FromResult(new TestSong { ArtistId = "shared-artist" });
        WP8SubsonicService.ReadArtist = id => { ++artistReads; return Task.FromResult(new TestArtist { ImageUrl = "https://music.invalid/" + id + ".jpg" }); };
        page = Page(); page.SetTrack(FeedbackTrack("first")); var retainedPhoto = page.BackgroundArtwork;
        var nextSong = new TaskCompletionSource<TestSong>(); WP8SubsonicService.ReadSong = id => nextSong.Task;
        page.SetTrack(FeedbackTrack("next"));
        Check(ReferenceEquals(page.BackgroundArtwork, retainedPhoto), "The loaded artist photo remains visible while the next track's metadata is pending.");
        nextSong.SetResult(new TestSong { ArtistId = "shared-artist" }); await Task.Yield();
        Check(artistReads == 1 && ReferenceEquals(page.BackgroundArtwork, retainedPhoto), "The same stable artist ID reuses its decoded photo without an extra artwork request or black flash.");
        WP8SubsonicService.ReadSong = id => Task.FromResult(new TestSong { ArtistId = id });
        BitmapImage.AutomaticallyOpen = false;
        page.SetTrack(FeedbackTrack("another-artist")); var pendingPhoto = page.PendingArtistArtwork;
        Check(pendingPhoto != null && ReferenceEquals(page.BackgroundArtwork, retainedPhoto), "A new artist image keeps the previous photo visible until ImageOpened confirms decoding.");
        pendingPhoto.Open();
        Check(ReferenceEquals(page.BackgroundArtwork, pendingPhoto), "Successful decoding publishes the current artist's photo.");
        page.SetTrack(FeedbackTrack("stale-artist")); var stalePhoto = page.PendingArtistArtwork;
        page.SetTrack(FeedbackTrack("latest-artist")); var latestPhoto = page.PendingArtistArtwork;
        page.DeliverArtistOpened(stalePhoto);
        Check(ReferenceEquals(page.BackgroundArtwork, pendingPhoto), "A queued ImageOpened from an obsolete track cannot publish the obsolete image.");
        latestPhoto.Open(); page.SetTrack(FeedbackTrack("another-user", "other-user"));
        Check(page.BackgroundArtwork == null, "A different playback account clears the previous artist photo immediately.");
        BitmapImage.AutomaticallyOpen = true;
    }
}

namespace Mixdrome.Phone.Views
{
    public sealed class PlaybackPage : Page
    {
        private readonly Dictionary<string, object> Resources = new Dictionary<string, object> { { "PlayerFeedbackNeutralBrush", new Windows.UI.Xaml.Media.Brush() } };
        private const string DefaultArtworkUri = "ms-appx:///Assets/MixRadio/DefaultArtwork300.png";
        private readonly DispatcherTimer progressTimer = new DispatcherTimer();
        private readonly AudioStateVersionGate stateGate = new AudioStateVersionGate();
        private TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
        private CancellationTokenSource activation = new CancellationTokenSource();
        private bool active = true, updatingProgress, seeking, shuffle, canNext, playbackFailed;
        private int repeat, currentIndex = -1;
        private string queueVersion = string.Empty, latestVersion = string.Empty;
        private MediaPlayer player = BackgroundMediaPlayer.Current;
        private TrackInfo displayedTrack;
        private BitmapImage artworkImage;
        private ItemCollection observedQueueItems;
        private bool queueListLoaded;
        private bool isMix, showingMixDetails, feedbackBusy, feedbackAvailable, songStarred, offlineRequestBusy;
        private int songRating;
        private BitmapImage artistImage;
        private BitmapImage pendingArtistImage;
        private TrackInfo pendingArtistTrack;
        private CancellationTokenSource pendingArtistActivation;
        private string pendingArtistId, loadedArtistId;
        private PhoneReportingProfile loadedArtistAccount;
        private DataTransferManager shareManager;
        private readonly TextBlock MixSongTitleText = new TextBlock(), MixArtistText = new TextBlock(), FeedbackTitleText = new TextBlock(), FeedbackMessageText = new TextBlock(), LyricsText = new TextBlock();
        private readonly Control LikeButton = new Control(), DislikeButton = new Control(), FavouriteButton = new Control(), FeedbackOverlay = new Control(), FeedbackIcon = new Control(), ShareButton = new Control(), LyricsButton = new Control(), LyricsOverlay = new Control();
        private readonly Control SaveOfflineMixMenu = new Control();
        private readonly CommandBar PlayerCommandBar = new CommandBar();
        private readonly PhoneTitleBar PlayerTitleBar = new PhoneTitleBar();
        public ObservableCollection<TrackInfo> QueueItems { get; private set; } = new ObservableCollection<TrackInfo>();
        private readonly TextBlock SongTitleText = new TextBlock(), AboutTitleText = new TextBlock(), ArtistText = new TextBlock(), AboutArtistText = new TextBlock(), AboutAlbumText = new TextBlock(), PlaybackStatusText = new TextBlock(), PlayPauseGlyph = new TextBlock(), CurrentTimeText = new TextBlock(), TotalTimeText = new TextBlock();
        private readonly Control PreviousButton = new Control(), PlayPauseButton = new Control(), NextButton = new Control(), ShuffleButton = new Control(), RepeatButton = new Control(), RepeatOneBadge = new Control();
        private readonly Slider ProgressSlider = new Slider();
        private readonly ListView QueueListView = new ListView();
        private readonly Image AlbumArtImage = new Image(), MixAlbumArtImage = new Image(), BackgroundImage = new Image(), BackgroundPreloadImage = new Image();
        public PlaybackPage() { ResetDisplay(); }
        public Task Accept(ValueSet data) { return ApplyStateAsync(data); }
        public void Leave() { OnNavigatedFrom(new NavigationEventArgs()); }
        public void ActivateBack() { OnNavigatedTo(new NavigationEventArgs { NavigationMode = NavigationMode.Back }); }
        public void Poll() { OnProgressTimerTick(null, null); }
        public void ResetView() { ResetDisplay(); }
        public void DeliverImageFailure(BitmapImage image) { OnArtworkImageFailed(image, new ExceptionRoutedEventArgs()); }
        public void LoadQueue() { QueueListView.IsLoaded = true; OnQueueListLoaded(QueueListView, new RoutedEventArgs()); }
        public void UnloadQueue() { OnQueueListUnloaded(QueueListView, new RoutedEventArgs()); QueueListView.IsLoaded = false; }
        public void PublishQueue() { QueueListView.Items.Replace(QueueItems); }
        public void PublishUnrelatedDuplicates() { QueueListView.Items.Replace(new[] { new TrackInfo { Id = "duplicate" }, new TrackInfo { Id = "duplicate" } }); }
        public string Title { get { return SongTitleText.Text; } }
        public string Artist { get { return ArtistText.Text; } }
        public string Album { get { return AboutAlbumText.Text; } }
        public string Status { get { return PlaybackStatusText.Text; } }
        public bool Failed { get { return playbackFailed; } }
        public int SelectedIndex { get { return QueueListView.SelectedIndex; } }
        public int SelectionWrites { get { return QueueListView.SelectionWrites; } }
        public object SelectedQueueObject { get { return SelectedIndex >= 0 && SelectedIndex < QueueListView.Items.Count ? QueueListView.Items[SelectedIndex] : null; } }
        public bool PlayEnabled { get { return PlayPauseButton.IsEnabled; } }
        public bool NextEnabled { get { return NextButton.IsEnabled; } }
        public bool SeekEnabled { get { return ProgressSlider.IsEnabled; } }
        public double Progress { get { return ProgressSlider.Value; } }
        public string Elapsed { get { return CurrentTimeText.Text; } }
        public string Duration { get { return TotalTimeText.Text; } }
        public bool TimerRunning { get { return progressTimer.IsEnabled; } }
        public BitmapImage Artwork { get { return AlbumArtImage.Source; } }
        public BitmapImage BackgroundArtwork { get { return BackgroundImage.Source; } }
        public bool FeedbackEnabled { get { return LikeButton.IsEnabled; } }
        public bool FeedbackVisible { get { return FeedbackOverlay.Visibility == Visibility.Visible; } }
        public bool Starred { get { return songStarred; } }
        public int Rating { get { return songRating; } }
        public bool MixMode { get { return isMix; } }
        public Task Rate(int rating) { return SaveFeedbackAsync(false, rating); }
        public Task Star() { return SaveFeedbackAsync(true, 0); }
        public static bool SameAccount(PhoneReportingProfile left, PhoneReportingProfile right) { return MatchesPlaybackAccount(left, right); }
        public bool OfflineEnabled { get { return SaveOfflineMixMenu.IsEnabled; } }
        public Task SaveOffline() { return StartOfflineMixAsync(); }
        public void OfflineQueue(params TrackInfo[] tracks) { QueueItems.Clear(); foreach (var track in tracks) QueueItems.Add(track); queueVersion = "offline-queue"; currentIndex = 0; ShowTrack(tracks[0]); UpdateFeedbackControls(); }
        public void ChangeQueueVersion() { queueVersion = "replacement"; }
        public void SetTrack(TrackInfo track) { ShowTrack(track); }
        public void AllowNext() { canNext = true; }
        public static async Task<PhoneReportingProfile> PrepareLocalQueue() { using (var service = await CreateLocalQueueServiceAsync(new List<SongDisplayItem> { new SongDisplayItem { Id = "saved" } })) return service.CreateReportingProfile(); }
        public string Lyrics { get { return LyricsText.Text; } }
        public Task ReadLyrics() { return LoadLyricsAsync(); }
        public string ShareText() { var args = new DataRequestedEventArgs(); OnShareDataRequested(null, args); return args.Request.Data.Text; }
        private void UpdatePlayerLayout() { }
        private void OnVisibleBoundsChanged(ApplicationView sender, object args) { }
        public BitmapImage PendingArtistArtwork { get { return pendingArtistImage; } }
        public void DeliverArtistOpened(BitmapImage image) { OnArtistImageOpened(image, new RoutedEventArgs()); }
        public int QueueStartCalls;
        private Task StartQueueAsync(PlaybackNavigationParameter parameter, CancellationToken token) { ++QueueStartCalls; return Task.FromResult(true); }
        private void OnBackPressed(object sender, BackPressedEventArgs args) { }
        private void OnMessage(object sender, MediaPlayerDataReceivedEventArgs args) { }
        private void OnMediaStateChanged(MediaPlayer sender, object args) { }
/* PRODUCTION_PAGE_METHODS */
    }
    public sealed class PlaybackNavigationParameter { public List<SongDisplayItem> Playlist; public int StartIndex; }
    public sealed class PhoneOfflineRequest { public TestPlaybackConfiguration Configuration; public PhoneMixDefinition Mix; }
    public sealed class OfflineMixesPage { }
    public sealed class SongDisplayItem { public string Id; }
}
namespace Mixdrome.Phone.Common { public static class PhoneText { public static string Get(string key) { return key; } public static string Format(string key, params object[] values) { return key; } } public class PhoneTitleBar { public string Title; } }
namespace Windows.Foundation.Collections
{
    public sealed class ValueSet : Dictionary<string, object> { }
    public interface IObservableVector<T> { }
    public interface IVectorChangedEventArgs { }
    public delegate void VectorChangedEventHandler<T>(IObservableVector<T> sender, IVectorChangedEventArgs args);
}
namespace Windows.Storage
{
    public sealed class StorageFile { }
    public sealed class StorageFolder { public Task<StorageFile> GetFileAsync(string name) { return Task.FromResult(new StorageFile()); } }
    public sealed class ApplicationData { public static readonly ApplicationData Current = new ApplicationData(); public readonly StorageFolder LocalFolder = new StorageFolder(); }
    public static class FileIO { public static Func<Task<string>> Read; public static Task<string> ReadTextAsync(StorageFile file) { return Read(); } }
}
namespace Windows.Media.Playback
{
    public enum MediaPlayerState { Playing, Paused, Stopped, Closed, Opening, Buffering }
    public sealed class MediaPlayer
    {
        public MediaPlayerState CurrentState;
        public Func<TimeSpan> DurationRead;
        private TimeSpan duration, position;
        private bool canSeek;
        public int ProgressReads;
        private void ReadProgress()
        {
            ++ProgressReads;
            if (CurrentState == MediaPlayerState.Closed || CurrentState == MediaPlayerState.Opening || CurrentState == MediaPlayerState.Buffering)
                throw new COMException("Native media properties are not initialized.", unchecked((int)0x80004005));
        }
        public TimeSpan NaturalDuration { get { ReadProgress(); return DurationRead == null ? duration : DurationRead(); } set { duration = value; } }
        public TimeSpan Position { get { ReadProgress(); return position; } set { position = value; } }
        public bool CanSeek { get { ReadProgress(); return canSeek; } set { canSeek = value; } }
        public void ClearReadCounts() { ProgressReads = 0; }
        public void Reset() { CurrentState = MediaPlayerState.Stopped; DurationRead = null; duration = position = TimeSpan.Zero; canSeek = false; ClearReadCounts(); }
        public event Action<MediaPlayer, object> CurrentStateChanged { add { } remove { } }
    }
    public sealed class MediaPlayerDataReceivedEventArgs : EventArgs { }
    public static class BackgroundMediaPlayer
    {
        public static readonly MediaPlayer Current = new MediaPlayer();
        public static readonly List<ValueSet> Messages = new List<ValueSet>();
        public static Action<ValueSet> Command;
        public static void SendMessageToBackground(ValueSet data) { Messages.Add(data); if (Command != null) Command(data); }
        public static event EventHandler<MediaPlayerDataReceivedEventArgs> MessageReceivedFromBackground { add { } remove { } }
    }
}
namespace Windows.Phone.UI.Input
{
    public sealed class BackPressedEventArgs : EventArgs { }
    public static class HardwareButtons { public static event EventHandler<BackPressedEventArgs> BackPressed { add { } remove { } } }
}
namespace Windows.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public sealed class RoutedEventArgs : EventArgs { }
    public sealed class ExceptionRoutedEventArgs : EventArgs { }
    public delegate void ExceptionRoutedEventHandler(object sender, ExceptionRoutedEventArgs args);
    public sealed class DispatcherTimer { public bool IsEnabled; public void Start() { IsEnabled = true; } public void Stop() { IsEnabled = false; } }
    public sealed class Application
    {
        public static readonly Application Current = new Application();
        public readonly Dictionary<string, object> Resources = new Dictionary<string, object> { { "MixRadioAccentBrush", new Media.Brush() }, { "ApplicationForegroundThemeBrush", new Media.Brush() }, { "MixRadioPanelBrush", new Media.Brush() } };
    }
}
namespace Windows.UI.Xaml.Controls
{
    public class Control { public bool IsEnabled; public Visibility Visibility = Visibility.Collapsed; public Media.Brush Foreground, Background; public object RenderTransform; }
    public class CommandBar : Control { public bool IsOpen; }
    public sealed class Frame { public int Navigations; public object Parameter; public Type Destination; public bool Navigate(Type type, object parameter) { ++Navigations; Destination = type; Parameter = parameter; return true; } }
    public class Page { public readonly Frame Frame = new Frame(); protected virtual void OnNavigatedTo(NavigationEventArgs args) { } protected virtual void OnNavigatedFrom(NavigationEventArgs args) { } }
    public sealed class TextBlock : Control { public string Text = string.Empty; }
    public sealed class Slider : Control { public double Maximum, Value; }
    public sealed class ItemCollection : List<object>, IObservableVector<object>
    {
        public event VectorChangedEventHandler<object> VectorChanged;
        public void Replace(IEnumerable<object> items)
        {
            Clear(); AddRange(items);
            if (VectorChanged != null) VectorChanged(this, null);
        }
    }
    public sealed class ListView : Control
    {
        public bool IsLoaded;
        public int SelectionWrites;
        public readonly ItemCollection Items = new ItemCollection();
        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                ++SelectionWrites;
                if (!IsLoaded || value < -1 || value >= Items.Count) throw new ArgumentException("The queue view is not ready for this selected index.");
                selectedIndex = value;
            }
        }
    }
    public sealed class Image { public BitmapImage Source; }
}
namespace Windows.UI.Xaml.Media { public class Brush { } public class RotateTransform { public double Angle, CenterX, CenterY; } }
namespace Windows.UI.Xaml.Media.Imaging
{
    public sealed class BitmapImage
    {
        public static bool AutomaticallyOpen = true;
        private Uri uri;
        public Uri UriSource { get { return uri; } set { uri = value; if (AutomaticallyOpen) Open(); } }
        private ExceptionRoutedEventHandler failed;
        private EventHandler<RoutedEventArgs> opened;
        public BitmapImage() { }
        public BitmapImage(Uri uri) { UriSource = uri; }
        public event ExceptionRoutedEventHandler ImageFailed { add { failed += value; } remove { failed -= value; } }
        public event EventHandler<RoutedEventArgs> ImageOpened { add { opened += value; } remove { opened -= value; } }
        public void Open() { if (opened != null) opened(this, new RoutedEventArgs()); }
        public int FailureSubscribers { get { return failed == null ? 0 : failed.GetInvocationList().Length; } }
        public void Fail() { if (failed != null) failed(this, new ExceptionRoutedEventArgs()); }
    }
}
namespace Windows.UI.Xaml.Automation { public static class AutomationProperties { public static void SetName(Control control, string name) { } } }
namespace Windows.UI.Xaml.Navigation
{
    public enum NavigationMode { New, Back }
    public sealed class NavigationEventArgs : EventArgs { public NavigationMode NavigationMode; public object Parameter; }
}

namespace Mixdrome.Phone.Services
{
    public sealed class TestPlaybackConfiguration { public string BaseUrl, Username, AuthenticationMode; }
    public static class PhoneOfflineMixes
    {
        public static Func<IEnumerable<SongDisplayItem>, Task<TestPlaybackConfiguration>> Resolve;
        public static Task<TestPlaybackConfiguration> ResolveLocalQueueConfigurationAsync(IEnumerable<SongDisplayItem> songs) { return Resolve(songs); }
    }
    public sealed class PhoneMixDefinition { public string Id, Name; public bool IsPlayMe; public IList<Song> Songs; }
    public sealed class TestSong { public string ArtistId, Starred; public int UserRating; }
    public sealed class TestArtist { public string ImageUrl; }
    public sealed class WP8SubsonicService : IDisposable
    {
        public static PhoneReportingProfile Profile = new PhoneReportingProfile { BaseUrl = "https://music.invalid", Username = "listener", AuthenticationMode = "Password" };
        public static Func<string, Task<TestSong>> ReadSong = id => Task.FromResult(new TestSong());
        public static Func<string, Task<TestArtist>> ReadArtist = id => Task.FromResult(new TestArtist());
        public static Func<string, bool, Task<bool>> WriteStar = (id, value) => Task.FromResult(true);
        public static Func<string, int, Task<bool>> WriteRating = (id, value) => Task.FromResult(true);
        private TestPlaybackConfiguration configuration;
        public void Configure(TestPlaybackConfiguration value) { configuration = value; }
        public PhoneReportingProfile CreateReportingProfile() { return configuration == null ? Profile : new PhoneReportingProfile { BaseUrl = configuration.BaseUrl, Username = configuration.Username, AuthenticationMode = configuration.AuthenticationMode }; }
        public Task<TestSong> GetSongAsync(string id) { return ReadSong(id); }
        public Task<TestArtist> GetArtistInfoAsync(string id) { return ReadArtist(id); }
        public Task<bool> SetSongStarredAsync(string id, bool value) { return WriteStar(id, value); }
        public Task<bool> SetSongRatingAsync(string id, int value) { return WriteRating(id, value); }
        public static Func<string, Task<List<TestLyric>>> ReadLyrics = id => Task.FromResult(new List<TestLyric>());
        public Task<List<TestLyric>> GetSongLyricsAsync(string id, string artist, string title) { return ReadLyrics(id); }
        public void Dispose() { }
    }
    public sealed class TestLyric { public List<TestLine> Lines = new List<TestLine>(); }
    public sealed class TestLine { public string Text; }
    public static class PhoneSubsonicServiceFactory
    {
        public static Func<Task<TestPlaybackConfiguration>> Load = () => Task.FromResult(new TestPlaybackConfiguration { BaseUrl = "https://music.invalid", Username = "listener", AuthenticationMode = "Password" });
        public static Task<TestPlaybackConfiguration> LoadConfigurationAsync() { return Load(); }
        public static Task<WP8SubsonicService> CreatePlaybackServiceAsync() { return Task.FromResult(new WP8SubsonicService()); }
    }
}

namespace Windows.ApplicationModel.DataTransfer
{
    public sealed class DataTransferManager { public event Action<DataTransferManager, DataRequestedEventArgs> DataRequested { add { } remove { } } }
    public sealed class DataRequestedEventArgs { public readonly DataRequest Request = new DataRequest(); }
    public sealed class DataRequest { public readonly DataPackage Data = new DataPackage(); public void FailWithDisplayText(string message) { } }
    public sealed class DataPackage { public string Text; public readonly DataProperties Properties = new DataProperties(); public void SetText(string text) { Text = text; } }
    public sealed class DataProperties { public string Title; }
}

namespace Windows.UI.ViewManagement
{
    public sealed class ApplicationView
    {
        private static readonly ApplicationView current = new ApplicationView();
        public static ApplicationView GetForCurrentView() { return current; }
        public event Action<ApplicationView, object> VisibleBoundsChanged { add { } remove { } }
    }
}

namespace Client.Common.Models.Subsonic
{
    public sealed class Song { public string Id, Title, Artist, Album, CoverArt; public int Duration; }
}

namespace Client.Common.Services.DataStructures.SubsonicService
{
    // AccountIdentity is compiled from production source; the CLR shell only
    // supplies the two configuration properties that identity reads.
    public interface ISubsonicServiceConfiguration { string BaseUrl { get; set; } string Username { get; set; } }
    public sealed class SubsonicServiceConfiguration : ISubsonicServiceConfiguration
    { public string BaseUrl { get; set; } public string Username { get; set; } }
}
