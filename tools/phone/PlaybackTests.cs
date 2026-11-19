using System;
using System.Collections.Generic;
using Mixdrome.Phone.Services;
public static class PhonePlaybackTests
{
    static int count;
    static void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
    static List<TrackInfo> Tracks(int count) {
        var tracks = new List<TrackInfo>();
        for (int i=0;i<count;i++) tracks.Add(new TrackInfo { Id="duplicate", Title="Track "+i, StreamUrl="https://example.invalid/stream/"+i });
        return tracks;
    }
    static void Main() {
        var queue = new AudioQueueState(new Random(7));
        Check(!queue.Replace(null,0), "reject null");
        Check(!queue.Replace(Tracks(3),3), "reject bad position");
        Check(!queue.Replace(new List<TrackInfo>{ new TrackInfo{ StreamUrl="file:///C:/secret"}},0), "reject file URL");
        Check(queue.CurrentIndex == -1, "validation doesn't change empty queue");
        Check(queue.Replace(Tracks(3),1), "replace");
        Check(queue.CurrentIndex==1, "duplicate occurrence initial");
        Check(queue.Next(false) && queue.CurrentIndex==2, "next distinct duplicate occurrence");
        Check(!queue.Next(true), "stop at end");
        Check(!queue.CanNext, "disabled boundary");
        Check(queue.Previous() && queue.CurrentIndex==1, "previous");
        queue.SetRepeat(2);
        Check(queue.Next(true) && queue.CurrentIndex==1, "repeat one automatic only");
        Check(queue.Next(false) && queue.CurrentIndex==2, "manual skip repeat one");
        Check(!queue.Next(false), "manual boundary repeat one");
        queue.SetRepeat(1);
        Check(queue.Next(true) && queue.CurrentIndex==0, "repeat all wraps");
        queue.SetRepeat(0);
        queue.SetShuffle(true);
        Check(queue.CurrentIndex==0, "shuffle keeps occurrence");
        var seen = new HashSet<int>{queue.CurrentIndex};
        while(queue.Next(true)) Check(seen.Add(queue.CurrentIndex), "shuffle visits once");
        Check(seen.Count==3,"shuffle visits all");
        queue.SetRepeat(1);
        int previous=queue.CurrentIndex;
        Check(queue.Next(true) && queue.CurrentIndex!=previous, "no immediate shuffle repeat");
        seen=new HashSet<int>{queue.CurrentIndex};
        for(int i=1;i<3;i++) Check(queue.Next(true) && seen.Add(queue.CurrentIndex),"complete next shuffle cycle");
        Check(queue.Select(1) && queue.CurrentIndex==1,"index selection");
        Check(!queue.Select(-1) && queue.CurrentIndex==1,"invalid selection preserves");
        Check(!queue.Replace(new List<TrackInfo>{null},0) && queue.CurrentIndex==1,"invalid replace preserves");
        var snapshot = new AudioQueueSnapshot { Version="version", Tracks=Tracks(3) };
        snapshot.Tracks[1].IsMix = true; snapshot.Tracks[1].MixTitle = "Mix & 音";
        var roundtrip=AudioJson.Deserialize<AudioQueueSnapshot>(AudioJson.Serialize(snapshot));
        Check(roundtrip.Tracks[1].IsMix && roundtrip.Tracks[1].MixTitle == "Mix & 音" && !roundtrip.Tracks[0].IsMix, "mix presentation survives background queue persistence without changing ordinary songs");
        Check(roundtrip.Version=="version" && roundtrip.Tracks.Count==3 && roundtrip.Tracks[2].Title=="Track 2", "queue wire roundtrip");
        Check(queue.Replace(Tracks(1),0),"single track");
        Check(queue.Next(true) && queue.CurrentIndex==0,"single repeat all");
                var gate = new AudioStateVersionGate();
        Check(!gate.Accept(null,1), "reject missing session");
        Check(!gate.Accept("first",-1), "reject missing sequence");
        Check(gate.Accept("first",1), "accept initial state");
        Check(!gate.Accept("first",1), "reject duplicate state");
        Check(!gate.Accept("first",0), "reject stale state");
        Check(gate.IsCurrent("first",1), "initial file read remains current");
        Check(gate.Accept("first",2), "accept next state");
        Check(!gate.IsCurrent("first",1), "reject late file result");
        Check(gate.Accept("second",1), "task restart resets sequence");
        Check(!gate.Accept("first",3), "reject retired task messages");
        Check(gate.IsCurrent("second",1), "retired messages preserve current session");
        Console.WriteLine("PASS: "+count+" production phone audio queue and message-order assertions. Native audio/UI remain unverified.");
    }
}
