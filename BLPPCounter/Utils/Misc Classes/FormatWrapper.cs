using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

public class FormatWrapper
{
    // Tokens < SPLIT are treated as "low" (control chars/flags)
    // Tokens >= SPLIT are treated as "high" (letters, symbols, etc.)
    private const int SPLIT = 30;

    private readonly Type[] givenTypes;
    private readonly object[] values;
    private readonly int[] lookupLow, lookupHigh;
    private readonly int minTokenLow, minTokenHigh;
    private readonly char[] tokensChars;

    public FormatWrapper(params (Type type, char token)[] tokens)
    {
        if (tokens is null || tokens.Length < 1)
            throw new ArgumentException("There must be at least one type given.");

        givenTypes = new Type[tokens.Length];
        values = new object[tokens.Length];
        tokensChars = new char[tokens.Length];

        // Initialize min/max for each cluster
        minTokenLow = SPLIT;
        minTokenHigh = int.MaxValue;
        int maxTokenLow = -1, maxTokenHigh = SPLIT;

        // Partition tokens into low/high ranges and record type info
        for (int i = 0; i < tokens.Length; i++)
        {
            var (type, token) = tokens[i];
            givenTypes[i] = type;
            tokensChars[i] = token;

            if (token < SPLIT)
            {
                if (minTokenLow > token) minTokenLow = token;
                if (maxTokenLow < token) maxTokenLow = token;
            }
            else
            {
                if (minTokenHigh > token) minTokenHigh = token;
                if (maxTokenHigh < token) maxTokenHigh = token;
            }
        }

        // Safely allocate lookup arrays only if the cluster exists
        lookupLow = maxTokenLow >= 0
            ? Enumerable.Repeat(-1, maxTokenLow - minTokenLow + 1).ToArray()
            : Array.Empty<int>();

        lookupHigh = minTokenHigh != int.MaxValue
            ? Enumerable.Repeat(-1, maxTokenHigh - minTokenHigh + 1).ToArray()
            : Array.Empty<int>();

        // Fill lookup tables
        for (int i = 0; i < tokens.Length; i++)
        {
            char token = tokens[i].token;
            if (token < SPLIT)
                lookupLow[token - minTokenLow] = i;
            else
                lookupHigh[token - minTokenHigh] = i;
        }
    }

    public void SetAllValues(params object[] values)
    {
        if (values is null || values.Length != this.values.Length)
            throw new ArgumentException($"There must be {this.values.Length} values provided.");

        for (int i = 0; i < values.Length; i++)
            this.values[i] = values[i];
    }

    public void SetValue<T>(char c, T value)
    {
        int index = GetIndex(c);
        if (index < 0)
            throw new ArgumentException($"Character '{c}' is not part of the token set.");

        if (!givenTypes[index].IsAssignableFrom(typeof(T)))
            throw new ArgumentException($"Expected {givenTypes[index].Name}, got {typeof(T).Name}.");

        values[index] = value;
    }

    public T GetValue<T>(char c)
    {
        int index = GetIndex(c);
        if (index < 0)
            throw new ArgumentException($"Character '{c}' is not part of the token set.");

        if (!typeof(T).IsAssignableFrom(givenTypes[index]))
            throw new ArgumentException($"Stored type is {givenTypes[index].Name}, not {typeof(T).Name}.");

        return (T)values[index];
    }

    public object[] GetAllValues() => values;

    public IEnumerable<char> Keys => tokensChars;

    public bool ContainsKey(char c) => GetIndex(c) >= 0;

    public bool TryGetValue(char c, out object value)
    {
        int idx = GetIndex(c);
        if (idx >= 0)
        {
            value = values[idx];
            return true;
        }
        value = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(char key)
    {
        if (key < SPLIT)
        {
            int idx = key - minTokenLow;
            return (lookupLow.Length > 0 && (uint)idx < (uint)lookupLow.Length)
                ? lookupLow[idx]
                : -1;
        }
        else
        {
            int idx = key - minTokenHigh;
            return (lookupHigh.Length > 0 && (uint)idx < (uint)lookupHigh.Length)
                ? lookupHigh[idx]
                : -1;
        }
    }
    public object this[char c] => GetValue<object>(c);
}
