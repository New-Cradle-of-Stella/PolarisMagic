namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 一次施法自己的时钟。单位是原版的"帧"（<c>fcnt</c>），不是秒：原版魔法的全部数值都按帧写死，
    /// 换成秒会让作者在两套单位之间来回换算。
    ///
    /// 时钟只在 holder 每 Tick 推进一次，作者读到的值在整个 Tick 内是稳定的。
    /// </summary>
    public sealed class MagicClock
    {
        /// <summary>本 Tick 的帧增量。原版正常速度下是 1，时停/慢放时更小。</summary>
        public float DeltaFrames { get; private set; }

        /// <summary>Task 开始以来累计的帧数。</summary>
        public float ElapsedFrames { get; private set; }

        /// <summary>Task 开始以来推进过的 Tick 次数。</summary>
        public int Ticks { get; private set; }

        internal void Advance(float deltaFrames)
        {
            DeltaFrames = deltaFrames;
            ElapsedFrames += deltaFrames;
            Ticks++;
        }
    }
}
