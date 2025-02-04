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
#pragma warning disable IDE0051
        public event PropertyChangedEventHandler PropertyChanged;

        public int Spaces
        {
            get => _Spaces;
            set
            {
                _Spaces = value;
                FormatValueUpdated = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Spaces)));
            }
        }
        public bool HasEndColumn
        {
            get => _HasEndColumn;
            set
            {
                _HasEndColumn = value;
                FormatValueUpdated = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasEndColumn)));
            }
        }
        public bool CenterText
        {
            get => _CenterText;
            set
            {
                _CenterText = value;
                FormatValueUpdated = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CenterText)));
            }
        }
        public int MaxWidth
        {
            get => _MaxWidth;
            set
            {
                _MaxWidth = value;
                FormattingUpdated = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxWidth)));
            }
        }
        public Color HighlightColor
        {
            get => _HighlightColor;
            set
            {
                _HighlightColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightColor)));
            }
        }
        public int _Spaces = 2;
        public bool _HasEndColumn = false;
        public bool _CenterText = true;
        public int _MaxWidth = -1; //<= 0 means no max width
        public Color _HighlightColor = new Color(1, 1, 0, 0.5f);

        private readonly TextMeshProUGUI Container;
        private string[][] Values; //Values given to be in the table. Can only be set so that no one can just change a value without notifying this object.
        private string[] Names; //The top row, names of each column.
        private float[][] SpacingValues; //The amount of space to make the tables line up for each value. Includes Names, is in pixels.
        private readonly string[][] TableValues; //Each part of the table, properly formatted. Used when combining to make the final string.

        public bool FormatValueUpdated { get; private set; } //Whether or not a value that effects formatting has been updated.
        public bool FormattingUpdated { get; private set; } //whether or not the formatting has been updated (specifically TableValues).
        private float[] MaxLengths; //The max length in pixels of a column
        private string Prefix, Format, Spacer, Suffix; //The prefix, formatting, spacer, and suffix for each row. 
        private readonly HashSet<(int Row, int Column)> HighlightQueue; //This is filled when the highlight method is called and the table hasn't been updated yet. When the table updates, this is used to highlight it while processing.
        private int HighlightCount; //This is how many cells are highlighted on the board.

        public Table(TextMeshProUGUI container, string[][] values, params string[] names)
        {
            Container = container;
            Values = values;
            Names = names;
            HighlightQueue = new HashSet<(int, int)>();
            HighlightCount = 0;

            SpacingValues = new float[Values.Length + 1][]; //+1 for Names lengths
            TableValues = new string[Values.Length + 1][];
            for (int i = 0; i < SpacingValues.Length; i++)
            {
                SpacingValues[i] = new float[Names.Length];
                TableValues[i] = new string[Names.Length];
            }

            FormattingUpdated = false;
            FormatValueUpdated = false;
            if (Container.font.characterLookupTable[' '].glyph.metrics.width == 0) Container.font.MakeSpacesHaveSpace();
        }
        public Table(TextMeshProUGUI container, IEnumerable<KeyValuePair<string, string>> values, string key, string value) 
            : this(container, values.Select(kvp => new string[2] { kvp.Key, kvp.Value }).ToArray(), key, value) {}
       
        public void SetValues(string[][] values)
        {
            Values = values;
            FormattingUpdated = false;
        }
        public void SetNames(string[] names)
        {
            Names = names;
            FormattingUpdated = false;
        }
        public void UpdateTable(bool removeHighlights = false)
        {
            if (!removeHighlights && FormattingUpdated)
                RequeueHighlights();
            Container.text = ToString();
        }
        public void ChangeValue(int row, int column, string newValue)
        {
            Values[row][column] = newValue;
            if (!FormattingUpdated) return;
            float len = GetLen(newValue);
            if (len < MaxLengths[column])
            {
                SpacingValues[row + 1][column] = MaxLengths[column] - len;
                FormatCell(row + 1, column);
            }
        }
        public void HighlightCell(int row, int column)
        {
            if (!FormattingUpdated) 
            {
                HighlightQueue.Add((row, column));
                return; 
            }
        }
        private void HighlightCellInternal(int row, int column)
        {
            TableValues[row][column] = $"<mark={HelpfulMisc.ConvertColorToHex(_HighlightColor)}>{TableValues[row][column]}</mark>";
            HighlightCount++;
        }
        public void ClearHighlights()
        {
            if (!FormattingUpdated)
            {
                HighlightCount = 0;
                HighlightQueue.Clear();
                return;
            }
            Regex r = new Regex("</?mark[^>]*>");
            for (int row = 0; row < TableValues.Length || HighlightCount > 0; row++)
                for (int column = 0; column < TableValues[row].Length || HighlightCount > 0; column++)
                    if (r.IsMatch(TableValues[row][column]))
                    {
                        TableValues[row][column] = r.Replace(TableValues[row][column], "");
                        HighlightCount--;
                    }
        }
        private void RequeueHighlights()
        {
            Regex r = new Regex("</?mark[^>]*>");
            for (int row = 0; row < TableValues.Length || HighlightCount > 0; row++)
                for (int column = 0; column < TableValues[row].Length || HighlightCount > 0; column++)
                    if (r.IsMatch(TableValues[row][column]))
                    {
                        HighlightCount--;
                        HighlightQueue.Add((row, column));
                        TableValues[row][column] = r.Replace(TableValues[row][column], "");
                    }
            HighlightCount = HighlightQueue.Count;
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
        private string GetRow(int rowIndex) => 
            FormattingUpdated ? Prefix + TableValues[rowIndex].Aggregate((total, current) => total + current + Spacer) + Suffix : FormatRow(rowIndex);
        private string FormatRow(int rowIndex)
        {
            string[] vals = rowIndex == 0 ? Names : Values[rowIndex - 1];
            string outp = Prefix;
            for (int i = 0; i < vals.Length; i++)
                outp += FormatCell(rowIndex, i) + Spacer;
            outp = outp.Substring(0, outp.Length - Spacer.Length);
            if (_CenterText) outp += string.Format(Suffix, SpacingValues[rowIndex].Last());
            return outp;
        }
        private string FormatCell(int row, int column)
        {
            string[] vals = row == 0 ? Names : Values[row - 1];
            TableValues[row][column] = _CenterText ?
                    string.Format(Format, vals[column], SpacingValues[row][column] / 2, SpacingValues[row][column] / 2) :
                    string.Format(Format, vals[column], SpacingValues[row][column]);
            if (HighlightQueue.Contains((row, column)))
            {
                HighlightCellInternal(row, column);
                HighlightQueue.Remove((row, column));
            }
            return TableValues[row][column];
        }
        private void CalculateRowSpacing(int columnIndex)
        {
            float[] columnLengths = Values.Select(arr => GetLenWithoutRich(arr[columnIndex])).Prepend(GetLenWithoutRich(Names[columnIndex])).ToArray();
            MaxLengths[columnIndex] = columnLengths.Aggregate((total, current) => Math.Max(total, current));
            for (int j = 0; j < columnLengths.Length; j++) 
                SpacingValues[j][columnIndex] = MaxLengths[columnIndex] - columnLengths[j];
        }
        private void UpdateValues()
        {
            if (MaxLengths is null)
                UpdateMaxLengths(); //This is to make sure there are no null pointer exceptions.

            if (!FormattingUpdated)
            {
                //If Values or Names has changed length, update to correct this.
                if (MaxLengths.Length != Names.Length || SpacingValues.Length != Values.Length + 1)
                    UpdateMaxLengths();
                //FormattingUpdated is kept as false so that GetRow knows to fix formatting.
            }

            if (!FormatValueUpdated)
            {
                //If a value that effects formatting has been changed, update format to match.
                UpdateFormatting();
                //Set FormatValueUpdated back to true.
                FormatValueUpdated = true;
            }
        }
        private void UpdateMaxLengths()
        {
            if (MaxLengths is null || MaxLengths.Length != Names.Length || Values.Length != SpacingValues.Length - 1)
            {
                MaxLengths = new float[Names.Length];
                SpacingValues = HelpfulMisc.CreateSquareMatrix<float>(Values.Length + 1, Names.Length);
            }
            for (int i = 0; i < MaxLengths.Length; i++)
                CalculateRowSpacing(i);
        }
        private void UpdateFormatting()
        {
            string space = new string(' ', Spaces);
            Prefix = $"|{space}";
            Format = _CenterText ? "<space={1}px>{0}<space={2}px>" : "<space={1}px>{0}";
            Spacer = $"{space}|{space}";
            Suffix = _HasEndColumn ? $"{space}|" : "";
        }

        public override string ToString()
        {
            string space = new string(' ', Spaces); //Converts Spaces from an int into the actual amount of spaces
            string[] rows = new string[Values.Length + 2]; //This is the actual rows the table will show, including the dividing dash line.

            UpdateValues(); //Handles if any value has been updated

            //Sets each row of the table to be the proper values and combines each row into a string. The only row not setup is the dashes
            rows[0] = GetRow(0) + '\n'; //The +\n is done because of the way Aggregate works, it doesn't add the \n to the first index.
            for (int i = 0; i < Values.Length; i++)
                rows[i + 2] = GetRow(i + 1); //Skips over row[1] because that is dash row.

            float spacerSize = GetLen(Prefix), dashSize = GetLen("-"); //Get sizes of the 2 "objects" that make up row[1], the Prefix (aka spacer) and dash.
            if (_HasEndColumn) spacerSize *= 2; //since Prefix and Suffix are made up of the same characters, their lengths are the same.
            float maxSpace = _MaxWidth > 0 ? _MaxWidth : GetLenWithSpacers(rows[0]); //Length of the line in pixels.
            int dashCount = (int)Math.Ceiling((maxSpace - spacerSize) / dashSize); //The amount of dashes.
            rows[1] = Prefix+"<space={0}px>" + new string('-', dashCount); //Sets row[1] to the prefix + space tag + the amount of dashes that was calculated
            float spaceLength = maxSpace - dashSize * dashCount - spacerSize * 2; //The length in pixels that the spacer(s) need to be set to
            if (_HasEndColumn) //Add last part of there is a Suffix
            {
                rows[1] += "<space={1}px>"+Suffix; //Adds the last part, the spacing and the suffix, to row[1].
                rows[1] = string.Format(rows[1], spaceLength / 2, spaceLength / 2); //Format row[1] with the correct spacing.
                //rows[1] = string.Format(rows[1], 0, 0);
            } else
                rows[1] = string.Format(rows[1], spaceLength); //Format row[1] with the correct spacing.

            FormattingUpdated = true; //Make sure this is true, because if it wasn't then formatting has been updated.
            string outp = rows.Aggregate((total, str) => total + str + '\n'); //Combine rows into one string.
            return outp; //Return that string
        }
    }
}
