namespace Mixdrome.Phone.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Client.Common.Models.Navidrome;
    using Client.Common.Models.Subsonic;

    public sealed class PhoneMixDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IList<Song> Songs { get; set; }
        public bool IsPlayMe { get; set; }
    }

    public sealed class PhoneOfflineProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
        public long Received { get; set; }
        public long? BytesTotal { get; set; }
    }

    // The manifest contains metadata and relative cache filenames, never request URLs.
    public sealed class PhoneOfflineManifest
    {
        public PhoneOfflineManifest() { SchemaVersion = 2; QueueIds = new List<string>(); }
        public int SchemaVersion { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsPlayMe { get; set; }
        public DateTime CompletedUtc { get; set; }
        public OfflineCatalog Catalog { get; set; }
        public List<string> QueueIds { get; set; }

        public IList<string> GetQueueIds()
        {
            // Version 1 cached each ID once and had no occurrence list.
            return SchemaVersion == 1 ? Catalog.Songs.Select(record => record.Song.Id).ToList() : QueueIds;
        }

        public void Validate(string account)
        {
            if ((SchemaVersion != 1 && SchemaVersion != 2) || string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) ||
                Catalog == null || Catalog.Songs == null || Catalog.Songs.Count == 0 || CompletedUtc == default(DateTime))
                throw new InvalidDataException("Invalid offline mix manifest.");
            Catalog.Validate(account);
            if (Catalog.Songs.Any(record => record.PlaybackFile == null || record.PlaybackSize <= 0 ||
                !string.IsNullOrEmpty(record.Song.CoverArt) || !string.IsNullOrEmpty(record.Song.Path)))
                throw new InvalidDataException("Invalid offline mix media.");
            var queue = GetQueueIds();
            var cached = new HashSet<string>(Catalog.Songs.Select(record => record.Song.Id), StringComparer.Ordinal);
            if (queue == null || queue.Count == 0 || queue.Any(id => string.IsNullOrWhiteSpace(id) || !cached.Contains(id)) ||
                cached.Any(id => !queue.Contains(id, StringComparer.Ordinal)))
                throw new InvalidDataException("Invalid offline mix queue.");
        }
    }
}
