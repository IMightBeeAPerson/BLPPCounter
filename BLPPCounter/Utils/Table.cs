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
        public string[][] Values { private get; set; } //Values given to be in the table.
        private string[][] ValueMemory; // this is a memory address, just my way to check if the "Values" variable has been changed.
        private string[] Names; //The top row, names of each column.
        private float[][] SpacingValues; //The amount of space to make the tables line up for each value. Includes Names, is in pixels.
        private string[][] TableValues; //Each part of the table, properly formatted. Used when combining to make the final string.
        private string Prefix, Suffix; //The prefix and suffix for each row.

        private bool ValuesReset;
        public bool TableUpdated { get; private set; }
        private float[] MaxLengths;
        private string Format;

        public Table(TextMeshProUGUI container, string[][] values, params string[] names)
        {
            Container = container;
            Values = values;
            ValueMemory = values;
            Names = names;

            SpacingValues = new float[Values.Length + 1][]; //+1 for names lengths
            TableValues = new string[Values.Length + 1][];
            for (int i = 0; i < SpacingValues.Length; i++)
            {
                SpacingValues[i] = new float[Values[0].Length];
                TableValues[i] = new string[Values[0].Length];
            }
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
            Prefix = null;
            Suffix = null;
            ValuesReset = true;
            TableUpdated = false;
        }
        public void UpdateTable()
        {
            Container.text = ToString();
        }
        public void ChangeValue(int row, int column, string newValue)
        {
            Values[row][column] = newValue;
            if (!TableUpdated || ValuesReset) return;
            float len = GetLen(newValue);
            if (len < MaxLengths[column])
            {
                SpacingValues[row + 1][column] = MaxLengths[column] - len;
                FormatCell(row + 1, column);
            }
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
        private string FormatRow(int rowIndex)
        {
            string[] vals = rowIndex == 0 ? Names : Values[rowIndex - 1];
            string outp = Prefix;
            for (int i = 0; i < vals.Length; i++)
                outp += FormatCell(rowIndex, i);
            outp = outp.Substring(0, outp.Length - (_Spaces * 2 + 1));
            if (_CenterText) outp += string.Format(Suffix, SpacingValues[rowIndex].Last());
            return outp;
        }
        private string FormatCell(int row, int column)
        {
            string[] vals = row == 0 ? Names : Values[row - 1];
            TableValues[row][column] = _CenterText ?
                    string.Format(Format, vals[column], SpacingValues[row][column] / 2, SpacingValues[row][column] / 2) :
                    string.Format(Format, vals[column], SpacingValues[row][column]);
            return TableValues[row][column];
        }
        private void CalculateRowSpacing(int columnIndex)
        {
            float[] columnLengths = Values.Select(arr => GetLenWithoutRich(arr[columnIndex])).Prepend(GetLenWithoutRich(Names[columnIndex])).ToArray();
            MaxLengths[columnIndex] = columnLengths.Aggregate((total, current) => Math.Max(total, current));
            for (int j = 0; j < columnLengths.Length; j++) 
                SpacingValues[j][columnIndex] = MaxLengths[columnIndex] - columnLengths[j];
        }

        public override string ToString()
        {
            string space = new string(' ', Spaces);
            string[] rows = new string[Values.Length + 2];
            int centerTextInc = _CenterText ? 3 : 2; //weird var, but is an attempt to make this less jank
            if (ValuesReset || ReferenceEquals(Values, ValueMemory)) 
            {
                ValueMemory = Values; //my way of checking if values was changed externally.
                MaxLengths = new float[Names.Length];
                for (int i = 0; i < MaxLengths.Length; i++) 
                    CalculateRowSpacing(i);
                Prefix = $"|{space}";
                Format = (_CenterText ? "<space={1}px>{0}<space={2}px>" : "<space={1}px>{0}") + $"{space}|{space}";
                Suffix = _CenterText ? $"{space}|" : "";
            }
            
            rows[0] = FormatRow(0);
            for (int i = 0; i < Values.Length; i++)
                rows[i + 2] = FormatRow(i + 1);

            float spacerSize = GetLen(space + "|"), dashSize = GetLen("-");
            if (_HasEndColumn) spacerSize *= 2; //to account for the end column
            float maxSpace = _MaxWidth > 0 ? _MaxWidth : GetLenWithSpacers(rows[0]);
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
