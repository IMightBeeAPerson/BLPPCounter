using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PleaseWork.Counters
{
    internal interface IMyCounters
    {
        void SetupData(string id, string hash, string diff, string mode, string mapData);
        void UpdateCounter(float acc, int notes, int badNotes, int fcScore);
    }
}
