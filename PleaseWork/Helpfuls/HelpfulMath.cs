using System;

namespace PleaseWork.Helpfuls
{
    public static class HelpfulMath
    {
        public static int MaxScoreForNotes(int notes)
        {
            if (notes <= 0) return 0;
            if (notes == 1) return 115;
            if (notes < 6) return 115 * (notes * 2 - 1);
            if (notes < 14) return 115 * ((notes - 5) * 4 + 9);
            return 920 * (notes - 14) + 5635;
        }
        public static int NotesForMaxScore(int score)
        {
            if (score <= 0) return 0;
            if (score == 115) return 1;
            if (score < 1495) return (score / 115 + 1) / 2;
            if (score < 5635) return (score / 115 - 9) / 4 + 5;
            return (score - 5635) / 920 + 14;
        }
        public static int MultiplierForNote(int notes)
        {
            if (notes >= 14) return 8;
            if (notes < 2) return 1;
            if (notes < 6) return 2;
            if (notes < 14) return 4;
            return 8;
        }
        public static int DecreaseMultiplier(int notes)
        {
            if (notes >= 14) return 6;
            if (notes >= 6) return 2;
            return 0;
        }
    }
}
