using System;
using rowemod.Mods;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; }

    private static float Read(ReplayPropTrack<float> track, float time)
    {
        Check(track.Evaluate(time, out var from, out var to, out float blend), "visible sample at " + time);
        return from.Value + (to.Value - from.Value) * blend;
    }

    private static void Main()
    {
        var track = new ReplayPropTrack<float>();
        Check(!track.Evaluate(0, out _, out _, out _), "empty track hidden");
        track.Observe(1, 60); track.Record(1, 1, true, 10);
        track.Observe(2, 60); track.Record(2, 1, true, 20);
        track.Observe(3, 60); track.Record(3, 1, false, 0);
        track.Observe(4, 60); track.Record(4, 2, true, 80);
        track.Observe(5, 60); track.Record(5, 2, true, 100);
        track.Observe(6, 60); track.Record(6, 2, false, 0);
        Check(!track.Evaluate(.99f, out _, out _, out _), "hidden before first gesture");
        Check(Read(track, 1.5f) == 15, "interpolates recorded poses");
        Check(Read(track, 2.5f) == 20, "does not blend toward stop marker");
        Check(!track.Evaluate(3, out _, out _, out _), "stop marker hides immediately");
        Check(!track.Evaluate(3.5f, out _, out _, out _), "gap between gestures stays hidden");
        Check(Read(track, 4.5f) == 90, "second gesture interpolates independently");
        Check(!track.Evaluate(6, out _, out _, out _), "second stop hides");
        Check(!track.Evaluate(8, out _, out _, out _), "outside recording hidden");
        // Scrubbing and reverse order must be exactly as deterministic as forward play.
        foreach (float time in new[] {4.25f, 1.75f, 4.25f, 1f, 2f, 4.25f})
        {
            float expected = time < 3 ? time * 10 : 80 + (time - 4) * 20;
            Check(Read(track, time) == expected, "random seek/reverse stable at " + time);
        }
        Check(Read(track, 1.5f) == Read(track, 1.5f), "paused repeated evaluation stable");
        track.Observe(7, 60); track.Record(7, 3, true, 200);
        track.Observe(8, 60); track.Record(8, 4, true, 500);
        Check(Read(track, 7.5f) == 200, "never interpolates across separate plays of same prop");
        track.Record(8, 4, false, 0);
        Check(!track.Evaluate(8, out _, out _, out _), "same-timestamp latest visibility wins");
        Check(track.Observe(2.25f, 60), "native timeline rewind detected");
        Check(!track.Evaluate(2.25f, out _, out _, out _), "rewind ends carried prop");
        Check(!track.Evaluate(4.5f, out _, out _, out _), "rewind truncates future gesture");
        Check(Read(track, 1.5f) == 15, "rewind retains earlier history");
        track.Record(2.25f, 5, true, 25);
        Check(Read(track, 2.25f) == 25, "new recording branches at rewind");
        track.Observe(2.26f, 60); track.Record(2.26f, 5, true, 26);
        Check(Read(track, 2.26f) == 25, "sampling limits high-frequency duplicate poses");
        track.Record(2.26f, 5, false, 0);
        Check(!track.Evaluate(2.26f, out _, out _, out _), "visibility transition bypasses sampling throttle");
        int count = track.Count;
        track.Record(float.NaN, 1, true, 0); track.Record(float.PositiveInfinity, 1, true, 0);
        Check(track.Count == count, "invalid timestamps rejected");
        Check(!track.Evaluate(float.NaN, out _, out _, out _), "invalid playback time hidden");

        var bounded = new ReplayPropTrack<float>(8);
        for (int i = 0; i < 20; i++) { bounded.Observe(i, 60); bounded.Record(i, 1, true, i); }
        Check(bounded.Count == 8, "memory capacity bounded");
        Check(!bounded.Evaluate(1, out _, out _, out _), "evicted history hidden");
        Check(Read(bounded, 18.5f) == 18.5f, "retained buffer interpolates");
        bounded.Observe(20, 2);
        Check(bounded.Count <= 3, "native time buffer expires old samples");
        Check(Read(bounded, 18.5f) == 18.5f, "buffer edge keeps interpolation predecessor");
        bounded.Clear();
        Check(bounded.Count == 0 && !bounded.Evaluate(19, out _, out _, out _), "scene/mod cleanup clears timeline");
        Console.WriteLine($"PASS: {checks} replay prop timeline checks. Native renderer/editor playback tested separately.");
    }
}
