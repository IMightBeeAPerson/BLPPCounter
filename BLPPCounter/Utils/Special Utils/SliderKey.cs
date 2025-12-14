namespace BLPPCounter.Utils.Special_Utils
{
    internal struct SliderKey(int timeMs, int lineIndex, NoteLineLayer lineLayer, ColorType color, NoteCutDirection cutDir)
    {
        public int timeMs = timeMs;
        public int lineIndex = lineIndex;
        public NoteLineLayer lineLayer = lineLayer;
        public ColorType color = color;
        public NoteCutDirection cutDir = cutDir;

        public override bool Equals(object obj)
        {
            if (!(obj is SliderKey)) return false;
            var o = (SliderKey)obj;
            return timeMs == o.timeMs && lineIndex == o.lineIndex && lineLayer == o.lineLayer && color == o.color && cutDir == o.cutDir;
        }
        public override readonly int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + timeMs;
                h = h * 31 + lineIndex;
                h = h * 31 + (int)lineLayer;
                h = h * 31 + (int)color;
                h = h * 31 + (int)cutDir;
                return h;
            }
        }
    }
}
