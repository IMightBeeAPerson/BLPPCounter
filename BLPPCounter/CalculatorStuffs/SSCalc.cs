﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.CalculatorStuffs
{
    public static class SSCalc
    {
        private static readonly float SecretMultiplier = 42.117208413f;
        private static readonly List<(double, double)> pointList = new List<(double, double)>()
        {
            (0, 0),
            (0.6, 0.182232335),
            (0.65, 0.586601),
            (0.7, 0.6125566),
            (0.75, 0.6451808),
            (0.8, 0.6872269),
            (0.825, 0.7150466),
            (0.85, 0.746229053),
            (0.875, 0.781693459),
            (0.9, 0.825756133),
            (0.91, 0.8488376),
            (0.92, 0.872871041),
            (0.93, 0.9039994),
            (0.94, 0.9417363),
            (0.95, 1),
            (0.955, 1.0388633),
            (0.96, 1.08718836),
            (0.965, 1.155212),
            (0.97, 1.24858081),
            (0.9725, 1.30903327),
            (0.975, 1.38071024),
            (0.9775, 1.46647263),
            (0.98, 1.570241),
            (0.9825, 1.69753623),
            (0.985, 1.85638881),
            (0.9875, 2.058947),
            (0.99, 2.32450628),
            (0.99125, 2.49029064),
            (0.9925, 2.68566775),
            (0.99375, 2.91901565),
            (0.995, 3.20220184),
            (0.99625, 3.55261445),
            (0.9975, 3.99679351),
            (0.99825, 4.32502747),
            (0.999, 4.715471),
            (0.9995, 5.01954365),
            (1, 5.36739445)
        };
        static SSCalc()
        {
            pointList.Reverse();
        }
        public static float GetPP(float acc, float stars) => SecretMultiplier * (float)BLCalc.GetCurve(acc, pointList) * stars;
    }
}
