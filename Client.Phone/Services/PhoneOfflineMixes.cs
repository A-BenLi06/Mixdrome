namespace Mixdrome.Phone.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Client.Common.Models.Navidrome;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;
    using Mixdrome.Phone.Views;
    using Windows.Security.Cryptography;
    using Windows.Security.Cryptography.Core;
    using Windows.Storage;

    public sealed class PhoneSavedMix
    {
        public string Folder { get; internal set; }
        public string AccountKey { get; internal set; }
        public PhoneOfflineManifest Manifest { get; internal set; }
        public string Id { get { return Manifest.Id; } }
        public string Name { get { return Manifest.Name; } }
        public bool IsPlayMe { get { return Manifest.IsPlayMe; } }
        public string Artwork
        {
            get
            {
                var record = Manifest.Catalog.Songs.FirstOrDefault(song => song.ArtworkFile != null);
                return record == null ? PhoneCollectionPresentation.DefaultArtwork : LocalUri(record.ArtworkFile);
            }
        }
        internal string LocalUri(string name)
        {
            if (!OfflineCatalog.IsFileName(name)) throw new InvalidDataException(PhoneText.Get("OfflineDataInvalid"));
            return PhoneOfflineMixes.LocalPrefix + Folder + "/" + name;
        }
        public List<SongDisplayItem> CreateQueue()
        {
            Manifest.Validate(AccountKey);
            var index = 0;
            var records = Manifest.Catalog.Songs.ToDictionary(record => record.Song.Id, StringComparer.Ordinal);
            return Manifest.GetQueueIds().Select(id => records[id]).Select(record => new SongDisplayItem
            {
                Id = record.Song.Id, Track = ++index, Title = record.Song.Title, Artist = record.Song.Artist,
                Album = record.Song.Album, Duration = record.Song.Duration, StreamUrl = LocalUri(record.PlaybackFile),
                CoverArt = record.ArtworkFile == null ? PhoneCollectionPresentation.DefaultArtwork : LocalUri(record.ArtworkFile)
            }).ToList();
        }
    }

    public static class PhoneOfflineMixes
    {
        public const string RootName = "phone-offline-mixes";
        public const string LocalPrefix = "ms-appdata:///local/" + RootName + "/";
        private const string ManifestName = "manifest.xml";
        private static readonly SemaphoreSlim DownloadGate = new SemaphoreSlim(1, 1);
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        public static async Task<PhoneSavedMix> SaveMixAsync(SubsonicServiceConfiguration configuration,
            PhoneMixDefinition mix, IProgress<PhoneOfflineProgress> progress, CancellationToken cancellation)
        {
            if (mix == null || string.IsNullOrWhiteSpace(mix.Id) || string.IsNullOrWhiteSpace(mix.Name) ||
                mix.Songs == null || mix.Songs.Count == 0 || mix.Songs.Any(song => song == null || string.IsNullOrWhiteSpace(song.Id)))
                throw new InvalidOperationException(PhoneText.Get("PlaylistEmpty"));
            // Freeze occurrences before any asynchronous work. Files are cached
            // once per ID, while this list retains every playlist occurrence.
            var queueIds = mix.Songs.Select(song => song.Id).ToList();
            var account = AccountIdentity.For(configuration);
            RequireAccount(account); cancellation.ThrowIfCancellationRequested();
            await DownloadGate.WaitAsync(cancellation);
            StorageFolder target = null;
            var committed = false;
            try
            {
                RequireAccount(account);
                var api = new NavidromeApi(configuration);
                var user = await api.GetUser().ReadAsync();
                RequireAccount(account); cancellation.ThrowIfCancellationRequested();
                if (!user.DownloadRole || !user.StreamRole) throw new InvalidOperationException(PhoneText.Get("DownloadingUnavailable"));
                var root = await AccountFolder(account);
                await SweepUnpublishedFoldersAsync(root);
                target = await root.CreateFolderAsync(Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
                var records = new List<DownloadedSong>();
                var selected = queueIds.Distinct(StringComparer.Ordinal).ToList();
                var covers = new Dictionary<string, string>(StringComparer.Ordinal);
                Report(progress, 0, selected.Count, null);
                foreach (var requested in selected)
                {
                    cancellation.ThrowIfCancellationRequested(); RequireAccount(account);
                    var song = await api.GetSong(requested).ReadAsync();
                    cancellation.ThrowIfCancellationRequested(); RequireAccount(account);
                    if (song.IsVideo) throw new InvalidOperationException(PhoneText.Get("OfflineAudioOnly"));
                    var name = Guid.NewGuid().ToString("N") + "-original.mp3";
                    var completed = records.Count;
                    var copied = await Transfer(target, name, api.StreamUri(song.Id, "mp3"), cancellation,
                        new Progress<DownloadProgress>(value => Report(progress, completed, selected.Count, value)), true);
                    cancellation.ThrowIfCancellationRequested(); RequireAccount(account);
                    var record = new DownloadedSong
                    {
                        Song = Metadata(song), OriginalFile = name, OriginalSize = copied.Length,
                        PlaybackFile = name, PlaybackSize = copied.Length, CompletedUtc = DateTime.UtcNow
                    };
                    if (!string.IsNullOrEmpty(song.CoverArt))
                    {
                        string cover;
                        if (!covers.TryGetValue(song.CoverArt, out cover))
                        {
                            cover = Guid.NewGuid().ToString("N") + "-cover.jpg";
                            try
                            {
                                await Transfer(target, cover, new Uri(api.CoverArtUrl(song.CoverArt)), cancellation, null, false);
                                covers[song.CoverArt] = cover;
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception) { cover = null; }
                        }
                        record.ArtworkFile = cover;
                    }
                    records.Add(record); Report(progress, records.Count, selected.Count, null);
                }
                cancellation.ThrowIfCancellationRequested(); RequireAccount(account);
                var manifest = new PhoneOfflineManifest
                {
                    Id = mix.Id, Name = mix.Name, IsPlayMe = mix.IsPlayMe, CompletedUtc = DateTime.UtcNow,
                    Catalog = new OfflineCatalog { AccountKey = account, Songs = records }, QueueIds = queueIds
                };
                manifest.Validate(account);
                var pending = await target.CreateFileAsync(ManifestName + ".pending", CreationCollisionOption.FailIfExists);
                await FileIO.WriteTextAsync(pending, LocalXml.Serialize(manifest));
                cancellation.ThrowIfCancellationRequested(); RequireAccount(account);
                // This rename is the only publication point. Incomplete folders are never listed.
                await pending.RenameAsync(ManifestName, NameCollisionOption.FailIfExists);
                committed = true;
                return new PhoneSavedMix { AccountKey = account, Folder = Hash(account) + "/" + target.Name, Manifest = manifest };
            }
            finally
            {
                try { if (!committed && target != null) await target.DeleteAsync(StorageDeleteOption.PermanentDelete); }
                finally { DownloadGate.Release(); }
            }
        }

        public static async Task<IList<PhoneSavedMix>> GetSavedMixesAsync()
        {
            var accounts = Accounts();
            var result = new List<PhoneSavedMix>();
            var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(RootName, CreationCollisionOption.OpenIfExists);
            foreach (var account in accounts)
            {
                StorageFolder accountFolder;
                try { accountFolder = await root.GetFolderAsync(Hash(account)); }
                catch (FileNotFoundException) { continue; }
                foreach (var folder in await accountFolder.GetFoldersAsync())
                {
                    Guid id;
                    if (!Guid.TryParseExact(folder.Name, "N", out id)) continue;
                    try
                    {
                        var manifest = LocalXml.Deserialize<PhoneOfflineManifest>(await FileIO.ReadTextAsync(await folder.GetFileAsync(ManifestName)));
                        manifest.Validate(account);
                        var available = true;
                        foreach (var record in manifest.Catalog.Songs)
                            if (!await HasFile(folder, record.PlaybackFile, record.PlaybackSize)) { available = false; break; }
                        if (!available) continue;
                        // Artwork is optional, and a removed image must fall back locally.
                        foreach (var record in manifest.Catalog.Songs)
                            if (record.ArtworkFile != null && !await HasFile(folder, record.ArtworkFile, 0)) record.ArtworkFile = null;
                        result.Add(new PhoneSavedMix { AccountKey = account, Folder = Hash(account) + "/" + folder.Name, Manifest = manifest });
                    }
                    catch (Exception) { /* Missing, partial, or invalid caches are not available mixes. */ }
                }
            }
            if (!accounts.SequenceEqual(Accounts(), StringComparer.Ordinal)) throw new OperationCanceledException();
            return result.GroupBy(mix => mix.AccountKey + "\n" + mix.Id, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(mix => mix.Manifest.CompletedUtc).First())
                .OrderByDescending(mix => mix.Manifest.CompletedUtc).ToList();
        }

        public static async Task<bool> ValidateLocalQueueAsync(IEnumerable<SongDisplayItem> songs)
        { return await ResolveLocalQueueConfigurationAsync(songs) != null; }

        public static async Task<SubsonicServiceConfiguration> ResolveLocalQueueConfigurationAsync(IEnumerable<SongDisplayItem> songs)
        {
            var selection = songs == null ? new List<SongDisplayItem>() : songs.ToList();
            if (selection.Count == 0 || selection.Any(song => song == null || string.IsNullOrEmpty(song.StreamUrl) ||
                !song.StreamUrl.StartsWith(LocalPrefix, StringComparison.Ordinal))) return null;
            var available = (await GetSavedMixesAsync()).SelectMany(mix => mix.CreateQueue()
                .Select(song => new { Song = song, mix.AccountKey })).ToList();
            string account = null;
            foreach (var song in selection)
            {
                var candidate = available.FirstOrDefault(value => value.Song.Id == song.Id &&
                    string.Equals(value.Song.StreamUrl, song.StreamUrl, StringComparison.Ordinal));
                if (candidate == null || (account != null && account != candidate.AccountKey)) return null;
                account = candidate.AccountKey;
            }
            if (!IsCurrentAccount(account)) return null;
            var current = PhoneSettings.Load();
            var separator = account.IndexOf('\n');
            if (separator <= 0) return null;
            var configuration = new SubsonicServiceConfiguration
            {
                BaseUrl = account.Substring(0, separator), Username = current.Username, Password = current.Password,
                PrimaryUrl = current.PrimaryUrl, SecondaryUrl = current.SecondaryUrl, AuthenticationMode = current.AuthenticationMode,
                CompatibleMode = current.CompatibleMode, StreamingFormat = current.StreamingFormat, MaximumBitRate = current.MaximumBitRate
            };
            // Reporting must use the route that supplied these IDs, including
            // when the saved mix belongs to the secondary configured server.
            return AccountIdentity.For(configuration) == account ? configuration : null;
        }

        public static async Task DeleteMixAsync(PhoneSavedMix mix)
        {
            if (mix == null) return;
            RequireAccount(mix.AccountKey);
            var parts = mix.Folder.Split('/');
            Guid folderId;
            if (parts.Length != 2 || parts[0] != Hash(mix.AccountKey) || !Guid.TryParseExact(parts[1], "N", out folderId))
                throw new InvalidDataException(PhoneText.Get("OfflineDataInvalid"));
            await DownloadGate.WaitAsync();
            try
            {
                RequireAccount(mix.AccountKey);
                var root = await AccountFolder(mix.AccountKey);
                // Remove every completed revision of this mix, so an older copy cannot
                // reappear after deleting the latest download. Unpublish manifest.xml
                // first so active XAML image or audio handles never leave the mix listed.
                foreach (var folder in await root.GetFoldersAsync())
                {
                    if (!Guid.TryParseExact(folder.Name, "N", out folderId)) continue;
                    StorageFile manifestFile;
                    PhoneOfflineManifest manifest;
                    try
                    {
                        manifestFile = await folder.GetFileAsync(ManifestName);
                        manifest = LocalXml.Deserialize<PhoneOfflineManifest>(await FileIO.ReadTextAsync(manifestFile));
                        manifest.Validate(mix.AccountKey);
                    }
                    catch (Exception) { continue; }
                    if (manifest.Id != mix.Id) continue;
                    RequireAccount(mix.AccountKey);
                    await manifestFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    await TryDeleteFolderContentsAsync(folder);
                }
                await SweepUnpublishedFoldersAsync(root);
            }
            finally { DownloadGate.Release(); }
        }

        private static async Task SweepUnpublishedFoldersAsync(StorageFolder accountFolder)
        {
            foreach (var folder in await accountFolder.GetFoldersAsync())
            {
                Guid id;
                if (!Guid.TryParseExact(folder.Name, "N", out id)) continue;
                var files = await folder.GetFilesAsync();
                if (files.Any(file => string.Equals(file.Name, ManifestName, StringComparison.Ordinal) ||
                    string.Equals(file.Name, ManifestName + ".pending", StringComparison.Ordinal))) continue;
                await TryDeleteFolderContentsAsync(folder);
            }
        }

        private static async Task TryDeleteFolderContentsAsync(StorageFolder folder)
        {
            foreach (var file in await folder.GetFilesAsync())
            {
                try { await file.DeleteAsync(StorageDeleteOption.PermanentDelete); }
                catch (Exception error) { if (!IsLockException(error)) throw; }
            }
            try { await folder.DeleteAsync(StorageDeleteOption.PermanentDelete); }
            catch (Exception error) { if (!IsLockException(error)) throw; }
        }

        private static bool IsLockException(Exception error)
        {
            var code = unchecked((uint)error.HResult);
            return error is UnauthorizedAccessException || error is IOException ||
                code == 0x80070005u || code == 0x80070020u || code == 0x80070091u;
        }

        private static async Task<DownloadPayload> Transfer(StorageFolder folder, string name, Uri uri,
            CancellationToken cancellation, IProgress<DownloadProgress> progress, bool requireMp3)
        {
            if (!OfflineCatalog.IsFileName(name)) throw new InvalidDataException(PhoneText.Get("OfflineDataInvalid"));
            var file = await folder.CreateFileAsync(name + ".partial", CreationCollisionOption.FailIfExists);
            var complete = false;
            try
            {
                DownloadPayload payload;
                using (var stream = await file.OpenStreamForWriteAsync())
                    payload = await MediaDownload.CopyAsync(Http, uri, stream, cancellation, progress, 0, requireMp3);
                cancellation.ThrowIfCancellationRequested();
                await file.RenameAsync(name, NameCollisionOption.FailIfExists); complete = true;
                return payload;
            }
            finally { if (!complete) await file.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }
        private static Song Metadata(Song song)
        {
            return new Song { Id = song.Id, Title = song.Title, Artist = song.Artist, Album = song.Album,
                ArtistId = song.ArtistId, AlbumId = song.AlbumId, Duration = song.Duration, Track = song.Track,
                Suffix = "mp3", ContentType = "audio/mpeg" };
        }
        private static void Report(IProgress<PhoneOfflineProgress> progress, int completed, int total, DownloadProgress transfer)
        {
            if (progress != null) progress.Report(new PhoneOfflineProgress { Completed = completed, Total = total,
                Received = transfer == null ? 0 : transfer.Received, BytesTotal = transfer == null ? null : transfer.Total });
        }
        private static IList<string> Accounts()
        {
            var configuration = PhoneSettings.Load();
            if (string.IsNullOrWhiteSpace(configuration.Username)) return new List<string>();
            return new[] { configuration.PrimaryUrl, configuration.SecondaryUrl, configuration.BaseUrl }
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => AccountIdentity.For(
                    new SubsonicServiceConfiguration { BaseUrl = value, Username = configuration.Username }))
                .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
        }
        private static void RequireAccount(string account)
        { if (!IsCurrentAccount(account)) throw new OperationCanceledException(); }
        public static bool IsCurrentAccount(string account)
        { return !string.IsNullOrEmpty(account) && Accounts().Contains(account, StringComparer.Ordinal); }
        private static string Hash(string account)
        {
            return CryptographicBuffer.EncodeToHexString(HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256)
                .HashData(CryptographicBuffer.ConvertStringToBinary(account, BinaryStringEncoding.Utf8))).Substring(0, 32).ToLowerInvariant();
        }
        private static async Task<StorageFolder> AccountFolder(string account)
        {
            var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(RootName, CreationCollisionOption.OpenIfExists);
            return await root.CreateFolderAsync(Hash(account), CreationCollisionOption.OpenIfExists);
        }
        private static async Task<bool> HasFile(StorageFolder folder, string name, long size)
        {
            if (!OfflineCatalog.IsFileName(name)) return false;
            try
            {
                var actual = (await (await folder.GetFileAsync(name)).GetBasicPropertiesAsync()).Size;
                return size == 0 ? actual > 0 : actual == (ulong)size;
            }
            catch (FileNotFoundException) { return false; }
        }
    }
}
