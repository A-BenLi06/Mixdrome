using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Client.Common.Models.Subsonic;
using Client.Common.Services;
using Client.Common.Services.DataStructures.SubsonicService;
using Client.Common.Services.Navidrome;
using Mixdrome.Phone.Services;
using Mixdrome.Phone.Views;
using Windows.Storage;

public static class PhoneOfflineMixTests
{
    private static int assertions;
    private static void Check(bool condition, string name) { assertions++; if (!condition) throw new Exception(name); }
    public static int Main(string[] args)
    {
        try { Run(args[0]).GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production phone offline-mix assertions with CLR storage adapters. Native storage/audio/UI remain unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run(string output)
    {
        ApplicationData.Current.UseRoot(output);
        var deny = false; var badMedia = false;
        Func<ProtocolRequest, ProtocolResponse> respond = request =>
        {
            var endpoint = request.Uri.AbsolutePath;
            if (endpoint.Contains("getUser")) return Xml("<user username='fixture' downloadRole='" + (!deny).ToString().ToLowerInvariant() + "' streamRole='true'/>");
            if (endpoint.Contains("getSong")) return Xml("<song id='" + request.Parameter("id") + "' title='Song' artist='Artist' album='Album' duration='6' artistId='artist-id' albumId='album-id'/>");
            if (endpoint.Contains("stream")) return badMedia ? Xml("<error code='0' message='no stream'/>") :
                new ProtocolResponse { ContentType = "audio/mpeg", Body = "ID3" + new string('x', 65536) };
            return Xml("<error code='70' message='unhandled fixture request'/>");
        };
        using (var server = new ProtocolServer(respond))
        {
            var configuration = new SubsonicServiceConfiguration { BaseUrl = server.Url, PrimaryUrl = server.Url, Username = "fixture", Password = "private-fixture", StreamingFormat = "flac", MaximumBitRate = 128 };
            PhoneSettings.Configuration = configuration;
            var definition = new PhoneMixDefinition { Id = "playlist:mix-id", Name = "Fixture mix", Songs = new List<Song> { new Song { Id = "one" }, new Song { Id = "one" }, new Song { Id = "two" } } };
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0, "Empty local catalog has no available mixes.");
            var saved = await PhoneOfflineMixes.SaveMixAsync(configuration, definition, null, CancellationToken.None);
            Check(saved.Manifest.Catalog.Songs.Count == 2, "Duplicate playlist occurrences are downloaded once in first-occurrence order.");
            Check(saved.Manifest.SchemaVersion == 2 && saved.Manifest.QueueIds.SequenceEqual(new[] { "one", "one", "two" }), "Version 2 manifests retain the complete A,A,B occurrence sequence independently of unique cached media.");
            Check(saved.CreateQueue().Select(song => song.Id).SequenceEqual(new[] { "one", "one", "two" }), "Offline playback preserves duplicate A,A,B occurrences and their original order.");
            Check(saved.CreateQueue().Select(song => song.Track).SequenceEqual(new[] { 1, 2, 3 }) &&
                saved.CreateQueue()[0].StreamUrl == saved.CreateQueue()[1].StreamUrl, "Repeated occurrences have distinct queue positions while sharing one validated local MP3 file.");
            Check(server.Requests.Count(request => request.Uri.AbsolutePath.Contains("stream")) == 2, "An A,A,B queue transfers exactly two unique media files.");
            Check(server.Requests.Where(request => request.Uri.AbsolutePath.Contains("stream")).All(request => request.Parameter("format") == "mp3"), "Every cache transfer explicitly requests a compatible MP3 stream.");
            Check(configuration.StreamingFormat == "flac" && configuration.MaximumBitRate == 128, "Offline requests do not change global streaming preferences.");
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 1, "Only completed manifest is published as an offline mix.");
            Check(await PhoneOfflineMixes.ValidateLocalQueueAsync(saved.CreateQueue()), "Completed local files validate without a route handshake.");
            var manifestPath = Path.Combine(output, PhoneOfflineMixes.RootName, saved.Folder.Replace('/', Path.DirectorySeparatorChar), "manifest.xml");
            var xml = File.ReadAllText(manifestPath);
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Single().CreateQueue().Select(song => song.Id).SequenceEqual(new[] { "one", "one", "two" }), "The production disk-read path restores A,A,B after manifest serialization.");
            var legacyXml = XDocument.Parse(xml);
            legacyXml.Root.Element("SchemaVersion").Value = "1"; legacyXml.Root.Element("QueueIds").Remove();
            File.WriteAllText(manifestPath, legacyXml.ToString());
            var legacy = (await PhoneOfflineMixes.GetSavedMixesAsync()).Single();
            Check(legacy.Manifest.SchemaVersion == 1 && legacy.CreateQueue().Select(song => song.Id).SequenceEqual(new[] { "one", "two" }), "A version 1 manifest without QueueIds remains readable in its original catalog order.");
            Check(await PhoneOfflineMixes.ValidateLocalQueueAsync(legacy.CreateQueue()), "Production path and file checks continue to validate an existing version 1 cache.");
            File.WriteAllText(manifestPath, xml);
            var brokenQueue = LocalXml.Deserialize<PhoneOfflineManifest>(xml); brokenQueue.QueueIds.Add("not-cached");
            var invalidQueue = false; try { brokenQueue.Validate(saved.AccountKey); } catch (InvalidDataException) { invalidQueue = true; }
            Check(invalidQueue, "Version 2 rejects occurrence IDs without a completed catalog file.");
            brokenQueue = LocalXml.Deserialize<PhoneOfflineManifest>(xml); brokenQueue.QueueIds = null;
            invalidQueue = false; try { brokenQueue.Validate(saved.AccountKey); } catch (InvalidDataException) { invalidQueue = true; }
            Check(invalidQueue, "Version 2 rejects a missing occurrence list rather than silently dropping duplicates.");
            brokenQueue = LocalXml.Deserialize<PhoneOfflineManifest>(xml); brokenQueue.QueueIds.Clear();
            File.WriteAllText(manifestPath, LocalXml.Serialize(brokenQueue));
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0, "The production read path excludes a version 2 cache with an empty occurrence list.");
            File.WriteAllText(manifestPath, xml);
            Check(!xml.Contains("private-fixture") && !xml.Contains("/rest/") && !xml.Contains("enc:") && !xml.Contains("token="), "Offline metadata excludes credentials and authenticated media URLs.");
            Check(!Directory.GetFiles(output, "*.partial", SearchOption.AllDirectories).Any(), "Completed transfers leave no partial files.");
            var requestsBeforeLocal = server.Requests.Length;
            await PhoneOfflineMixes.GetSavedMixesAsync(); await PhoneOfflineMixes.ValidateLocalQueueAsync(saved.CreateQueue());
            Check(server.Requests.Length == requestsBeforeLocal, "Reading and validating saved mixes sends no network request.");
            configuration.PrimaryUrl = "https://primary.invalid/"; configuration.SecondaryUrl = server.Url; configuration.BaseUrl = configuration.PrimaryUrl;
            var offlineConfiguration = await PhoneOfflineMixes.ResolveLocalQueueConfigurationAsync(saved.CreateQueue());
            Check(offlineConfiguration != null && AccountIdentity.For(offlineConfiguration) == saved.AccountKey &&
                offlineConfiguration.BaseUrl == server.Url, "A secondary-route cache resolves reporting to its manifest source server rather than the configured primary.");
            Check(offlineConfiguration.Username == configuration.Username && offlineConfiguration.Password == configuration.Password &&
                offlineConfiguration.AuthenticationMode == configuration.AuthenticationMode && configuration.BaseUrl == configuration.PrimaryUrl,
                "Source resolution clones current credentials and authentication without changing the active settings route.");
            Check(server.Requests.Length == requestsBeforeLocal, "Resolving secondary-route offline reporting performs no network handshake.");
            using (var alternate = new ProtocolServer(respond))
            {
                configuration.PrimaryUrl = alternate.Url; configuration.BaseUrl = alternate.Url;
                var alternateSaved = await PhoneOfflineMixes.SaveMixAsync(configuration, new PhoneMixDefinition
                { Id = "playlist:alternate", Name = "Alternate route", Songs = new List<Song> { new Song { Id = "one" } } }, null, CancellationToken.None);
                var mixedQueue = new[] { saved.CreateQueue()[0], alternateSaved.CreateQueue()[0] };
                Check(await PhoneOfflineMixes.ResolveLocalQueueConfigurationAsync(mixedQueue) == null &&
                    !await PhoneOfflineMixes.ValidateLocalQueueAsync(mixedQueue), "A queue that combines cached song IDs from different source routes is rejected.");
                await PhoneOfflineMixes.DeleteMixAsync(alternateSaved);
            }
            configuration.PrimaryUrl = server.Url; configuration.BaseUrl = server.Url; configuration.SecondaryUrl = null;
            var first = saved.CreateQueue()[0]; var localPath = Path.Combine(output, Uri.UnescapeDataString(new Uri(first.StreamUrl).AbsolutePath.Substring("/local/".Length)).Replace('/', Path.DirectorySeparatorChar));
            var bytes = File.ReadAllBytes(localPath); File.WriteAllBytes(localPath, bytes.Take(bytes.Length - 1).ToArray());
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0 && !await PhoneOfflineMixes.ValidateLocalQueueAsync(saved.CreateQueue()), "Changed file length makes the entire mix unavailable and rejects its queue.");
            File.WriteAllBytes(localPath, bytes);
            configuration.Username = "other";
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0 && !await PhoneOfflineMixes.ValidateLocalQueueAsync(saved.CreateQueue()), "Another account cannot list or play saved media.");
            configuration.Username = "fixture"; configuration.PrimaryUrl = "https://unrelated.invalid/"; configuration.BaseUrl = configuration.PrimaryUrl;
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0, "Removing a configured route isolates its former offline media.");
            configuration.PrimaryUrl = server.Url; configuration.BaseUrl = server.Url;
            var injected = LocalXml.Deserialize<PhoneOfflineManifest>(xml); injected.Catalog.Songs[0].PlaybackFile = "../escape.mp3";
            var rejected = false; try { injected.Validate(AccountIdentity.For(configuration)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Traversal cache filenames are rejected by the actual manifest validator.");
            injected = LocalXml.Deserialize<PhoneOfflineManifest>(xml); injected.Catalog.Songs[0].Song.CoverArt = "https://example.invalid/rest/getCoverArt?p=private";
            rejected = false; try { injected.Validate(AccountIdentity.For(configuration)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Credential-bearing artwork URLs cannot enter a valid offline manifest.");
            deny = true; rejected = false;
            try { await PhoneOfflineMixes.SaveMixAsync(configuration, definition, null, CancellationToken.None); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && (await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 1, "Missing download permission creates no new available revision."); deny = false;
            badMedia = true; rejected = false;
            try { await PhoneOfflineMixes.SaveMixAsync(configuration, definition, null, CancellationToken.None); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && Directory.GetFiles(output, "manifest.xml", SearchOption.AllDirectories).Length == 1, "A server document in place of MP3 does not publish a manifest."); badMedia = false;
            using (var cancellation = new CancellationTokenSource())
            {
                rejected = false;
                try { await PhoneOfflineMixes.SaveMixAsync(configuration, definition, new DirectProgress(value => { if (value.Completed == 1) cancellation.Cancel(); }), cancellation.Token); }
                catch (OperationCanceledException) { rejected = true; }
                Check(rejected && Directory.GetFiles(output, "manifest.xml", SearchOption.AllDirectories).Length == 1, "Cancelling after a real file transfer preserves only the earlier complete revision.");
                Check(!Directory.GetFiles(output, "*.partial", SearchOption.AllDirectories).Any(), "Cancellation cleans partial download files.");
            }
            rejected = false;
            try { await PhoneOfflineMixes.SaveMixAsync(configuration, definition, new DirectProgress(value => { if (value.Completed == 1) configuration.Username = "other"; }), CancellationToken.None); }
            catch (OperationCanceledException) { rejected = true; }
            Check(rejected && Directory.GetFiles(output, "manifest.xml", SearchOption.AllDirectories).Length == 1, "An account change during download prevents publication."); configuration.Username = "fixture";
            var latest = await PhoneOfflineMixes.SaveMixAsync(configuration, definition, null, CancellationToken.None);
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 1, "Downloading again exposes only the newest complete revision.");
            var lockedMedia = Path.Combine(output, Uri.UnescapeDataString(new Uri(latest.CreateQueue()[0].StreamUrl).AbsolutePath.Substring("/local/".Length)).Replace('/', Path.DirectorySeparatorChar));
            using (new FileStream(lockedMedia, FileMode.Open, FileAccess.Read, FileShare.Read))
                await PhoneOfflineMixes.DeleteMixAsync(latest);
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0 && !await PhoneOfflineMixes.ValidateLocalQueueAsync(latest.CreateQueue()) &&
                Directory.GetFiles(output, "manifest.xml", SearchOption.AllDirectories).Length == 0,
                "Removing an offline mix unpublishes its manifest immediately even when a cached media or artwork file is still held open.");
            latest = await PhoneOfflineMixes.SaveMixAsync(configuration, definition, null, CancellationToken.None);
            await PhoneOfflineMixes.DeleteMixAsync(latest);
            var accountDirectory = Path.GetDirectoryName(Path.GetDirectoryName(lockedMedia));
            Check((await PhoneOfflineMixes.GetSavedMixesAsync()).Count == 0 && Directory.GetFiles(output, "manifest.xml", SearchOption.AllDirectories).Length == 0 &&
                Directory.GetDirectories(accountDirectory).Length == 0, "Removing an offline mix deletes all revisions and sweeps released handles without orphan folders.");
            Check(!await PhoneOfflineMixes.ValidateLocalQueueAsync(new[] { new SongDisplayItem { Id = "one", StreamUrl = "ms-appdata:///local/unrelated/private.mp3" } }), "Local files outside the validated offline catalog cannot be played as saved mixes.");
        }
    }
    private static ProtocolResponse Xml(string body)
    { return new ProtocolResponse { Body = "<subsonic-response xmlns='http://subsonic.org/restapi' status='ok' version='1.16.1'>" + body + "</subsonic-response>" }; }
    private sealed class DirectProgress : IProgress<PhoneOfflineProgress>
    {
        private readonly Action<PhoneOfflineProgress> report;
        public DirectProgress(Action<PhoneOfflineProgress> report) { this.report = report; }
        public void Report(PhoneOfflineProgress value) { report(value); }
    }
}
