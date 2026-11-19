namespace Mixdrome.Phone.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Client.Common.Models.Subsonic;
    using Client.Common.Services.Navidrome;
    using Mixdrome.Phone.Views;
    using Windows.UI.Xaml;

    public sealed class PhoneCollectionHeading : INotifyPropertyChanged
    {
        private double size = 48;
        public string Name { get; set; }
        public double Size { get { return size; } set { size = value; var handler = PropertyChanged; if (handler != null) { handler(this, new PropertyChangedEventArgs("Size")); handler(this, new PropertyChangedEventArgs("Margin")); } } }
        public Thickness Margin { get { return new Thickness(21 * Size / 72, -8 * Size / 72, Size / 72, 22 * Size / 72); } }
        public event PropertyChangedEventHandler PropertyChanged;
        public override string ToString() { return Name; }
    }

    public sealed class PhoneCollectionItem : INotifyPropertyChanged
    {
        private double size;
        private string artwork;
        private double typographyScale = 1;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public string Route { get; set; }
        public ExpandedArtist Artist { get; set; }
        public SongDisplayItem Song { get; set; }
        public string Artwork { get { return artwork; } set { artwork = value; Changed("Artwork"); } }
        public double Size { get { return size; } set { size = value; Changed("Size"); Changed("LabelHeight"); Changed("LabelSize"); Changed("NameSize"); Changed("CardMargin"); Changed("RowMargin"); Changed("NameMargin"); Changed("LabelMargin"); Changed("SubtitleSize"); } }
        public double TypographyScale { get { return typographyScale; } set { typographyScale = value; Changed("LabelHeight"); Changed("LabelSize"); Changed("LabelMargin"); Changed("LabelLineHeight"); Changed("LabelMaxHeight"); } }
        public double LabelHeight { get { return TypographyScale * 60; } }
        public double LabelSize { get { return TypographyScale * 21; } }
        public double LabelLineHeight { get { return TypographyScale * 22; } }
        public double LabelMaxHeight { get { return TypographyScale * 44; } }
        private double RowScale { get { return Size / (Route == "artist" ? 90 : 120); } }
        public double NameSize { get { return RowScale * 32; } }
        public double SubtitleSize { get { return RowScale * 20; } }
        public Thickness CardMargin { get { return new Thickness(0, 0, Size * 18 / 207, Size * 18 / 207); } }
        public Thickness LabelMargin { get { return new Thickness(TypographyScale * 12, 0, TypographyScale * 12, 0); } }
        public Thickness RowMargin { get { return new Thickness(RowScale * 24, 0, RowScale * 24, RowScale * 12); } }
        public Thickness NameMargin { get { return new Thickness(RowScale * 12, 0, 0, RowScale * 5); } }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string property) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(property)); }
    }

    public sealed class PhoneArtistGroup : ObservableCollection<PhoneCollectionItem>
    {
        public PhoneArtistGroup(string key, IEnumerable<PhoneCollectionItem> items) : base(items) { Key = key; }
        public string Key { get; private set; }
        private double size = 45;
        public double Size { get { return size; } set { size = value; OnPropertyChanged(new PropertyChangedEventArgs("Size")); OnPropertyChanged(new PropertyChangedEventArgs("Margin")); OnPropertyChanged(new PropertyChangedEventArgs("FontSize")); } }
        public Thickness Margin { get { return new Thickness(Size * 24 / 68, Size * 14 / 68, 0, Size * 18 / 68); } }
        public double FontSize { get { return Size * 40 / 68; } }
    }

    public static class PhoneCollectionPresentation
    {
        public const string DefaultArtwork = "ms-appx:///Assets/MixRadio/DefaultArtwork300.png";
        public const string DefaultArtist = "ms-appx:///Assets/MixRadio/DefaultArtist300.png";

        public static string Artwork(NavidromeApi api, string id, bool artist = false)
        {
            if (string.IsNullOrWhiteSpace(id)) return artist ? DefaultArtist : DefaultArtwork;
            Uri uri;
            if (Uri.TryCreate(id, UriKind.Absolute, out uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "ms-appx" || uri.Scheme == "ms-appdata")) return id;
            return api.CoverArtUrl(id);
        }

        public static IList<PhoneArtistGroup> GroupArtists(IEnumerable<PhoneCollectionItem> artists)
        {
            return artists.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .GroupBy(item => Initial(item.Name))
                .Select(group => new PhoneArtistGroup(group.Key, group)).ToList();
        }

        private static string Initial(string name)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0 || !char.IsLetterOrDigit(value, 0)) return "#";
            return CultureInfo.CurrentCulture.TextInfo.ToLower(StringInfo.GetNextTextElement(value));
        }

        public static IList<Album> FavouriteUpdates(IEnumerable<Album> albums, IEnumerable<ExpandedArtist> artists)
        {
            var ids = new HashSet<string>(artists.Where(artist => !string.IsNullOrEmpty(artist.Id)).Select(artist => artist.Id), StringComparer.Ordinal);
            // Match stable server IDs, including the OpenSubsonic multiple-artist list.
            // Similar display names do not establish that an album belongs to an artist.
            return albums.Where(album => !string.IsNullOrEmpty(album.ArtistId) && ids.Contains(album.ArtistId) ||
                album.Artists != null && album.Artists.Any(artist => ids.Contains(artist.Id))).ToList();
        }

        public static SongDisplayItem Song(Song song, int track)
        {
            return new SongDisplayItem { Id = song.Id, Track = track, Title = song.Title ?? song.Name,
                Artist = song.Artist, Album = song.Album, Duration = song.Duration,
                DurationString = string.Format(CultureInfo.CurrentCulture, "{0}:{1:D2}", song.Duration / 60, song.Duration % 60), CoverArt = song.CoverArt };
        }

        public static async Task<IList<Song>> LoadSongsAsync(NavidromeApi source, PhoneCollectionItem item, Func<bool> current)
        {
            if (!current()) throw new OperationCanceledException();
            if (item.Route == "playlist") return (await source.GetPlaylist(item.Id).ReadAsync()).Entries.Cast<Song>().ToList();
            if (item.Route == "album") return (await source.GetAlbum(item.Id).ReadAsync()).Songs;
            var songs = new List<Song>();
            while (true)
            {
                if (!current()) throw new OperationCanceledException();
                var page = await source.GetGenreSongs(item.Name, null, songs.Count, 100).ReadAsync();
                if (!current()) throw new OperationCanceledException();
                songs.AddRange(page); if (page.Count < 100) return songs;
            }
        }
    }
}
