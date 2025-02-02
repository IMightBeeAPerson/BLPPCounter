using BLPPCounter.Helpfuls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BLPPCounter.Utils
{
    internal class Table: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Spaces
        {
            get => _Spaces;
            set
            {
                _Spaces = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Spaces)));
            }
        }
        public bool HasEndColumn
        {
            get => _HasEndColumn;
            set
            {
                _HasEndColumn = value;
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasEndColumn)));
            }
        }
        public bool CenterText
        {
            get => _CenterText;
            set
            {
                _CenterText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CenterText)));
            }
        }
        public int MaxWidth
        {
            get => _MaxWidth;
            set
            {
                _MaxWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxWidth)));
            }
        }
        public Color HighlightColor
        {
            get => _HighlightColor;
            set
            {
                _HighlightColor = value;
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightColor)));
            }
        }
        public int _Spaces = 2;
        public bool _HasEndColumn = false;
        public bool _CenterText = true;
        public int _MaxWidth = -1; //<= 0 means no max width
        public Color _HighlightColor = new Color(1, 1, 0, 0.5f);

        private readonly TextMeshProUGUI Container;
        public string[][] Values 
        { 
            get => _Values; 
            set
            {
                _Values = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
            }
        }
        public string[] Names
        {
            get => _Names;
            set 
            {
                _Names = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Names)));
            }
        }
        private string[][] _Values;
        private string[] _Names;

        private bool ValuesReset;
        public bool TableUpdated { get; private set; }
        private float[] MaxLengths;
        private string Format;

        public Table(TextMeshProUGUI container, string[][] values, params string[] names)
        {
            Container = container;
            Values = values;
            Names = names;

            ValuesReset = true;
            TableUpdated = false;
            PropertyChanged += (a,b) => { if (!ValuesReset) ResetValues(); };

            if (Container.font.characterLookupTable[' '].glyph.metrics.width == 0) Container.font.MakeSpacesHaveSpace();
        }
        public Table(TextMeshProUGUI container, IEnumerable<KeyValuePair<string, string>> values, string key, string value) 
            : this(container, values.Select(kvp => new string[2] { kvp.Key, kvp.Value }).ToArray(), key, value) {}

        public void ResetValues()
        {
            MaxLengths = null;
            Format = null;
            ValuesReset = true;
            TableUpdated = false;
        }
        public void UpdateTable()
        {
            Container.text = ToString();
        }
        public void HighlightRow(int row) //NOT 0 indexed, row 1 IS row 1
        {
            if (!TableUpdated) return;
            string[] rows = Container.text.Split('\n');
            string top = rows.Take(1 + row).Aggregate("", (total, current) => total + current + '\n');
            string selectedRow = rows.Skip(1 + row).Take(1).First();
            selectedRow = selectedRow.Substring(0, selectedRow.IndexOf('|') + 1) + $"<mark=#{HelpfulMisc.ConvertColorToHex(_HighlightColor)}>" + selectedRow.Substring(selectedRow.IndexOf('|') + 1) + "</mark>";
            Container.text = top + selectedRow + '\n' + rows.Skip(2 + row).Aggregate("", (total, current) => total + current + '\n').Trim();
        }
        public Task AwaitHighlightRow(int row, int period = 10, int timeout = 3) //timeout is in seconds
        {
            int msDelay = 1000 / period, count = 0;
            timeout *= period;
            return Task.Run(() =>
            {
                while (!TableUpdated)
                {
                    Thread.Sleep(msDelay);
                    if (count++ >= timeout) break;
                }
                HighlightRow(row);
            });
        }

        private float GetLen(string str) => Container.GetPreferredValues(str).x;
        private float GetLenWithoutRich(string str) => GetLen(Regex.Replace(str, "<[^>]+>", ""));
        private float GetLenWithSpacers(string str)
        {
            MatchCollection mc = Regex.Matches(str, "(?<=<space=)[^p]+");
            //float addedSpace = mc.Aggregate(0.0f, (total, match) => total + float.Parse(match.Value)); // 1.37.0 and above
            float addedSpace = mc.OfType<Match>().Aggregate(0.0f, (total, match) => total + float.Parse(match.Value)); // 1.34.2 and below
            return GetLenWithoutRich(str) + addedSpace;
        }
        private object[] GetFormatVals(string[] row, int centerTextInc)
        {
            int outArrLen = row.Length * 2 - 1;
            if (_CenterText) outArrLen += row.Length;
            if (_HasEndColumn) outArrLen += centerTextInc - 1;
            object[] outArr = new object[outArrLen];
            for (int i = 0, c = 0; i < row.Length; i++, c += centerTextInc)
            {
                outArr[c] = row[i];
                if (i < row.Length - 1 || _HasEndColumn) if (_CenterText) { outArr[c + 1] = (MaxLengths[i] - GetLenWithoutRich(row[i])) / 2; outArr[c + 2] = outArr[c + 1]; }
                    else outArr[c + 1] = MaxLengths[i] - GetLenWithoutRich(row[i]);
            }
            return outArr;
        }

        public override string ToString()
        {
            string space = new string(' ', Spaces);
            string[] rows = new string[_Values.Length + 2];
            int centerTextInc = _CenterText ? 3 : 2; //weird var, but is an attempt to make this less jank
            Dictionary<uint, TMP_Character> lookupTable = new Dictionary<uint, TMP_Character>();
            if (ValuesReset) 
            {
                MaxLengths = new float[Names.Length];
                Format = _CenterText ? $"|{space}<space={{1}}px>{{0}}" : $"|{space}{{0}}";
                for (int i = 1, c = centerTextInc - 1; i < _Names.Length; i++, c += centerTextInc)
                    Format += $"<space={{{c}}}px>{space}|{space}" + (_CenterText ? $"<space={{{c + 2}}}px>{{{c + 1}}}" : $"{{{c + 1}}}");
                if (_HasEndColumn) Format += $"<space={{{centerTextInc * (_Names.Length - 2) + centerTextInc + 1}}}px>{space}|";
                for (int i = 0; i < MaxLengths.Length; i++)
                    MaxLengths[i] = Math.Max(_Values.Aggregate(0.0f, (total, strArr) => Math.Max(total, GetLenWithoutRich(strArr[i]))), GetLenWithoutRich(_Names[i]));
            }
            rows[0] = string.Format(Format, GetFormatVals(_Names, centerTextInc));
            for (int i = 0; i < _Values.Length; i++)
                rows[i + 2] = string.Format(Format, GetFormatVals(_Values[i], centerTextInc));
            float spacerSize = GetLen(space + "|"), dashSize = GetLen("-");
            if (_HasEndColumn) spacerSize *= 2;
            float maxSpace = _MaxWidth > 0 ? _MaxWidth : rows.Skip(2).Aggregate(0.0f, (total, str) => Math.Max(total, GetLenWithSpacers(str)));
            int dashCount = (int)Math.Ceiling((maxSpace - spacerSize) / dashSize);
            rows[1] = "|" + space + new string('-', dashCount);
            if (_HasEndColumn)
            {
                float dashLength = GetLen(rows[1]);
                rows[1] += $"<space={maxSpace - GetLen(rows[1]) - spacerSize / 2}px>{space}|";
            }
            rows[0] += '\n';
            ValuesReset = false;
            TableUpdated = true;
            return rows.Aggregate((total, str) => total + str + "\n");
        }
    }
}
