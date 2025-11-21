using BLPPCounter.CalculatorStuffs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace BLPPCounter.Utils.Misc_Classes
{
    public struct PPContainer
    {
        public int PPLength;
        public float TotalPP;
        public float AccPP;
        public float TechPP;
        public float PassPP;
        public int Precision;

        public PPContainer(int ppLength, float totalPP, float accPP = 0.0f, float passPP = 0.0f, float techPP = 0.0f, int precision = -1)
        {
            PPLength = ppLength;
            TotalPP = totalPP;
            AccPP = accPP;
            PassPP = passPP;
            TechPP = techPP;
            Precision = precision;
            RoundAll();
        }
        public PPContainer(int ppLength, float[] ppVals, int precision = -1)
        {
            if (ppVals is null || ppVals.Length == 0)
                throw new ArgumentException("ppVals cannot be null or empty", nameof(ppVals));
            if (ppVals.Length == 4)
            {
                AccPP = ppVals[0];
                PassPP = ppVals[1];
                TechPP = ppVals[2];
                TotalPP = ppVals[3];
            }
            else
            {
                TotalPP = ppVals[0];
                AccPP = 0.0f;
                TechPP = 0.0f;
                PassPP = 0.0f;
            }
            PPLength = ppLength;
            Precision = precision;
        }

        public void SetValues(float[] ppVals)
        {
            if (ppVals is null || ppVals.Length == 0)
                throw new ArgumentException("ppVals cannot be null or empty", nameof(ppVals));
            if (ppVals.Length == 4)
            {
                AccPP = ppVals[0];
                PassPP = ppVals[1];
                TechPP = ppVals[2];
                TotalPP = ppVals[3];
            } 
            else
                TotalPP = ppVals[0];

        }
        public void SetValues(PPContainer other)
        {
            TotalPP = other.TotalPP;
            AccPP = other.AccPP;
            TechPP = other.TechPP;
            PassPP = other.PassPP;
        }
        public void RoundAll()
        {
            if (Precision < 0) return;
            TotalPP = Round(TotalPP);
            AccPP = Round(AccPP);
            TechPP = Round(TechPP);
            PassPP = Round(PassPP);
        }
        public readonly IEnumerable<float> ToEnumerable() => PPLength == 1 ? [TotalPP] : [AccPP, PassPP, TechPP, TotalPP];

        private readonly float Round(float a) => Precision < 0 ? a : (float)Math.Round(a, Precision);

        public float this[int i] { 
            get 
            {
                if (PPLength < 4)
                {
                    if (i != 0)
                        throw new IndexOutOfRangeException("Index must be 0 when PPContainer length is less than 4.");
                    return TotalPP;
                }
                switch (i)
                {
                    case 0: return AccPP;
                    case 1: return TechPP;
                    case 2: return PassPP;
                    case 3: return TotalPP;
                    default: throw new IndexOutOfRangeException("Index must be between 0 and 3, inclusive.");
                }
            }
        }
        public static PPContainer operator +(PPContainer a) => a;
        public static PPContainer operator +(PPContainer a, float b) => 
            new PPContainer(a.PPLength, a.TotalPP + b, a.AccPP + b, a.TechPP + b, a.PassPP + b, a.Precision);
        public static PPContainer operator +(PPContainer a, PPContainer b) => 
            new PPContainer(a.PPLength, a.TotalPP + b.TotalPP, a.AccPP + b.AccPP, a.TechPP + b.TechPP, a.PassPP + b.PassPP, Math.Max(a.Precision, b.Precision));
        public static PPContainer operator -(PPContainer a) =>
            new PPContainer(a.PPLength, -a.TotalPP, -a.AccPP, -a.TechPP, -a.PassPP, a.Precision);
        public static PPContainer operator -(PPContainer a, float b) => 
            new PPContainer(a.PPLength, a.TotalPP - b, a.AccPP - b, a.TechPP - b, a.PassPP - b, a.Precision);
        public static PPContainer operator -(PPContainer a, PPContainer b) => 
            new PPContainer(a.PPLength, a.TotalPP - b.TotalPP, a.AccPP - b.AccPP, a.TechPP - b.TechPP, a.PassPP - b.PassPP, Math.Max(a.Precision, b.Precision));
        public static PPContainer operator -(PPContainer a, float[] b)
        {
            if (a.PPLength > b.Length)
                throw new ArgumentException("Array length is less than PPContainer length", nameof(b));
            return a.PPLength == 1 ?
                new PPContainer(a.PPLength, a.TotalPP - b[0], precision: a.Precision) :
                new PPContainer(a.PPLength, a.TotalPP - b[0], a.AccPP - b[1], a.TechPP - b[2], a.PassPP - b[3], a.Precision);
        }
        public static void SubtractFast(in PPContainer lside, in PPContainer rside, ref PPContainer resultStorage)
        {
            resultStorage.TotalPP = lside.TotalPP - rside.TotalPP;
            resultStorage.AccPP = lside.AccPP - rside.AccPP;
            resultStorage.TechPP = lside.TechPP - rside.TechPP;
            resultStorage.PassPP = lside.PassPP - rside.PassPP;
            resultStorage.RoundAll();
        }
        public static void MultiplyFast(ref PPContainer lside, float multiplier)
        {
            lside.TotalPP *= multiplier;
            lside.AccPP *= multiplier;
            lside.TechPP *= multiplier;
            lside.PassPP *= multiplier;
            lside.RoundAll();
        }
    }
}
