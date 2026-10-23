using System;
using System.Collections.Generic;

namespace rowemod.Mods
{
    // Native replay-clock samples. Playback is a pure lookup: pause, reverse, and
    // arbitrary seeks cannot advance or mutate the recorded prop's state.
    internal sealed class ReplayPropTrack<T>
    {
        internal readonly struct Sample
        {
            internal readonly float Time;
            internal readonly int Segment;
            internal readonly bool Visible;
            internal readonly T Value;
            internal Sample(float time, int segment, bool visible, T value)
            { Time = time; Segment = segment; Visible = visible; Value = value; }
        }

        private readonly List<Sample> _samples = new List<Sample>();
        private readonly int _capacity;
        internal int Count => _samples.Count;
        internal float RecordTime { get; private set; } = float.NegativeInfinity;
        internal ReplayPropTrack(int capacity = 60000) { _capacity = Math.Max(2, capacity); }

        internal bool Observe(float time, float historySeconds)
        {
            if (!float.IsFinite(time) || time < 0f) return false;
            bool rewound = time < RecordTime - .005f;
            RecordTime = time;
            if (rewound)
            {
                int firstFuture = Find(time) + 1;
                if (firstFuture < Count) _samples.RemoveRange(firstFuture, Count - firstFuture);
                Record(time, 0, false, default, true);
            }
            float cutoff = time - Math.Max(1f, historySeconds);
            int keep = Find(cutoff);
            if (keep > 0) _samples.RemoveRange(0, keep); // Keep the predecessor at the buffer edge.
            return rewound;
        }

        internal void Record(float time, int segment, bool visible, T value, bool force = false)
        {
            if (!float.IsFinite(time) || time < 0f || time < RecordTime - .005f) return;
            RecordTime = time;
            if (Count > 0)
            {
                Sample last = _samples[Count - 1];
                if (time < last.Time) return;
                if (time == last.Time) _samples.RemoveAt(Count - 1);
                else if (!force && last.Segment == segment && last.Visible == visible &&
                         (!visible || time - last.Time < 1f / 30f)) return;
            }
            if (Count >= _capacity) _samples.RemoveAt(0);
            _samples.Add(new Sample(time, segment, visible, value));
        }

        internal bool Evaluate(float time, out Sample from, out Sample to, out float blend)
        {
            from = to = default; blend = 0f;
            if (!float.IsFinite(time) || time > RecordTime + .05f) return false;
            int index = Find(time);
            if (index < 0 || !_samples[index].Visible) return false;
            from = to = _samples[index];
            if (index + 1 < Count)
            {
                Sample next = _samples[index + 1];
                if (next.Visible && next.Segment == from.Segment && next.Time > from.Time)
                {
                    to = next;
                    blend = Math.Clamp((time - from.Time) / (next.Time - from.Time), 0f, 1f);
                }
            }
            return true;
        }

        private int Find(float time)
        {
            int low = 0, high = Count - 1, result = -1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                if (_samples[middle].Time <= time) { result = middle; low = middle + 1; }
                else high = middle - 1;
            }
            return result;
        }

        internal void Clear() { _samples.Clear(); RecordTime = float.NegativeInfinity; }
    }
}
