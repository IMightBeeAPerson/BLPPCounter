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
using UnityEngine.UI;

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
        private int _Spaces = 2;
        private bool _HasEndColumn = false;
        private bool _CenterText = true;
        private int _MaxWidth = -1; //<= 0 means no max width
        private Color _HighlightColor = new Color(1, 1, 0, 0.5f); //Color used when highlighting text.

        public readonly TextMeshProUGUI Container;
        private string[][] Values; //Values given to be in the table. Can only be set so that no one can just change a value without notifying this object.
        private string[] Names; //The top row, names of each column.
        private float[][] SpacingValues; //The amount of space to make the tables line up for each value. Includes Names, is in pixels.
        private readonly string[][] TableValues; //Each part of the table, properly formatted. Used when combining to make the final string.

        public bool FormatValueUpdated { get; private set; } //Whether or not a value that effects formatting has been updated.
        public bool FormattingUpdated { get; private set; } //Whether or not the formatting has been updated (specifically TableValues).
        public bool ContainerUpdated { get; private set; } //Whether or not the container has been updated in the UpdateTable() method.

        public float TableWidth { get; private set; } //The width of the table in pixels.
        public float TableHeight { get; private set; } //The height of the table in pixels.

        private bool SoftUpdate => FormatValueUpdated && FormattingUpdated; //True only when the only things to have changed have been accounted for (ex: cell text changed) or highlighting has been changed.
        private float[] MaxLengths; //The max length in pixels of a column.
        private string Prefix, Format, Spacer, Suffix; //The prefix, formatting, spacer, and suffix for each row. 
        private readonly HashSet<(int Row, int Column)> HighlightQueue; //This is filled when the highlight method is called and the table hasn't been updated yet. When the table updates, this is used to highlight it while processing.
        private readonly HashSet<(int Row, int Column)> UsedHighlights; //This is filled with highlights currently in the table right now, and they are removed when the highlight is cleared or goes back into the queue.

        private bool HasButtonsBeenAdded => CurrentButtonColumn > -1; //This bool tracks whether or not Value has had the extra column for the buttons added or not.
        internal List<Button> TableButtons { get; private set; }
        private int CurrentButtonColumn; //The column that the buttons are at currently. -1 if none.

        /// <summary>
        /// Initialize a table, given a matrix of values and an array of headers (or names). 
        /// </summary>
        /// <param name="container">
        /// This is how the table displays itself, as it is simply text. Calling <see cref="UpdateTable"/> will automatically display this table through the container,
        /// and calling <see cref="ToString"/> will return the text if you wish to put it into a different container.
        /// </param>
        /// <param name="values">
        /// This is a matrix containing the table values. It must be a square (meaning all arrays inside of it are the same length).
        /// </param>
        /// <param name="names">
        /// This is the headers for the table. It must be the same length as all the arrays inside <paramref name="values"/>.
        /// </param>
        public Table(TextMeshProUGUI container, string[][] values, params string[] names)
        {
            //Set the vars to the vars :D
            Container = container;
            Values = values;
            Names = names;
            HighlightQueue = new HashSet<(int, int)>();
            UsedHighlights = new HashSet<(int, int)>();

            SpacingValues = HelpfulMisc.CreateSquareMatrix<float>(Values.Length + 1, Names.Length); //+1 because this also stores values for the Names array.
            TableValues = HelpfulMisc.CreateSquareMatrix<string>(Values.Length + 1, Names.Length);

            PropertyChanged += (obj, arg) => ContainerUpdated = false; //Whenever a value has been updated, this means the container is no longer up to date.

            //Set all "updated" variables to false
            FormattingUpdated = false;
            FormatValueUpdated = false;
            ContainerUpdated = false;

            //Set TableWidth and TableHeight to 0 (it is set correctly once it has been updated).
            TableWidth = 0;
            TableHeight = 0;

            //Setup the button adder variables
            CurrentButtonColumn = -1;
            TableButtons = new List<Button>();

            //This is a special line to fix an error in the font where people don't set the width for spaces, which screws up highlighting.
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
            ContainerUpdated = true;

            // Resize the RectTransform to match the table’s size
            RectTransform rt = Container.rectTransform;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, TableWidth / Container.canvas.scaleFactor);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, TableHeight / Container.canvas.scaleFactor);
        }
        public void ClearTable()
        {
            Container.text = new string('\n', Values[0].Length);
            ContainerUpdated = false;
        }
        public void ChangeValue(int row, int column, string newValue)
        {
            Values[row][column] = newValue; //Set Value to new value no matter the state of the table.
            ContainerUpdated = false; //Since a value has been changed, the current container display is no longer up to date.
            if (!FormattingUpdated) return; //If formatting hasn't been updated, then nothing more needs to be done. Exit.
            float len = GetLen(newValue); //Get the length of the new Value (in pixels).
            if (len < MaxLengths[column]) //Check if this length is within the maximum length of the column.
            {
                SpacingValues[row + 1][column] = MaxLengths[column] - len; //Set the spacing value to be correct with the new value. (row + 1 because SpacingValues stores the Name array)
                FormatCell(row + 1, column); //Reformat the cell to having the proper values on <space> tags.
            }
        }
        public void HighlightCell(int row, int column, bool updateTable = true)
        {
            if (!FormattingUpdated) //If formatting hasn't been updated, meaning TableValues could be null, do not proceed.
            {
                HighlightQueue.Add((row, column)); //Add highlight to queue to be handled when TableValues is updated.
                return; //Do not go any further.
            }
            HighlightCellInternal(row, column); //Since TableValues isn't null, go ahead and highlight the cell.
            if (updateTable) UpdateTable(false); //Update the table if the user would like the table to be updated.
        }
        private void HighlightCellInternal(int row, int column)
        {
            TableValues[row][column] = $"<mark={HelpfulMisc.ConvertColorToHex(_HighlightColor)}>{TableValues[row][column]}</mark>"; //Surrounds the cell with the highlight tag.
            UsedHighlights.Add((row, column)); //Adds the highlight to UsedHighlights, as it is now in use.
        }
        public void RemoveHighlightFromCell(int row, int column)
        {
            if (HighlightQueue.Contains((row, column))) //If highlight is inside queue, the handle removing the highlight, otherwise continue.
            {
                HighlightQueue.Remove((row, column)); //Remove the highlight from the queue.
                return; //Mission succeeded, exit this method.
            }
            //FIRST PART: If formatting hasn't been updated and a highlight isn't in the queue, then highlight must not exist. Exit this method.
            //SECOND PART: If UsedHighlights doesn't have this highlight, then it must not exist. Exit this method.
            if (!FormattingUpdated || !UsedHighlights.Contains((row, column))) return;
            TableValues[row][column] = Regex.Replace(TableValues[row][column], "</?mark[^>]*>", ""); //Uses regex to find and remove the mark from this cell.
            UsedHighlights.Remove((row, column)); //Removes this highlight from UseHighlights as it is no longer used.
        }
        public void ClearHighlights()
        {
            HighlightQueue.Clear(); //No matter what, remove all queued highlights from queue.
            if (!FormattingUpdated) return; //If formatting isn't updated, meaning TableValues could be null, do not proceed.
            Regex r = new Regex("</?mark[^>]*>"); //Simple regex that finds any <mark> tag.
            foreach ((int, int) highlight in UsedHighlights) //Iterates through all highlights in use.
                TableValues[highlight.Item1][highlight.Item2] = r.Replace(TableValues[highlight.Item1][highlight.Item2], ""); //Removes the highlight from TableValues using the regex replace.
            UsedHighlights.Clear(); //Clears used highlights once they are all back into the queue.
        }
        private void RequeueHighlights()
        {
            Regex r = new Regex("</?mark[^>]*>"); //Simple regex that finds any <mark> tag.
            foreach ((int, int) highlight in UsedHighlights) //Iterates through all highlights in use.
            {
                TableValues[highlight.Item1][highlight.Item2] = r.Replace(TableValues[highlight.Item1][highlight.Item2], ""); //Removes the highlight from TableValues using the regex replace.
                HighlightQueue.Add(highlight); //Adds the highlight back into the queue.
            }
            UsedHighlights.Clear(); //Clears used highlights once they are all back into the queue.
        }

        /// <summary>
        /// Spawns buttons aligned to the given column of the table.
        /// </summary>
        /// <param name="buttonPrefab">Prefab of the button to spawn</param>
        /// <param name="column">Which table column to align with</param>
        /// <param name="onClick">Action callback when a button is clicked, passes row/col ID string</param>
        /// <param name="labels">Optional array of labels for buttons</param>
        public void SpawnButtonsForColumn(int column, Action<string> onClick, Button buttonPrefab, string columnName = null, string[] labels = null)
        {
            //Check for nulls
            if (!FormatValueUpdated || buttonPrefab is null) return;

            //Reset changes if they have been made
            if (HasButtonsBeenAdded)
            {
                Names = Names.RemoveElement(CurrentButtonColumn);
                Values = Values.RemoveElement(CurrentButtonColumn);
                MaxLengths = MaxLengths.RemoveElement(CurrentButtonColumn);
            }

            // Destroy old buttons
            foreach (Button b in TableButtons)
                if (!(b is null)) UnityEngine.Object.Destroy(b.gameObject);
            TableButtons.Clear();

            // Force TMP mesh update
            Container.ForceMeshUpdate();
            TMP_TextInfo textInfo = Container.textInfo;
            RectTransform containerRT = Container.rectTransform;

            // --- Determine column width (widest text in column) ---
            float columnWidth = 0f;
            for (int row = 0; row < textInfo.lineCount - 2; row++)
            {
                string text = labels != null && row < labels.Length ? labels[row] : $"{row}_{column}";
                float textLen = GetLen(text);
                if (textLen > columnWidth) columnWidth = textLen;
            }

            // --- Compute start of current column with spacer adjustments ---
            float aLength = GetLen("a");
            float columnStart = GetLen(Prefix + 'a') - aLength;        // start after prefix
            float spacer = GetLen('a' + Spacer + 'a') - 2 * aLength;   // spacer width
            for (int c = 0; c < column; c++)
                columnStart += MaxLengths[c] + spacer;
            columnStart -= spacer;
            columnStart += column < Names.Length ? spacer : GetLen('a' + Suffix) - aLength;

            // --- Column center X offset ---
            float xOffset = -TableWidth / 2f + columnStart + (column >= Names.Length ? columnWidth : MaxLengths[column]) / 2f;

            // --- Add in the extra column ---
            if (!(columnName is null))
            {
                Names = Names.InsertElement(column, columnName);
                MaxLengths = MaxLengths.InsertElement(column, columnWidth);
                string[] hold = new string[Values[0].Length];
                for (int row = 0; row < hold.Length; row++)
                    hold[row] = "";
                Values = Values.InsertElement(column, hold);
            }

            // --- Loop through rows (skip first 2 lines) ---
            for (int row = 2; row < textInfo.lineCount; row++)
            {
                TMP_LineInfo line = textInfo.lineInfo[row];
                float lineHeight = Mathf.Abs(line.ascender - line.descender);
                float lineMidY = (line.ascender + line.descender) / 2f;

                string text = labels != null && row - 2 < labels.Length ? labels[row - 2] : $"{row - 2}_{column}";

                // Adjust X offset to center the button over the text
                float buttonOffsetX = xOffset + (columnWidth - GetLen(text)) / 2f;

                // Instantiate button
                Button btn = UnityEngine.Object.Instantiate(buttonPrefab, containerRT);
                RectTransform btnRT = btn.GetComponent<RectTransform>();
                btnRT.pivot = btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
                btnRT.sizeDelta = new Vector2(columnWidth, lineHeight);
                btnRT.anchoredPosition = new Vector2(buttonOffsetX, lineMidY);

                string buttonId = $"{row - 2}_{column}";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick?.Invoke(buttonId));

                // Update label and center it
                TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 4;
                    label.fontSizeMax = Container.fontSize;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;

                    RectTransform labelRT = label.GetComponent<RectTransform>();
                    labelRT.pivot = labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0.5f);
                    labelRT.sizeDelta = btnRT.sizeDelta;

                    label.text = (labels != null && row - 2 < labels.Length) ? labels[row - 2] : buttonId;
                }
                TableButtons.Add(btn);
            }
            if (HasButtonsBeenAdded || !(columnName is null))
                ContainerUpdated = false;
            if (!(columnName is null))
                CurrentButtonColumn = column;
            else CurrentButtonColumn = -1;
        }

        private float GetLen(string str) => Container.GetPreferredValues(str).x;
        private float GetLenWithoutRich(string str) => GetLen(Regex.Replace(str, "<[^>]+>", ""));
        private float GetLenWithSpacers(string str)
        {
            MatchCollection mc = Regex.Matches(str, "(?<=<space=)[^p]+");
#if NEW_VERSION
            float addedSpace = mc.Aggregate(0.0f, (total, match) => total + float.Parse(match.Value)); // 1.37.0 and above
#else
            float addedSpace = mc.OfType<Match>().Aggregate(0.0f, (total, match) => total + float.Parse(match.Value)); // 1.34.2 and below
#endif
            return GetLenWithoutRich(str) + addedSpace;
        }
        private string GetRow(int rowIndex) => FormattingUpdated ? 
            Prefix + TableValues[rowIndex].Skip(1).Aggregate(TableValues[rowIndex][0], (total, current) => total + Spacer + current) + Suffix : //If formatting is updated then a row is simply the Prefix + all table values combined (with a spacer between them) + Suffix.
            FormatRow(rowIndex); //If formatting is not updated, then update the format :D.
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
            if (columnIndex != CurrentButtonColumn)
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
                //if (MaxLengths.Length != Names.Length || SpacingValues.Length != Values.Length + 1)
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
            string space = new string(' ', Spaces); //Set space to the amount of Spaces. 
            Prefix = $"|{space}"; //Sets prefix based on Spaces.
            Format = _CenterText ? "<space={1}px>{0}<space={2}px>" : "{0}<space={1}px>"; //Sets Format based on CenterText.
            Spacer = $"{space}|{space}"; //Sets Spacer based on Spaces.
            Suffix = _HasEndColumn ? $"{space}|" : ""; //Sets Suffix based on Spaces and HasEndColumn.
        }

        public override string ToString()
        {
            string space = new string(' ', Spaces); //Converts Spaces from an int into the actual amount of spaces
            string[] rows = new string[Values.Length + 2]; //This is the actual rows the table will show, including the dividing dash line.

            bool softUpdate = SoftUpdate; //Need to store this variable here in case UpdateValues changes its value.
            if (!softUpdate) UpdateValues(); //Handles if any value has been updated. Only call this if softUpdate is false, otherwise Update variables are checked twice for no reason.

            //Sets each row of the table to be the proper values and combines each row into a string. The only row not setup is the dashes.
            rows[0] = GetRow(0) + '\n'; //The +\n is done because of the way Aggregate works, it doesn't add the \n to the first index.
            for (int i = 0; i < Values.Length; i++)
                rows[i + 2] = GetRow(i + 1); //Skips over row[1] because that is dash row.

            if (!softUpdate) //Only recalculate the dash line if some value had to be updated, otherwise skip and just reuse what was already made.
            {
                float dashSize = GetLen("-"); //Get sizes of what one dash is. This will be updated later to be more precise.
                //Length of the line in pixels (without spacers).
                float maxSpace = _MaxWidth > 0 ? _MaxWidth : GetLenWithSpacers(rows[0].Substring(Prefix.Length, rows[0].Length - Prefix.Length - (_HasEndColumn ? Suffix.Length : 0))); 
                int dashCount = -1; //inits the dashCount variable out here so that it is in scope for later use.
                float realDashSize = 0;
                do
                { //Since the size of a dash changes based off how many there are, we will loop until we match with 2 decimal points of precision.
                    if (dashCount > 0) //All this really does is avoids redoing the dashSize on the first iteration.
                    {
                        realDashSize = GetLen(new string('-', dashCount)); //Calculates the actual length of the dash line by directly asking Unity.
                        dashSize = realDashSize / dashCount; //The new size of dashSize, based on the size of the placed dashes.
                    }
                    dashCount = (int)Math.Ceiling(maxSpace / dashSize); //The amount of dashes.
                } while (Math.Round(realDashSize - dashSize * dashCount, 2) != 0); //Checks if the calculated dash size equals the actual one within 2 decimals places.
                float spaceLength = maxSpace - dashSize * dashCount; //The length in pixels that the spacer(s) need to be set to.
                if (_HasEndColumn) spaceLength /= 2; //If there is a suffix, divide by 2 since the spacing will be distributed between the front and back.
                rows[1] = $"{Prefix}<space={spaceLength}px>{new string('-', dashCount)}"; //Sets row[1] to the prefix + space tag + the amount of dashes that was calculated
                if (_HasEndColumn) rows[1] += $"<space={spaceLength}px>{Suffix}"; //Adds the last part, the spacing and the suffix, to row[1].
                FormattingUpdated = true; //Make sure this is true, because if it wasn't then formatting has been updated.
            } else
            {
                string content = Container.text; //Gets the text in the container (this line can only be reached if Container text had been set).
                content = content.Substring(content.IndexOf('\n')); //Removes the header line from the string.
                content = content.Substring(1, content.IndexOf('\n', 2) - 1); //Removes everything after the second line (aka the dash line). Importantly it also removes the \n at the start and end of the line.
                rows[1] = content; //Set the row[1] to be the dash line.
            }
            TableWidth = GetLenWithSpacers(rows[0]); //Sets the width of the table to the width of the top row. Because the shape of the table is a rectangle, this should be the width of all rows.
            TableHeight = Container.GetPreferredValues("|").y * rows.Length; //Sets the table height to the height of the tallest text multiplied by the number of rows.
            return rows.Aggregate((total, str) => total + str + '\n'); //Combine rows into one string and returns that string.
        }
    }
}
