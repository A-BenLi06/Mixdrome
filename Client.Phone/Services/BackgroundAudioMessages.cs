namespace Mixdrome.Phone.Services
{
    using System.Runtime.Serialization;

    // Linked into the audio component so both processes share one wire contract.
#if BACKGROUND_AUDIO_AGENT
    internal static class BackgroundAudioMessages
#else
    public static class BackgroundAudioMessages
#endif
    {
        public const string Ready = "Ready";
        public const string RequestState = "RequestState";
        public const string Snapshot = "Snapshot";
        public const string ReplaceQueue = "ReplaceQueue";
        public const string QueueFile = "QueueFile";
        public const string QueueVersion = "QueueVersion";
        public const string Session = "Session";
        public const string Sequence = "Sequence";
        public const string CanNext = "CanNext";
        public const string CurrentIndex = "CurrentIndex";
        public const string TrackChanged = "TrackChanged";
        public const string StateChanged = "StateChanged";
        public const string PlayerState = "PlayerState";
        public const string Failure = "Failure";
        public const string Play = "Play";
        public const string Pause = "Pause";
        public const string Next = "Next";
        public const string Previous = "Previous";
        public const string SelectTrack = "SelectTrack";
        public const string Seek = "Seek";
        public const string Shuffle = "Shuffle";
        public const string Repeat = "Repeat";
        public const string Position = "Position";
        public const string SessionFile = "MixDromeAudioQueue.json";
        public const string TransferFilePrefix = "MixDromeAudioTransfer-";
    }

    [DataContract]
#if BACKGROUND_AUDIO_AGENT
    internal sealed class TrackInfo
#else
    public sealed class TrackInfo
#endif
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Artist { get; set; }
        [DataMember] public string Album { get; set; }
        [DataMember] public string CoverArt { get; set; }
        [DataMember] public string StreamUrl { get; set; }
        [DataMember] public int Duration { get; set; }
        [DataMember(EmitDefaultValue = false)] public bool IsMix { get; set; }
        [DataMember(EmitDefaultValue = false)] public string MixTitle { get; set; }
        [DataMember(EmitDefaultValue = false)] public PhoneReportingProfile Reporting { get; set; }
    }

    [DataContract]
#if BACKGROUND_AUDIO_AGENT
    internal sealed class PhoneReportingProfile
#else
    public sealed class PhoneReportingProfile
#endif
    {
        [DataMember] public string BaseUrl { get; set; }
        [DataMember] public string Username { get; set; }
        [DataMember] public string AuthenticationMode { get; set; }
        [DataMember] public string CredentialResource { get; set; }
    }
}
