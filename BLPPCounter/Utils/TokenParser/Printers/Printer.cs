using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.TokenParser.FormatTypes;
using System.Collections.Generic;
using System.Text;

namespace BLPPCounter.Utils.TokenParser.Printers
{
    internal class Printer
    {
#nullable enable
        private readonly string[] outputChunks;
        private readonly int[][] dependencies;
        private readonly StringBuilder sb;
        private readonly FormatWrapper inputValues;
        private readonly char[] keys;
        private readonly (int index, Parameter p)[] parameters;
        private readonly object?[] values;
        private readonly HashSet<int> boolIndexes;

        public FormatWrapper Values => inputValues;
        public HashSet<char> UsedKeys => [.. keys];

        public Printer(string[] outputChunks, int[][] dependencies, FormatWrapper inputValues, char[] keys, (int index, Parameter p)[] parameters)
        {
            this.outputChunks = outputChunks;
            this.dependencies = dependencies;
            this.inputValues = inputValues;
            this.keys = keys;
            this.parameters = parameters;
            values = new object?[keys.Length];
            sb = new();
            boolIndexes = [];
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] < 30 && inputValues.GetValueType(keys[i]) == typeof(bool))
                    boolIndexes.Add(i);
            }
            //Plugin.Log.Info("Bool indexes: " + string.Join(", ", boolIndexes));
        }

        private void SetValues(ref int paramIndex)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (paramIndex < parameters.Length && parameters[paramIndex].index == i)
                {
                    values[i] = Tokens.TryParseParameter(parameters[paramIndex].p, inputValues);
                    paramIndex++;
                    continue;
                }
                values[i] = boolIndexes.Contains(i) ? (bool)inputValues[keys[i]] ? true : null : inputValues[keys[i]];
            }
        }
        public string Print()
        {
            sb.Clear();
            int paramIndex = 0;
            SetValues(ref paramIndex);
            //Plugin.Log.Info($"Values: {string.Join(", ", values)}");
            //Plugin.Log.Info($"Keys: {string.Join(", ", keys)}");
            for (int i = 0; i < outputChunks.Length; i++)
            {
                if (ValidateDependency(dependencies[i]))
                    sb.Append(string.Format(outputChunks[i], values));
            }
            //Plugin.Log.Info($"Printer output: {sb.ToString().Replace("\n", "\\n")}");
            
            return sb.ToString();
        }
        private bool ValidateDependency(int[] dependencies)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (dependencies[i] >= 0 && values[dependencies[i]] is null)
                    return false;
            }
            return true;
        }

        public override string ToString() => Print();
    }
}
