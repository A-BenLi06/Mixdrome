namespace Mixdrome.Phone.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Models.Navidrome;
    using Client.Common.Services;
    using Client.Common.Services.DataStructures.SubsonicService;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Common;

    // Phone pages keep their existing adapter API while using the desktop protocol/models.
    public class WP8SubsonicService : IDisposable
    {
        private SubsonicServiceConfiguration _configuration = new SubsonicServiceConfiguration();
        private NavidromeApi Api { get { RequireServer(); return new NavidromeApi(_configuration); } }

        private void RequireServer()
        {
            if (string.IsNullOrWhiteSpace(_configuration.BaseUrl))
                throw new InvalidOperationException(PhoneText.Get("ConfigureServer"));
        }

        public void Configure(string baseUrl, string username, string password)
        {
            Configure(new SubsonicServiceConfiguration { BaseUrl = baseUrl, Username = username, Password = password });
        }

        public void Configure(SubsonicServiceConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _configuration = configuration;
        }

        public void SetCompatibleMode(bool enabled) { CompatibleMode = enabled; }
        public PhoneReportingProfile CreateReportingProfile()
        {
            RequireServer();
            return new PhoneReportingProfile
            {
                BaseUrl = _configuration.BaseUrl, Username = _configuration.Username,
                AuthenticationMode = _configuration.AuthenticationMode.ToString(),
                CredentialResource = PhoneSettings.CredentialResource
            };
        }
        public bool CompatibleMode
        {
            get { return _configuration.CompatibleMode; }
            set { _configuration.CompatibleMode = value; }
        }

        public async Task<bool> PingAsync() { return await Api.Ping().ReadAsync(); }
        public Task<Song> GetSongAsync(string id) { return Api.GetSong(id).ReadAsync(); }
        public async Task<ArtistInformation> GetArtistInfoAsync(string id)
        {
            var api = Api;
            var information = await api.GetArtistInfo(id).ReadAsync();
            if (string.IsNullOrWhiteSpace(information.ImageUrl))
            {
                var artist = await api.GetArtist(id).ReadAsync();
                if (artist != null) information.ImageUrl = artist.ArtistImageUrl;
            }
            return information;
        }
        public Task<bool> SetSongStarredAsync(string id, bool value) { return Api.SetStarred(new Song { Id = id }, value).ReadAsync(); }
        public Task<bool> SetSongRatingAsync(string id, int value) { return Api.SetRating(id, value).ReadAsync(); }
        public async Task<IList<LyricVariant>> GetSongLyricsAsync(string id, string artist, string title)
        {
            var api = Api;
            var extensions = api.GetExtensions();
            await extensions.Execute();
            IList<int> versions;
            if (extensions.Error == null && extensions.Result.TryGetValue("songLyrics", out versions) && versions.Contains(1))
                return await api.GetLyrics(id, true).ReadAsync();
            return await api.GetLegacyLyrics(artist, title).ReadAsync();
        }
        public Uri GetStreamUri(string songId)
        { RequireServer(); return new SubsonicService { Configuration = _configuration }.GetUriForFileWithId(songId); }

        public string GetCoverArtUrl(string coverArtId, int size = 300)
        {
            if (string.IsNullOrEmpty(coverArtId)) return SubsonicService.CoverArtPlaceholder;
            RequireServer();
            return new SubsonicRequest(_configuration, "getCoverArt").Add("id", coverArtId).Add("size", size).Url;
        }

        public async Task<List<InternetRadioStation>> GetInternetRadioStationsAsync()
        {
            return (await Api.GetRadio().ReadAsync()).Select(station => new InternetRadioStation
            {
                Id = station.Id, Name = station.Name, StreamUrl = station.StreamUrl, HomePageUrl = station.HomePageUrl
            }).ToList();
        }

        public async Task<List<Song>> GetStarredSongsAsync()
        { return (await Api.GetStarred(null).ReadAsync()).Songs.ToList(); }

        public async Task<List<NowPlayingEntry>> GetNowPlayingAsync()
        {
            return (await Api.GetNowPlaying().ReadAsync()).Select(entry => new NowPlayingEntry
            {
                Song = entry, Username = entry.Username, PlayerId = entry.PlayerId,
                PlayerName = entry.PlayerName, MinutesAgo = entry.MinutesAgo
            }).ToList();
        }

        public async Task<List<Album>> GetAlbumsAsync(string artistId)
        { return (await Api.GetArtist(artistId).ReadAsync()).Albums.ToList(); }
        public async Task<List<Song>> GetAlbumSongsAsync(string albumId)
        { return (await Api.GetAlbum(albumId).ReadAsync()).Songs.ToList(); }

        public void Dispose() { }
    }

    public class InternetRadioStation
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StreamUrl { get; set; }
        public string HomePageUrl { get; set; }
    }

    public class NowPlayingEntry
    {
        public Song Song { get; set; }
        public string Username { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public int MinutesAgo { get; set; }
    }
}
