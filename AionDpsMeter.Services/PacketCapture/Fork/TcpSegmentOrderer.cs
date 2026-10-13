// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Services.PacketCapture.Fork
{
    /// <summary>
    /// Puts each TCP stream back in order before it is parsed. A segment that arrives early waits until the gap before
    /// it fills; a resend is dropped, an overlapping one trimmed. Upstream jumped over every gap and then dropped the
    /// late segment as old, losing its hits (841 such gaps in a user's logs, mostly one 1412-byte segment).
    /// </summary>
    public sealed class TcpSegmentOrderer
    {
        /// <summary>A gap still open after this long is given up: its bytes are lost, as upstream would lose them.</summary>
        public const long GapWaitMs = 500;

        /// <summary>A jump this far is a new connection on the same port (reconnects jumped 100M+), not a gap.</summary>
        private const long NewConnectionJump = 4 * 1024 * 1024;

        /// <summary>No more than this waits for one gap.</summary>
        private const int MaxPendingBytes = 512 * 1024;

        private sealed record Segment(uint Seq, byte[] Data, bool Fin, long At);

        private sealed class Stream
        {
            public uint Expected;
            public readonly List<Segment> Pending = [];
        }

        private readonly Dictionary<string, Stream> streams = new(StringComparer.Ordinal);

        /// <summary>The bytes ready to parse, in order (possibly none, possibly earlier segments that were waiting).</summary>
        /// <param name="nowMs">Arrival time, to give up a gap that does not fill.</param>
        public IReadOnlyList<byte[]> Accept(string stream, uint seq, byte[] payload, long nowMs, bool syn = false, bool fin = false)
        {
            var output = new List<byte[]>();
            if (syn || !streams.TryGetValue(stream, out var st))
            {
                // First segment seen (or a new connection): it sets where the stream is.
                st = new Stream();
                streams[stream] = st;
                if (syn) seq++;
                st.Expected = seq + (uint)payload.Length + (fin ? 1u : 0u);
                if (payload.Length > 0) output.Add(payload);
                return output;
            }

            long ahead = (int)(seq - st.Expected); // serial arithmetic: survives the 2^32 wrap
            if (Math.Abs(ahead) > NewConnectionJump)
            {
                st.Pending.Clear();
                st.Expected = seq + (uint)payload.Length + (fin ? 1u : 0u);
                if (payload.Length > 0) output.Add(payload);
                return output;
            }

            if (ahead > 0)
            {
                if (payload.Length > 0 && !st.Pending.Any(p => p.Seq == seq))
                    st.Pending.Add(new Segment(seq, payload, fin, nowMs));
                if (nowMs - st.Pending.Min(p => p.At) > GapWaitMs || st.Pending.Sum(p => p.Data.Length) > MaxPendingBytes)
                    SkipGap(st, output);
                return output;
            }

            Consume(st, new Segment(seq, payload, fin, nowMs), output);
            Drain(st, output);
            return output;
        }

        public void Clear() => streams.Clear();

        private static void Consume(Stream st, Segment s, List<byte[]> output)
        {
            long overlap = (int)(st.Expected - s.Seq);
            if (overlap >= s.Data.Length)
            {
                if (overlap == s.Data.Length && s.Fin) st.Expected++;
                return; // all of it was already parsed: a resend
            }
            output.Add(overlap > 0 ? s.Data[(int)overlap..] : s.Data);
            st.Expected = s.Seq + (uint)s.Data.Length + (s.Fin ? 1u : 0u);
        }

        private static void Drain(Stream st, List<byte[]> output)
        {
            while (true)
            {
                var next = st.Pending.FirstOrDefault(p => (int)(p.Seq - st.Expected) <= 0);
                if (next is null) return;
                st.Pending.Remove(next);
                Consume(st, next, output);
            }
        }

        private static void SkipGap(Stream st, List<byte[]> output)
        {
            var first = st.Pending.MinBy(p => (int)(p.Seq - st.Expected))!;
            st.Expected = first.Seq;
            Drain(st, output);
        }
    }
}
