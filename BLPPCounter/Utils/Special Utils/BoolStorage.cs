using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Special_Utils
{
    internal class BoolStorage
    {
        private List<uint> bools;
        private uint currentBool;
        byte currentStored;
        private BoolStorage()
        {
            bools = new List<uint>();
            currentStored = 0;
            currentBool = 0;
        }
        public BoolStorage(params bool[] bools) : this()
        {
            foreach (bool b in bools)
                AddBool(b);
        }
        public BoolStorage(int currentStored, params int[] bools)
        {
            currentBool = (uint)bools[bools.Length - 1];
            this.bools = bools.Take(bools.Length - 1).Select(i => (uint)i).ToList();
            this.currentStored = (byte)currentStored;
        }
        public void AddBool(bool value)
        {
            currentBool += (value ? 1u : 0u) << currentStored++;
            if (currentStored == 32)
            {
                bools.Add(currentBool);
                currentBool = 0;
                currentStored = 0;
            }
        }
        public int[] GetVals() => bools.Select(u => (int)u).Append((int)currentBool).ToArray();
        public static bool SameLength(BoolStorage a, BoolStorage b) => a.bools.Count == b.bools.Count && a.currentStored == b.currentStored;
        public static BoolStorage operator ^(BoolStorage a, BoolStorage b)
        {
            int i = 0;
            BoolStorage bigger, smaller;
            if (a.bools.Count > b.bools.Count)
            {
                bigger = a;
                smaller = b;
            } else
            {
                bigger = b;
                smaller = a;
            }
            uint[] arr = new uint[bigger.bools.Count];
            for (; i < smaller.bools.Count; i++)
                arr[i] = a.bools[i] ^ b.bools[i];
            if (a.bools.Count == b.bools.Count)
                arr[i] = a.currentBool ^ b.currentBool;
            else
                arr[i] = smaller.currentBool ^ bigger.bools[i];
            for (i++; i < bigger.bools.Count; i++)
                arr[i] = bigger.bools[i];
            return new BoolStorage()
            {
                bools = new List<uint>(arr),
                currentBool = bigger.currentBool,
                currentStored = bigger.currentStored,
            };
        }
    }
}
