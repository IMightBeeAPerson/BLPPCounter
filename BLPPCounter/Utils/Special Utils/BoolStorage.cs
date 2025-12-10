using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.Special_Utils
{
    public class BoolStorage
    {
        private const byte DATA_LENGTH = 64;
        private List<ulong> bools;
        private ulong currentBool;
        private byte currentStored;

        public int Length => bools.Count * DATA_LENGTH + currentStored;
        public int CurrentLength => currentStored;
        public BoolStorage()
        {
            bools = new List<ulong>();
            currentStored = 0;
            currentBool = 0;
        }
        public BoolStorage(params bool[] bools) : this(bools.AsEnumerable())
        {}
        public BoolStorage(IEnumerable<bool> bools) : this()
        {
            foreach (bool b in bools)
                AddBool(b);
        }
        public BoolStorage(int currentStored, params ulong[] bools)
        {
            currentBool = bools[bools.Length - 1];
            this.bools = bools.Take(bools.Length - 1).ToList();
            this.currentStored = (byte)currentStored;
        }
        public BoolStorage(int currentStored, IEnumerable<ulong> bools)
        {
            currentBool = bools.Last();
            this.bools = bools.Take(bools.Count() - 1).ToList();
            this.currentStored = (byte)currentStored;
        }
        public BoolStorage(int dummyItems, bool initToTrue = false)
        {
            bools = new List<ulong>();
            for (int i = DATA_LENGTH; i < dummyItems; i += DATA_LENGTH)
                bools.Add(initToTrue ? ulong.MaxValue : 0UL);
            currentStored = (byte)(dummyItems % DATA_LENGTH);
            currentBool = initToTrue ? (ulong.MaxValue >> (DATA_LENGTH - currentStored)) : 0;
        }
        public void AddBool(bool value)
        {
            currentBool += (value ? 1UL : 0UL) << currentStored++;
            if (currentStored == DATA_LENGTH)
            {
                bools.Add(currentBool);
                currentBool = 0;
                currentStored = 0;
            }
        }
        public void ChangeBool(bool newVal, int index)
        {
            int listIndex = index / DATA_LENGTH;
            index %= DATA_LENGTH;
            if (listIndex > bools.Count)
            {
                bools.Add(currentStored);
                currentBool = 0;
                currentStored = 0;
                while (listIndex > bools.Count)
                    bools.Add(0UL);
            }
            if (listIndex == bools.Count)
            {
                if (newVal) currentBool |= 1UL << index;
                else currentBool &= ~(1UL << index);
                currentStored = Math.Max((byte)index, currentStored);
            }
            else
                if (newVal) bools[listIndex] |= 1UL << index;
                else bools[listIndex] &= ~(1UL << index);
        }
        public bool GetBool(int index)
        {
            int listIndex = index / DATA_LENGTH;
            index %= DATA_LENGTH;
            if (listIndex == bools.Count)
            {
                if (index > currentStored)
                    throw new IndexOutOfRangeException($"The index given is greater than the amount of bools stored (index = {index}, stored = {currentStored})");
                return (currentBool >> index) % 2 == 1;
            }
            if (listIndex > bools.Count)
                throw new IndexOutOfRangeException($"The index given is greater than the amount of bools stored");
            return (bools[listIndex] >> index) % 2 == 1;
        }
        public ulong[] GetVals() => bools.Append(currentBool).ToArray();
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
            ulong[] arr = new ulong[bigger.bools.Count];
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
                bools = new List<ulong>(arr),
                currentBool = bigger.currentBool,
                currentStored = bigger.currentStored,
            };
        }
        public bool this[int index]
        {
            get => GetBool(index);
            set => ChangeBool(value, index);
        }
    }
}
