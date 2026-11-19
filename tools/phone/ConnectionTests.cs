using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.Common.Services;
using Client.Common.Services.DataStructures.SubsonicService;
using Client.Common.Services.Navidrome;
using Mixdrome.Phone.Services;
using Windows.Security.Credentials;
using Windows.Storage;

// Production phone settings/factory/adapter tests. Vault and settings adapters exercise
// migration/error rules; they do not certify WinRT encryption or native media decoding.
public static class PhoneConnectionTests
{
    private static int _assertions;
    private static void Check(bool condition, string message)
    { ++_assertions; if (!condition) throw new Exception(message); }
    private static string Response(string body, bool success = true)
    { return "<subsonic-response xmlns='http://subsonic.org/restapi' status='" + (success ? "ok" : "failed") + "' version='1.16.1'>" + body + "</subsonic-response>"; }
    private static void Reset()
    { ApplicationData.Current.LocalSettings.Values.Clear(); PasswordVault.Credentials.Clear(); PasswordVault.FailNextAdd = false; }
    private static SubsonicServiceConfiguration Config(string url = "https://music.example.com/")
    { return new SubsonicServiceConfiguration { PrimaryUrl = url, BaseUrl = url, Username = "fixture", Password = "secret" }; }
    private static void Reject(Action action, string message)
    { var rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, message); }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + _assertions + " production phone settings, credentials, route and adapter assertions. Native vault/UI/audio remain unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        Reset();
        Check(!PhoneSettings.HasServer(await PhoneSubsonicServiceFactory.LoadConfigurationAsync()), "First launch does not attempt a network request without a server.");
        var unconfigured = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync();
        var configurationRequired = false;
        try { await unconfigured.PingAsync(); } catch (InvalidOperationException exception) { configurationRequired = exception.Message.StartsWith("ConfigureServer"); }
        Check(configurationRequired, "Unconfigured playback reports a controlled configuration status.");
        var settings = ApplicationData.Current.LocalSettings.Values;
        settings["BaseUrl"] = "https://music.example.com/"; settings["Username"] = "fixture"; settings["Password"] = "legacy&音";
        PasswordVault.FailNextAdd = true;
        var migrationFailed = false;
        try { PhoneSettings.Load(); } catch (InvalidOperationException) { migrationFailed = true; }
        Check(migrationFailed && (string)settings["Password"] == "legacy&音", "A failed migration preserves the legacy secret for recovery.");
        var loaded = PhoneSettings.Load();
        Check(loaded.Password == "legacy&音" && loaded.PrimaryUrl == settings["BaseUrl"].ToString(), "Legacy BaseUrl and credentials migrate without changing account data.");
        Check(!settings.ContainsKey("Password") && PasswordVault.Credentials.Single().Password == "legacy&音", "Plaintext is removed only after successful vault migration.");
        Check(PhoneSettings.Load().Password == loaded.Password, "Subsequent loads retrieve the migrated vault credential.");
        var update = Config(); update.Password = "replacement";
        PasswordVault.FailNextAdd = true;
        var updateFailed = false;
        try { PhoneSettings.Save(update); } catch (InvalidOperationException) { updateFailed = true; }
        Check(updateFailed && PhoneSettings.Load().Password == "legacy&音", "A failed replacement restores the prior vault credential.");
        update.Username = "other"; update.Password = "key"; update.AuthenticationMode = SubsonicAuthenticationMode.ApiKey;
        update.StreamingFormat = "raw"; update.MaximumBitRate = 192;
        PhoneSettings.Save(update); loaded = PhoneSettings.Load();
        Check(loaded.Username == "other" && loaded.Password == "key" && PasswordVault.Credentials.Count == 1, "Changing accounts commits the new credential before removing the old one.");
        Check(loaded.AuthenticationMode == SubsonicAuthenticationMode.ApiKey && loaded.StreamingFormat == "raw" && loaded.MaximumBitRate == 192, "Phone configuration retains authentication and streaming options.");
        Check(!settings.ContainsKey("Password") && !settings.Values.Contains("key"), "Settings storage contains no new password or API key.");
        foreach (var invalid in new[] { "ftp://music.example.com", "https://user:pass@music.example.com", "https://music.example.com/?token=secret", "https://music.example.com/#fragment" })
        { var config = Config(invalid); Reject(() => PhoneSettings.Validate(config), "Unsupported or credential-bearing addresses are rejected: " + invalid); }
        var missing = Config(); missing.PrimaryUrl = null; Reject(() => PhoneSettings.Validate(missing), "At least one route is required.");
        missing = Config(); missing.Password = ""; Reject(() => PhoneSettings.Validate(missing), "An account secret is required.");
        missing = Config(); missing.MaximumBitRate = -1; Reject(() => PhoneSettings.Validate(missing), "Negative bitrates are rejected.");
        var normalized = Config("music.example.com/navidrome"); PhoneSettings.Validate(normalized);
        Check(normalized.PrimaryUrl == "http://music.example.com/navidrome/", "A bare address is normalized while preserving its server path.");

        var owner = "fixture"; var fail = false; var enhancedLyrics = true; var externalArtistImage = true;
        using (var bad = new ProtocolServer(request => new ProtocolResponse { Body = Response("<error code='40' message='Denied'/>", false) }))
        using (var good = new ProtocolServer(request =>
        {
            var path = request.Uri.AbsolutePath;
            if (path.EndsWith("tokenInfo.view")) return new ProtocolResponse { Body = Response("<tokenInfo username='" + owner + "'/>") };
            if (fail) return new ProtocolResponse { Body = Response("<error code='40' message='Denied'/>", false) };
            if (path.EndsWith("getOpenSubsonicExtensions.view")) return new ProtocolResponse { Body = enhancedLyrics ? Response("<openSubsonicExtensions name='songLyrics'><versions>1</versions></openSubsonicExtensions>") : Response("<error code='70' message='Not implemented'/>", false) };
            if (path.EndsWith("getLyricsBySongId.view")) return new ProtocolResponse { Body = Response("<lyricsList><structuredLyrics lang='en' synced='false'><line>Structured line</line></structuredLyrics></lyricsList>") };
            if (path.EndsWith("getLyrics.view")) return new ProtocolResponse { Body = Response("<lyrics artist='Artist' title='Track'>Legacy line</lyrics>") };
            if (path.EndsWith("getArtistInfo2.view")) return new ProtocolResponse { Body = Response(externalArtistImage ? "<artistInfo2><biography>Real biography</biography><largeImageUrl>https://images.invalid/artist.jpg</largeImageUrl></artistInfo2>" : "<artistInfo2><biography>Real biography</biography><largeImageUrl></largeImageUrl></artistInfo2>") };
            if (path.EndsWith("getAlbum.view")) return new ProtocolResponse { Body = Response("<album id='album' name='Album' artist='Artist'><song id='song&amp;音' title='Track' artist='Artist' album='Album' track='7' duration='125' bitRate='192' contentType='audio/flac' /></album>") };
            if (path.EndsWith("getStarred2.view")) return new ProtocolResponse { Body = Response("<starred2><song id='starred' title='Starred' duration='30' /></starred2>") };
            if (path.EndsWith("getInternetRadioStations.view")) return new ProtocolResponse { Body = Response("<internetRadioStations><internetRadioStation id='radio' name='Station' streamUrl='https://radio.example.com/' homePageUrl='https://example.com/' /></internetRadioStations>") };
            if (path.EndsWith("getNowPlaying.view")) return new ProtocolResponse { Body = Response("<nowPlaying><entry id='playing' title='Playing' duration='65' username='listener' playerId='2' minutesAgo='1' /></nowPlaying>") };
            if (path.EndsWith("getArtist.view")) return new ProtocolResponse { Body = Response("<artist id='artist' name='Artist' artistImageUrl='https://images.invalid/local-artist.jpg'><album id='album' name='Album' artist='Artist' year='2005' songCount='8' /></artist>") };
            return new ProtocolResponse { Body = Response("") };
        }))
        {
            Reset(); var configuration = Config(bad.Url); configuration.SecondaryUrl = good.Url;
            configuration.Password = "password&音"; configuration.StreamingFormat = "raw"; configuration.MaximumBitRate = 192;
            PhoneSettings.Save(configuration);
            var service = await PhoneSubsonicServiceFactory.CreateAsync();
            Check(service.Configuration.BaseUrl == good.Url, "A 200 authentication failure cannot select a route over a valid peer.");
            var adapter = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync();
            Check(await adapter.PingAsync(), "Phone ping uses the shared result status contract.");
            var albumSongs = await adapter.GetAlbumSongsAsync("album");
            Check(albumSongs.Single().Track == 7 && albumSongs.Single().ContentType == "audio/flac" && albumSongs.Single().Duration == 125, "Phone album reading retains production metadata beyond title/artwork.");
            Check((await adapter.GetStarredSongsAsync()).Single().Id == "starred", "Starred songs use the common model parser.");
            Check((await adapter.GetAlbumsAsync("artist")).Single().Year == 2005, "Artist albums retain year and song counts.");
            Check((await adapter.GetInternetRadioStationsAsync()).Single().StreamUrl == "https://radio.example.com/", "Radio adapts server-defined streams.");
            Check((await adapter.GetNowPlayingAsync()).Single().Song.Duration == 65, "Listener entries preserve the full common song model.");
            Check((await adapter.GetArtistInfoAsync("artist&音")).ImageUrl == "https://images.invalid/artist.jpg", "The phone reads the server's real large artist image through the shared protocol.");
            externalArtistImage = false;
            var localArtist = await adapter.GetArtistInfoAsync("artist&音");
            Check(localArtist.ImageUrl == "https://images.invalid/local-artist.jpg" && localArtist.Biography == "Real biography", "Empty getArtistInfo artwork falls back to the actual OpenSubsonic artistImageUrl while retaining other metadata.");
            await adapter.SetSongStarredAsync("song&音", true); await adapter.SetSongStarredAsync("song&音", false); await adapter.SetSongRatingAsync("song&音", 5);
            Check(good.Requests.Where(request => request.Uri.AbsolutePath.EndsWith("star.view") || request.Uri.AbsolutePath.EndsWith("unstar.view") || request.Uri.AbsolutePath.EndsWith("setRating.view"))
                .All(request => request.Method == "POST" && request.Parameter("id") == "song&音"), "Feedback uses real form POST star, unstar and rating requests for the escaped song ID.");
            Check((await adapter.GetSongLyricsAsync("song&音", "Artist", "Track")).Single().Lines.Single().Text == "Structured line", "Advertised songLyrics capability selects structured song-ID lyrics.");
            enhancedLyrics = false;
            Check((await adapter.GetSongLyricsAsync("song&音", "Artist", "Track")).Single().Lines.Single().Text == "Legacy line", "An unsupported extension-discovery endpoint falls back to the legacy lyrics protocol.");
            var stream = new ProtocolRequest { Target = adapter.GetStreamUri("song&音").PathAndQuery, Headers = new Dictionary<string, string>() };
            Check(stream.Uri.AbsolutePath.EndsWith("stream.view") && stream.Parameter("id") == "song&音" && stream.Parameter("format") == "raw" && stream.Parameter("maxBitRate") == "192", "Phone stream requests escape IDs and apply explicit streaming settings.");
            Check(stream.Parameter("c") == SubsonicRequest.ClientName && stream.Parameter("v") == SubsonicRequest.ProtocolVersion && stream.Parameter("p") == service.Configuration.EncodedPassword, "Phone playback shares the PC client identity/protocol and encoded credential contract.");
            adapter.CompatibleMode = true;
            stream.Target = adapter.GetStreamUri("song").PathAndQuery;
            Check(stream.Parameter("format") == "mp3" && stream.Parameter("maxBitRate") == "192", "Compatibility playback uses the configured bitrate.");
            var artwork = new ProtocolRequest { Target = new Uri(adapter.GetCoverArtUrl("cover&音", 480)).PathAndQuery, Headers = new Dictionary<string, string>() };
            Check(artwork.Parameter("id") == "cover&音" && artwork.Parameter("size") == "480", "Artwork shares escaped protocol values.");
            fail = true; var errorReported = false;
            try { await adapter.GetStarredSongsAsync(); } catch (Exception) { errorReported = true; }
            Check(errorReported, "A failed request is not presented as an empty music library.");
            fail = false; configuration.PrimaryUrl = good.Url; configuration.SecondaryUrl = null;
            configuration.AuthenticationMode = SubsonicAuthenticationMode.ApiKey; configuration.Password = "api-key&音";
            PhoneSettings.Save(configuration); adapter = await PhoneSubsonicServiceFactory.CreatePlaybackServiceAsync();
            stream.Target = adapter.GetStreamUri("song").PathAndQuery;
            Check(stream.Parameter("apiKey") == configuration.Password && stream.Parameter("p") == null && stream.Parameter("u") == null, "Phone API keys are not mixed with password authentication.");
            owner = "another"; var mismatchRejected = false;
            try { await PhoneSubsonicServiceFactory.CreateAsync(); } catch (InvalidOperationException) { mismatchRejected = true; }
            Check(mismatchRejected, "A key for another user cannot open the requested account.");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); var cancelled = false;
                try { await PhoneSubsonicServiceFactory.LoadConfigurationAsync(cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "External route cancellation remains cancellation.");
            }
        }
    }
}

