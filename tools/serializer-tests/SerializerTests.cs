using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;

// Types that are never XML-serialized by the app (in-memory only), so their
// XmlSerializer reflection failures are recorded but not counted as failures.
//  - PlaylistHistoryStack : ICollection without a default XmlSerializer accessor
//  - GenericMediaModel    : no parameterless constructor
public static class SerializerTests
{
    private static readonly HashSet<string> KnownNotSerialized = new HashSet<string>
    {
        "Client.Common.Models.PlaylistHistoryStack",
        "Client.Common.Models.Subsonic.GenericMediaModel",
    };

    private static int _failures;

    public static void Main()
    {
        var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var asm = Assembly.LoadFrom(Path.Combine(exeDir, "ClientCommonDesktop.dll"));
        Type[] allTypes;
        try { allTypes = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            allTypes = ex.Types.Where(t => t != null).ToArray();
            _failures += ex.LoaderExceptions.Length;
            Console.WriteLine("FAIL: " + ex.LoaderExceptions.Length + " production types could not load");
        }

        var modelTypes = allTypes
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Client.Common.Models"))
            .OrderBy(t => t.FullName)
            .ToList();

        if (modelTypes.Count == 0)
        {
            _failures++;
            Console.WriteLine("FAIL: no production model types were discovered");
        }

        foreach (var t in modelTypes)
        {
            if (KnownNotSerialized.Contains(t.FullName)) { Console.WriteLine("SKIP " + t.FullName); continue; }

            try
            {
                new XmlSerializer(t);
                Console.WriteLine("OK   " + t.FullName);
            }
            catch (Exception ex)
            {
                _failures++;
                var msg = new List<string> { ex.Message };
                var e = ex.InnerException;
                while (e != null) { msg.Add(e.Message); e = e.InnerException; }
                Console.WriteLine("FAIL " + t.FullName + " :: " + string.Join(" | ", msg));
            }
        }

        DeserializeGenre();
        DeserializeRadioStation();
        GenresHandleResponse();
        RadioStationsHandleResponse();
        IndexesHandleResponse();
        AlbumHandleResponse();
        MusicDirectoryHandleResponse();
        SongHandleResponse();
        DeserializeArtistImage();
        PlaylistXmlRoundTrip();

        Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED (" + modelTypes.Count + " model types)"
                                         : _failures + " FAILURES");
        Environment.Exit(_failures == 0 ? 0 : 1);
    }

    private static void DeserializeArtistImage()
    {
        var serializer = new XmlSerializer(typeof(Client.Common.Models.Subsonic.ExpandedArtist));
        const string image = "https://images.example.invalid/artist.jpg?size=600&token=opaque";
        var xml = "<artist xmlns='http://subsonic.org/restapi' id='artist&amp;音' name='艺人' albumCount='2' coverArt='ar-opaque:1' artistImageUrl='https://images.example.invalid/artist.jpg?size=600&amp;token=opaque'><album id='a' name='Album'/></artist>";
        using (var reader = new StringReader(xml))
        {
            var artist = (Client.Common.Models.Subsonic.ExpandedArtist)serializer.Deserialize(reader);
            if (artist.Id != "artist&音" || artist.ArtistImageUrl != image || artist.CoverArt != "ar-opaque:1" || artist.Albums.Count != 1)
            {
                _failures++;
                Console.WriteLine("FAIL ExpandedArtist artistImageUrl protocol metadata");
                return;
            }
        }
        Console.WriteLine("PASS ExpandedArtist artistImageUrl: real image URL, escaping, opaque cover ID and album metadata");
    }

    private static void PlaylistXmlRoundTrip()
    {
        try
        {
            var queue = new Client.Common.Models.PlaylistItemCollection
            {
                new Client.Common.Models.PlaylistItem
                {
                    SongId = "song&1", CoverArtId = "cover-1", AccountKey = "http://localhost/fixture\nreviewer",
                    Title = "R&D <Live>", Artist = "测试 artist", Duration = 219,
                    Uri = new Uri("http://localhost/fixture/stream?id=song%261"),
                    CoverArtUrl = "http://localhost/fixture/art?id=cover-1&size=200", IsStarred = true,
                    PlayingState = Client.Common.Models.PlaylistItemState.Playing
                },
                new Client.Common.Models.PlaylistItem
                {
                    SongId = "song-2", Title = "Second", Artist = "Artist B", Duration = 301,
                    Uri = new Uri("http://localhost/fixture/stream?id=song-2")
                }
            };
            var serializer = new XmlSerializer(typeof(Client.Common.Models.PlaylistItemCollection));
            string xml;
            using (var output = new StringWriter())
            {
                serializer.Serialize(output, queue);
                xml = output.ToString();
            }
            Client.Common.Models.PlaylistItemCollection restored;
            using (var input = new StringReader(xml))
                restored = (Client.Common.Models.PlaylistItemCollection)serializer.Deserialize(input);
            if (restored.Count != 2) throw new Exception("Queue order/count did not round-trip.");
            for (var index = 0; index < queue.Count; index++)
            {
                var before = queue[index];
                var after = restored[index];
                AssertSame(before.Title, after.Title, "Title");
                AssertSame(before.Artist, after.Artist, "Artist");
                AssertSame(before.Duration, after.Duration, "Duration");
                AssertSame(before.SongId, after.SongId, "SongId");
                AssertSame(before.CoverArtId, after.CoverArtId, "CoverArtId");
                AssertSame(before.AccountKey, after.AccountKey, "AccountKey");
                AssertSame(before.Uri, after.Uri, "Uri");
                AssertSame(before.CoverArtUrl, after.CoverArtUrl, "CoverArtUrl");
                AssertSame(before.IsStarred, after.IsStarred, "IsStarred");
                AssertSame(Client.Common.Models.PlaylistItemState.NotPlaying, after.PlayingState, "PlayingState");
            }
            // Older .spls files omit the new ID/account/favourite fields.
            var legacy = XDocument.Parse(xml);
            legacy.Descendants().Where(element => new[] { "SongId", "CoverArtId", "AccountKey", "IsStarred" }
                .Contains(element.Name.LocalName)).Remove();
            using (var input = new StringReader(legacy.ToString()))
            {
                var imported = (Client.Common.Models.PlaylistItemCollection)serializer.Deserialize(input);
                if (imported.Count != 2 || imported[0].Title != queue[0].Title || imported[0].Uri != queue[0].Uri ||
                    imported[0].SongId != null || imported[0].IsStarred)
                    throw new Exception("Legacy .spls compatibility was lost.");
            }
            var rejected = false;
            try { serializer.Deserialize(new StringReader("<ArrayOfPlaylistItem><PlaylistItem>")); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Malformed playlist XML must propagate an error.");
            Console.WriteLine("PASS Playlist .spls XML: metadata/order/Unicode round-trip, legacy fields and malformed input");
        }
        catch (Exception ex)
        {
            _failures++;
            Console.WriteLine("FAIL Playlist XML: " + ex.Message);
        }
    }

    private static void AssertSame(object expected, object actual, string field)
    {
        if (!Equals(expected, actual)) throw new Exception("Queue " + field + " did not round-trip.");
    }

    private static void GenresHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetGenresResult(config);
        var xml = XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><genres><genre songCount=\"1753\" albumCount=\"311\">Anime</genre><genre songCount=\"132\" albumCount=\"36\">Game</genre></genres></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null || result.Result.Count != 2 ||
            result.Result[0].Name != "Anime" || result.Result[0].SongCount != 1753 ||
            result.Result[1].Name != "Game")
        {
            _failures++;
            Console.WriteLine("FAIL GetGenresResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetGenresResult.HandleResponse: 2 genres (Anime 1753/311, Game 132/36)");
    }

    private static void RadioStationsHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetInternetRadioStationsResult(config);
        var xml = XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><internetRadioStations><internetRadioStation id=\"i1\" name=\"AsiaFM\" streamUrl=\"http://asiafm/hk\" /><internetRadioStation id=\"i2\" name=\"HITS FM\" streamUrl=\"https://sk.cri.cn/887.m3u8\" /></internetRadioStations></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null || result.Result.Count != 2 ||
            result.Result[0].Name != "AsiaFM" || result.Result[1].StreamUrl != "https://sk.cri.cn/887.m3u8")
        {
            _failures++;
            Console.WriteLine("FAIL GetInternetRadioStationsResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetInternetRadioStationsResult.HandleResponse: 2 stations");
    }

    private static void IndexesHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetIndexResult(config, "1");
        var xml = XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><indexes lastModified=\"1787910186000\" ignoredArticles=\"The El La\"><index name=\"#\"><artist id=\"12xz\" name=\"(测试艺人\" coverArt=\"ar-12xz_0\" /></index><index name=\"A\"><artist id=\"aq1\" name=\"Aqours\" coverArt=\"ar-aq1_0\" /></index></indexes></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null || result.Result.Artists == null || result.Result.Artists.Count != 2)
        {
            _failures++;
            Console.WriteLine("FAIL GetIndexResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetIndexResult.HandleResponse: " + result.Result.Artists.Count + " artists across index groups");
    }

    private static void AlbumHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetAlbumResult(config, "al-1");
        var xml = XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><album id=\"al-1\" name=\"アイコトバ!\" artist=\"Various Artists\" artistId=\"63sq\" songCount=\"11\" duration=\"2506\"><song id=\"s1\" parent=\"al-1\" title=\"アイコトバ!\" artist=\"Various Artists\" duration=\"231\" track=\"1\" /><song id=\"s2\" parent=\"al-1\" title=\"Second Song\" artist=\"Various Artists\" duration=\"200\" track=\"2\" /></album></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null)
        {
            _failures++;
            Console.WriteLine("FAIL GetAlbumResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetAlbumResult.HandleResponse: title=" + result.Result.Name);
    }

    private static void MusicDirectoryHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetMusicDirectoryResult(config, "dir-1");
        var xml = System.Xml.Linq.XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><directory id=\"dir-1\" name=\"Test Folder\"><child id=\"ch-1\" title=\"Album One\" artist=\"Artist A\" isDir=\"true\" /><child id=\"ch-2\" title=\"Song X\" artist=\"Artist A\" duration=\"200\" isDir=\"false\" /></directory></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null)
        {
            _failures++;
            Console.WriteLine("FAIL GetMusicDirectoryResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetMusicDirectoryResult.HandleResponse: " + result.Result.Id);
    }

    private static void SongHandleResponse()
    {
        var config = new Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration
        {
            BaseUrl = "http://127.0.0.1:10011", Username = "u", Password = "p"
        };
        var result = new Client.Common.Results.GetSongResult(config, "song-1");
        var xml = System.Xml.Linq.XDocument.Parse("<subsonic-response xmlns=\"http://subsonic.org/restapi\" status=\"ok\"><song id=\"song-1\" title=\"Test Song\" artist=\"Test Artist\" duration=\"200\" track=\"1\" /></subsonic-response>");
        result.HandleResponse(xml);
        if (result.Result == null)
        {
            _failures++;
            Console.WriteLine("FAIL GetSongResult.HandleResponse");
            return;
        }
        Console.WriteLine("PASS GetSongResult.HandleResponse: " + result.Result.Name);
    }

    private static void DeserializeGenre()
    {
        var xml = "<genre xmlns=\"http://subsonic.org/restapi\" songCount=\"1753\" albumCount=\"311\">Anime</genre>";
        var serializer = new XmlSerializer(typeof(Client.Common.Models.Subsonic.Genre));
        var g = (Client.Common.Models.Subsonic.Genre)serializer.Deserialize(new StringReader(xml));
        if (g.Name != "Anime" || g.SongCount != 1753 || g.AlbumCount != 311)
        {
            _failures++;
            Console.WriteLine("FAIL Genre round-trip");
            return;
        }
        Console.WriteLine("PASS Genre deserialize: " + g.Name + " / " + g.SongCount + " songs / " + g.AlbumCount + " albums");
    }

    private static void DeserializeRadioStation()
    {
        var xml = "<internetRadioStation xmlns=\"http://subsonic.org/restapi\" id=\"abc\" name=\"HITS FM\" streamUrl=\"http://x/y.m3u8\" />";
        var serializer = new XmlSerializer(typeof(Client.Common.Models.Subsonic.InternetRadioStation));
        var s = (Client.Common.Models.Subsonic.InternetRadioStation)serializer.Deserialize(new StringReader(xml));
        if (s.Name != "HITS FM" || s.StreamUrl != "http://x/y.m3u8")
        {
            _failures++;
            Console.WriteLine("FAIL InternetRadioStation round-trip");
            return;
        }
        Console.WriteLine("PASS InternetRadioStation deserialize: " + s.Name + " -> " + s.StreamUrl);
    }
}
