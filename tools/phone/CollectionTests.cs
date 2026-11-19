using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Client.Common.Models.Subsonic;
using Client.Common.Services.DataStructures.SubsonicService;
using Client.Common.Services.Navidrome;
using Mixdrome.Phone.Services;

// Production collection selection and protocol projection. These checks cannot
// certify the native Pivot, grouped ListView, touch input or image decoder.
public static class PhoneCollectionTests
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static string Response(string body, bool success = true) { return "<subsonic-response xmlns='http://subsonic.org/restapi' status='" + (success ? "ok" : "failed") + "' version='1.16.1'>" + body + "</subsonic-response>"; }
    public static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " production collection grouping, artist identity, artwork, complete mix queues, cancellation and protocol-failure assertions. Native rendering remains unverified."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task Run()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var groups = PhoneCollectionPresentation.GroupArtists(new[] {
                new PhoneCollectionItem { Id = "z", Name = "Zulu" }, new PhoneCollectionItem { Id = "a2", Name = "Alpha" },
                new PhoneCollectionItem { Id = "a1", Name = "Alpha" }, new PhoneCollectionItem { Id = "a3", Name = "apple" },
                new PhoneCollectionItem { Id = "han", Name = "音乐人" }, new PhoneCollectionItem { Id = "accent", Name = "Élan" },
                new PhoneCollectionItem { Id = "unknown", Name = null }, new PhoneCollectionItem { Id = "symbol", Name = "♪ Song" } });
            Check(groups.Single(group => group.Key == "a").Select(item => item.Id).SequenceEqual(new[] { "a1", "a2", "a3" }), "Artists use culture ordering and stable ID ties, and case variants share a group.");
            Check(groups.Any(group => group.Key == "音"), "Chinese artist names retain a meaningful initial without a Latin alphabet table.");
            Check(groups.Any(group => group.Key == "é"), "Accented initials retain their Unicode text element.");
            Check(groups.Single(group => group.Key == "#").Count == 2, "Blank and symbol-led names remain visible.");
            Check(groups.Sum(group => group.Count) == 8, "Grouping retains every artist, including same-name distinct identities.");
        }
        finally { CultureInfo.CurrentCulture = culture; }

        var failedOffset = -1;
        var offsets = new List<int>();
        using (var server = new ProtocolServer(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("getSongsByGenre.view"))
            {
                var offset = int.Parse(request.Parameter("offset")); offsets.Add(offset);
                if (offset == failedOffset) return new ProtocolResponse { Body = Response("<error code='40' message='Denied'/>", false) };
                var count = Math.Min(100, Math.Max(0, 125 - offset));
                return new ProtocolResponse { Body = Response("<songsByGenre>" + string.Concat(Enumerable.Range(offset, count).Select(index => "<song id='genre-" + index + "' title='Song' artist='Artist' duration='90' />")) + "</songsByGenre>") };
            }
            if (request.Uri.AbsolutePath.EndsWith("getPlaylist.view")) return new ProtocolResponse { Body = Response("<playlist id='list' name='Mix'><entry id='same' title='First' duration='125'/><entry id='same' title='Second' duration='126'/></playlist>") };
            if (request.Uri.AbsolutePath.EndsWith("getAlbum.view")) return new ProtocolResponse { Body = Response("<album id='album' name='Album'><song id='album-song' title='Album song'/></album>") };
            return new ProtocolResponse { Body = Response("") };
        }))
        {
            var api = new NavidromeApi(new SubsonicServiceConfiguration { BaseUrl = server.Url, Username = "fixture", Password = "fixture" });
            var genre = await PhoneCollectionPresentation.LoadSongsAsync(api, new PhoneCollectionItem { Route = "genre", Name = "Genre & 音" }, () => true);
            Check(genre.Count == 125 && offsets.SequenceEqual(new[] { 0, 100 }), "A genre mix loads all pages with actual offsets.");
            Check(genre.Select(song => song.Id).Distinct().Count() == 125 && genre.Last().Id == "genre-124", "The full genre survives projection in server order.");
            var playlist = await PhoneCollectionPresentation.LoadSongsAsync(api, new PhoneCollectionItem { Route = "playlist", Id = "list" }, () => true);
            Check(playlist.Count == 2 && playlist[0].Id == playlist[1].Id && playlist[0].Title != playlist[1].Title, "Playlist occurrences preserve legitimate duplicate IDs.");
            var projected = playlist.Select((song, index) => PhoneCollectionPresentation.Song(song, index + 1)).ToList();
            Check(projected[1].Track == 2 && projected[1].DurationString == "2:06", "Projection retains occurrence position and duration metadata.");
            Check((await PhoneCollectionPresentation.LoadSongsAsync(api, new PhoneCollectionItem { Route = "album", Id = "album" }, () => true)).Single().Id == "album-song", "Album offline requests use actual album entries.");
            offsets.Clear(); var checks = 0; var cancelled = false;
            try { await PhoneCollectionPresentation.LoadSongsAsync(api, new PhoneCollectionItem { Route = "genre", Name = "Genre" }, () => ++checks < 4); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && offsets.SequenceEqual(new[] { 0 }), "A stale selection stops before requesting its next genre page.");
            failedOffset = 100; var failed = false;
            try { await PhoneCollectionPresentation.LoadSongsAsync(api, new PhoneCollectionItem { Route = "genre", Name = "Genre" }, () => true); } catch (Exception) { failed = true; }
            Check(failed, "A later page error fails the complete mix instead of playing a partial queue.");
            foreach (var id in new[] { "al-opaque:1", "dc-opaque:1", "art&音" })
            {
                var uri = new Uri(PhoneCollectionPresentation.Artwork(api, id));
                var request = new ProtocolRequest { Target = uri.PathAndQuery, Headers = new Dictionary<string, string>() };
                Check(request.Parameter("id") == id, "Artwork identifiers survive URI classification and protocol escaping.");
            }
            Check(PhoneCollectionPresentation.Artwork(api, null, true) == PhoneCollectionPresentation.DefaultArtist, "Missing artist artwork uses the packaged artist image.");
            Check(PhoneCollectionPresentation.Artwork(api, "https://example.invalid/artist.png") == "https://example.invalid/artist.png", "A real direct artist image remains a direct image URI.");
        }
        var updates = PhoneCollectionPresentation.FavouriteUpdates(new[] {
            new Album { Id = "first", ArtistId = "artist" }, new Album { Id = "collaboration", Artists = new List<MediaArtist> { new MediaArtist { Id = "artist", Name = "Other display name" } } },
            new Album { Id = "wrong", ArtistId = "another", Artist = "Same Name" }, new Album { Id = "unknown", Artist = "Same Name" } },
            new[] { new ExpandedArtist { Id = "artist", Name = "Same Name" } });
        Check(updates.Select(album => album.Id).SequenceEqual(new[] { "first", "collaboration" }), "Updates preserve newest server order and match stable IDs, including collaborations, without guessing by names.");
        Check(PhoneCollectionPresentation.FavouriteUpdates(new[] { new Album { ArtistId = "artist" } }, new ExpandedArtist[0]).Count == 0, "No favourite artists produces no invented updates.");
    }
}

namespace Windows.UI.Xaml
{
    public struct Thickness
    {
        public Thickness(double left, double top, double right, double bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public double Left, Top, Right, Bottom;
    }
}
