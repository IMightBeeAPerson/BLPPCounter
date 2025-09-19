using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Special_Utils
{
    internal struct SliderKey
    {
        public int timeMs;
        public int lineIndex;
        public NoteLineLayer lineLayer;
        public ColorType color;
        public NoteCutDirection cutDir;

        public SliderKey(int timeMs, int lineIndex, NoteLineLayer lineLayer, ColorType color, NoteCutDirection cutDir)
        {
            this.timeMs = timeMs;
            this.lineIndex = lineIndex;
            this.lineLayer = lineLayer;
            this.color = color;
            this.cutDir = cutDir;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SliderKey)) return false;
            var o = (SliderKey)obj;
            return timeMs == o.timeMs && lineIndex == o.lineIndex && lineLayer == o.lineLayer && color == o.color && cutDir == o.cutDir;
        }
        public override int GetHashCode()
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
