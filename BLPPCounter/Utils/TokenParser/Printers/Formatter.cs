using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.TokenParser.FormatTypes;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BLPPCounter.Utils.TokenParser.Printers
{
    internal class Formatter
    {
        private Chunk[] chunks;
        private readonly StringBuilder sb;
        private readonly FormatWrapper tokenValues;
        private readonly List<string> outputChunks;
        private readonly List<int[]> dependencies;
        private readonly Stack<int> currentDependency;
        private readonly List<(int index, Parameter p)> parameters;
        private readonly List<char> tokenOrder;
        private readonly HashSet<char> promisedTokens;

        private bool chunksCombined;

        public FormatWrapper TokenValues => tokenValues;
#pragma warning disable IDE0057
#nullable enable
        public Formatter(Chunk[] arr, FormatWrapper tokenVals)
        {
            chunks = arr;
            sb = new();
            tokenValues = tokenVals;
            outputChunks = [];
            dependencies = [];
            currentDependency = [];
            parameters = [];
            tokenOrder = [];
            promisedTokens = [];
            chunksCombined = false;
            Setup();
        }

        private void Setup()
        {
            HashSet<char> usedTokens = [];
            SetupInternal(chunks, usedTokens);
            foreach (char t in usedTokens)
                tokenOrder.Add(t);
            tokenOrder.Sort((a, b) => b - a);
        }
        private static void SetupInternal(IEnumerable<Chunk> givenChunks, HashSet<char> usedTokens)
        {
            foreach (Chunk c in givenChunks)
            {
                if (c is Token)
                {
                    usedTokens.Add(c.GetValue()[0]);
                    continue;
                }
                if (c is Group g)
                {
                    if (g is not RichText)
                        usedTokens.Add(g.Symbol);
                    SetupInternal(g.Chunks, usedTokens);
                    continue;
                }
            }
        }
        public void PromiseValueForTokens(params char[] tokens) => PromiseValueForTokens((IEnumerable<char>)tokens);
        public void PromiseValueForTokens(IEnumerable<char> tokens)
        {
            foreach (char c in tokens)
                promisedTokens.Add(c);
        }
        public void PromiseValueForAllTokens() => PromiseValueForTokens(tokenOrder.Where(t => t > FormatWrapper.SPLIT));
        public void SetTokenToConstantValue(char token, string value = "")
        {
            int offset = 0;
            SetTokenToConstantValue_Internal(token, value, chunks, ref offset);
            tokenOrder.Remove(token);
        }
        private void SetTokenToConstantValue_Internal(char token, string value, Chunk[] arr, ref int typesOffset)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Chunk c = arr[i];
                if (c is Token && c.GetValue()[0] == token)
                {
                    arr[i] = new Chunk(value);
                }
                else if (c is Group g)
                {
                    if (g.Symbol == token)
                        g.Symbol = '\0';
                    Chunk[] groupArr = [.. g.Chunks];
                    int hold = i + typesOffset + 1;
                    SetTokenToConstantValue_Internal(token, value, groupArr, ref hold);
                    g.Chunks = [.. groupArr];
                    typesOffset = hold - i - 1 + g.Chunks.Count;
                }
            }
        }
        public void SurroundToken(char token, string start, string end) => SurroundTokens(start, end, token);
        public void SurroundTokens(string start, string end, params char[] tokens)
        {
            var (startChunks, startPlaceholders) = QuickParse(start);
            var (endChunks, endPlaceholders) = QuickParse(end);
            SurroundToken_Internal([.. tokens], startChunks, endChunks, startPlaceholders, endPlaceholders, ref chunks);
        }
        private static (Chunk?[] chunks, int[] placeholders) QuickParse(string s)
        {
            List<Chunk?> outp = [];
            List<int> placeholders = [];
            int index = s.IndexOf(Tokens.INSERT_SELF);
            while (index >= 0 && s.Length > 0)
            {
                if (index != 0)
                    outp.Add(new Chunk(s.Substring(0, index)));
                placeholders.Add(outp.Count);
                outp.Add(null);
                s = s.Substring(index + 1);
                index = s.IndexOf(Tokens.INSERT_SELF);
            }
            if (s.Length > 0)
                outp.Add(new Chunk(s));
            return ([.. outp], [.. placeholders]);
        }
        private static void SurroundToken_Internal(HashSet<char> tokens, Chunk?[] start, Chunk?[] end, int[] startPlaceholders, int[] endPlaceholders, ref Chunk[] arr)
        {
            List<Chunk> outp = [];
            for (int i = 0; i < arr.Length; i++)
            {
                Chunk c = arr[i];
                if (c is Token && tokens.Contains(c.GetValue()[0]))
                {
                    outp.ChangeValuesAndAdd(start, startPlaceholders, ch => ch is null ? c : ch);
                    outp.Add(c);
                    outp.ChangeValuesAndAdd(end, endPlaceholders, ch => ch is null ? c : ch);
                }
                else
                {
                    if (c is Group g)
                    {
                        bool isToken = tokens.Contains(g.Symbol);
                        Chunk[] groupArr = [.. g.Chunks];
                        SurroundToken_Internal(tokens, start, end, startPlaceholders, endPlaceholders, ref groupArr);
                        g.Chunks = [.. groupArr];
                        if (isToken)
                        {
                            Token t = new(g.Symbol);
                            g.Chunks.ChangeValuesAndPrepend(start, startPlaceholders, ch => ch is null ? t : ch);
                            g.Chunks.ChangeValuesAndAdd(end, endPlaceholders, ch => ch is null ? t : ch);
                        }
                    }
                    outp.Add(c);
                }
            }
            arr = [.. outp];
        }
        public Printer GetOutput()
        {
            CombineChunks();
            sb.Clear();
            Parse(chunks);
            return new([.. outputChunks], [.. dependencies], tokenValues, [.. tokenOrder], [.. parameters]);
        }
        private void Parse(IEnumerable<Chunk> chunks)
        {
            foreach (Chunk c in chunks)
            {
                if (c.GetType() == typeof(Chunk))
                {
                    sb.Append(c.GetValue());
                    continue;
                }
                if (c is Token)
                {
                    sb.Append($"{{{tokenOrder.IndexOf(c.GetValue()[0])}}}");
                    if (c is Parameter p)
                        parameters.Add((tokenOrder.IndexOf(c.GetValue()[0]), p));
                    continue;
                }
                if (c is Group g)
                {
                    if (c is RichText rt)
                    {
                        sb.Append(rt.GetStart());
                        Parse(rt.Chunks);
                        sb.Append(rt.GetEnd());
                        continue;
                    }
                    AddChunk();
                    int index = tokenOrder.IndexOf(g.Symbol);
                    if (index >= 0)
                        currentDependency.Push(index);
                    Parse(g.Chunks);
                    AddChunk();
                    if (index >= 0)
                        currentDependency.Pop();
                    continue;
                }
            }
            if (sb.Length != 0)
                AddChunk();
        }
        private void AddChunk()
        {
            if (sb.Length == 0)
                return;
            outputChunks.Add(sb.ToString());
            dependencies.Add(currentDependency.Count > 0 ? [.. currentDependency] : [-1]);
            sb.Clear();
        }
        private void CombineChunks()
        {
            if (chunksCombined)
                return;
            List<Chunk> combinedChunks = [];
            int offset = 0;
            CombineChunks_Internal(combinedChunks, chunks, ref offset);
            chunks = [.. combinedChunks];
            chunksCombined = true;
        }
        private void CombineChunks_Internal(List<Chunk> combinedChunks, Chunk[] arr, ref int typesOffset)
        {
            Chunk? last = null;
            for (int i = 0; i < arr.Length; i++)
            {
                if (last?.GetType() != arr[i].GetType())
                    goto end;
                if (arr[i].GetType() == typeof(Chunk))
                {
                    last = new Chunk(last.GetValue() + arr[i].GetValue());
                    continue;
                }
            end:
                if (last is not null) combinedChunks.Add(last);
                if (arr[i] is Group g)
                {
                    bool badGroup = promisedTokens.Contains(g.Symbol) || (g is not RichText && g.Symbol == '\0');
                    List<Chunk> combinedGroupChunks = [];
                    int hold = i + typesOffset + 1;
                    CombineChunks_Internal(combinedGroupChunks, [.. g.Chunks], ref hold);
                    typesOffset = hold - i - 1 + g.Chunks.Count;
                    if (badGroup)
                        combinedChunks.AddRange(combinedGroupChunks);
                    else
                        g.Chunks = [.. combinedGroupChunks];
                    if (badGroup)
                    {
                        last = null;
                        continue;
                    }
                }
                last = arr[i];
            }
            if (last is not null) combinedChunks.Add(last);
        }
    }
}
