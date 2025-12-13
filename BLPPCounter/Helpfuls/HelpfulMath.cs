using BeatLeader.Models.Replay;
using BLPPCounter.CalculatorStuffs;
using System.Collections;
using System.Collections.Generic;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulMath
    {
        /* NOTE MULTIPLIER CALCULATIONS
         * at/after 0 notes: x1, 0% filled,  score: 0
         * after 1 note:     x2, 0% filled,  score: 0 + 115 * 1 = 115 (1st note is x1)
         * after 2 notes:    x2, 25% filled, score: 115 + 115 * 2 = 345
         * after 3 notes:    x2, 50% filled,  score: 345 + 115 * 2 = 575 
         * after 4 notes:    x2, 75% filled, score: 575 + 115 * 2 = 805
         * after 5 notes:    x4, 0% filled, score: 805 + 115 * 2 = 1,035 (5th note is x2)
         * after 6 notes:    x4, 12.5% filled, score: 1,035 + 115 * 4 = 1,495
         * after 7 notes:    x4, 25% filled,  score: 1,495 + 115 * 4 = 1,955 
         * after 8 notes:    x4, 37.5% filled,  score: 1,955 + 115 * 4 = 2,415 
         * after 9 notes:    x4, 50% filled,  score: 2,415 + 115 * 4 = 2,875 
         * after 10 notes:   x4, 62.5% filled,  score: 2,875 + 115 * 4 = 3,335 
         * after 11 notes:   x4, 75% filled,  score: 3,335 + 115 * 4 = 3,795 
         * after 12 notes:   x4, 87.5% filled,  score: 3,795 + 115 * 4 = 4,225
         * after 13 notes:   x8, 0% filled,  score: 4,225 + 115 * 4 = 4,715 (13th note is x4)
         
         * for all notes 14 and beyond, multiplier is x8, each note adds 115 * 8 = 920
         * after 14 notes:    x8, 0% filled, score: 4,715 + 920 = 5,635
         */
        public static int MaxScoreForNotes(int notes)
        {
            if (notes <= 0) return 0;
            if (notes < 6) return 115 * (notes * 2 - 1);
            if (notes < 14) return 115 * ((notes - 5) * 4 + 9);
            return 920 * (notes - 14) + 5635;
        }
        public static int NotesForMaxScore(int score)
        {
            if (score <= 0) return 0;
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
        public static float ClampedMultiplierForNote(int notes)
        {
            if (notes >= 14) return 1f;
            if (notes < 2) return 0.125f;
            if (notes < 6) return 0.25f;
            if (notes < 14) return 0.5f;
            return 1f;
        }
        public static int DecreaseMultiplier(int notes)
        {
            if (notes >= 14) return 6;
            if (notes >= 6) return 2;
            return 0;
        }
        public static int GetMaxScoreFromNotes(IEnumerable<NoteEvent> notes)
        {
            int score = 0, mult = 1, multCount = 1, multMax = 2;
            foreach (NoteEvent note in notes)
            {
                score += Calculator.GetMaxCutScore(note) * mult;
                if (mult < 8 && ++multCount >= multMax)
                {
                    mult *= 2;
                    multCount = 0;
                    multMax *= 2;
                }
            }
            return score;
        }
        public static int GetTotalScoreFromNotes(IEnumerable<NoteEvent> notes)
        {
            int score = 0, mult = 1, multCount = 1, multMax = 2;
            foreach (NoteEvent note in notes)
            {
                score += Calculator.GetCutScore(note) * mult;
                if (mult < 8 && ++multCount >= multMax)
                {
                    mult *= 2;
                    multCount = 0;
                    multMax *= 2;
                }
            }
            return score;
        }
        public static float GetAccuracyFromNotes(IEnumerable<NoteEvent> notes) =>
            (float)GetTotalScoreFromNotes(notes) / GetMaxScoreFromNotes(notes);
    }
}