namespace Windows.Storage
{
    public sealed class ApplicationData
    {
        public static readonly ApplicationData Current = new ApplicationData();
        public ApplicationDataContainer LocalSettings = new ApplicationDataContainer();
#if PHONE_LISTENING_TEST
        public StorageFolder LocalFolder;
#endif
    }
    public sealed class ApplicationDataContainer { public Dictionary<string, object> Values = new Dictionary<string, object>(); }
}
namespace Windows.Security.Credentials
{
    public sealed class PasswordCredential
    {
        public PasswordCredential(string resource, string username, string password)
        { Resource = resource; UserName = username; Password = password; }
        public string Resource, UserName, Password;
        public void RetrievePassword() { }
    }
    public sealed class PasswordVault
    {
        public static readonly List<PasswordCredential> Credentials = new List<PasswordCredential>();
        public static bool FailNextAdd;
        public PasswordCredential[] RetrieveAll() { return Credentials.ToArray(); }
        public void Add(PasswordCredential credential)
        {
            if (FailNextAdd) { FailNextAdd = false; throw new InvalidOperationException("Injected vault write failure"); }
            if (Credentials.Any(item => item.Resource == credential.Resource && item.UserName == credential.UserName)) throw new InvalidOperationException("Duplicate vault credential");
            Credentials.Add(credential);
        }
        public void Remove(PasswordCredential credential) { Credentials.Remove(credential); }
    }
}
namespace Client.Common.Services
{
    public sealed class NetworkDetectionService : INetworkDetectionService
    {
        public event EventHandler<NetworkConnectionType> NetworkChanged { add { } remove { } }
        public NetworkConnectionType GetCurrentConnectionType() { return NetworkConnectionType.WiFi; }
    }
}
namespace Mixdrome.Phone.Common
{
    public static class PhoneText
    {
        public static string Get(string key) { return key + ": {0}"; }
        public static string Format(string key, params object[] values) { return string.Format(Get(key), values); }
    }
}
