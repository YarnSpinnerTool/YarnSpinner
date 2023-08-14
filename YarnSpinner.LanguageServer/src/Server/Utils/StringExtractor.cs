namespace YarnLanguageServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ClosedXML.Excel;

    public static class StringExtractor
    {
        public static byte[] ExportStrings(string[][] lineBlocks, IDictionary<string, Yarn.Compiler.StringInfo> stringTable, string[] columns, string format = "csv", string defaultName = "NO CHAR", bool includeCharacters = true)
        {
            // bail out if we have no line
            if (lineBlocks.Length == 0)
            {
                return Array.Empty<byte>();
            }

            // bail out if we don't have at least an id and text
            if (columns.Length > 0)
            {
                if (Array.IndexOf(columns, "text") == -1)
                {
                    return Array.Empty<byte>();
                }

                if (Array.IndexOf(columns, "id") == -1)
                {
                    return Array.Empty<byte>();
                }
            }
            else
            {
                return Array.Empty<byte>();
            }

            ISpreadsheetWriter writer;
            if (format.Equals("csv"))
            {
                writer = new CSVStringWriter(columns);
            }
            else
            {
                writer = new ExcelStringWriter(columns);
            }

            HashSet<string> characters = new HashSet<string>();

            foreach (var block in lineBlocks)
            {
                foreach (var lineID in block)
                {
                    var line = stringTable[lineID];

                    string character = defaultName;
                    string text = line.text;
                    if (includeCharacters)
                    {
                        var index = line.text.IndexOf(':');
                        if (index > 0)
                        {
                            character = line.text.Substring(0, index);
                            text = line.text.Substring(index + 1).TrimStart();
                        }

                        characters.Add(character);
                    }

                    foreach (var column in columns)
                    {
                        switch (column)
                        {
                            case "id":
                                writer.WriteColumn(lineID);
                                break;
                            case "text":
                                writer.WriteColumn(text);
                                break;
                            case "character":
                                writer.WriteColumn(character);
                                break;
                            case "line":
                                writer.WriteColumn($"{line.lineNumber}");
                                break;
                            case "file":
                                writer.WriteColumn(line.fileName);
                                break;
                            case "node":
                                writer.WriteColumn(line.nodeName);
                                break;
                            default:
                                writer.WriteColumn(string.Empty);
                                break;
                        }
                    }

                    writer.EndRow();
                }

                writer.EndBlock();
            }

            writer.Format(characters);
            return writer.ReturnFile();
        }
    }

    /// <summary>
    /// Contains methods for writing dialogue data into a spreadsheet.
    /// </summary>
    public interface ISpreadsheetWriter
    {
        /// <summary>
        /// Writes a value into the next column on the current row.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteColumn(string value);

        /// <summary>
        /// Ends the current row and moves to the next one.
        /// </summary>
        public void EndRow();

        /// <summary>
        /// Marks the previous row as the end of a block of rows.
        /// </summary>
        public void EndBlock();

        /// <summary>
        /// Performs formatting on the overall spreadsheet, given the collection
        /// of known character names that are present in the dialogue.
        /// </summary>
        /// <param name="characters">The collection of character names.</param>
        public void Format(HashSet<string> characters);

        /// <summary>
        /// Converts the spreadsheet to an array of bytes.
        /// </summary>
        /// <returns>The spreadsheet, as a byte array.</returns>
        public byte[] ReturnFile();
    }

    public class CSVStringWriter : ISpreadsheetWriter
    {
        private string[] columns;

        private MemoryStream memory;
        private StreamWriter stream;
        private CsvHelper.CsvWriter csv;
        public CSVStringWriter(string[] columns)
        {
            this.memory = new MemoryStream();
            this.stream = new StreamWriter(this.memory);
            var configuration = new CsvHelper.Configuration.Configuration(System.Globalization.CultureInfo.InvariantCulture);
            this.csv = new CsvHelper.CsvWriter(stream);
            this.columns = columns;

            foreach (var column in columns)
            {
                this.csv.WriteField(column);
            }

            this.csv.NextRecord();
        }

        public void WriteColumn(string value)
        {
            this.csv.WriteField(value);
        }

        public void EndRow()
        {
            this.csv.NextRecord();
        }

        public void EndBlock()
        {
            for (int i = 0; i < columns.Length; i++)
            {
                this.csv.WriteField(string.Empty);
            }

            this.csv.NextRecord();
        }

        public void Format(HashSet<string> characters) { 
            /* does nothing in CSV */
        }

        public byte[] ReturnFile()
        {
            this.csv.Flush();
            var bytes = this.memory.ToArray();

            this.stream.Close();
            this.memory.Close();

            return bytes;
        }
    }

    public class ExcelStringWriter : ISpreadsheetWriter
    {
        private int rowIndex = 1;
        private int columnIndex = 1;
        private IXLWorksheet sheet;
        private XLWorkbook wb;
        private string[] columns;

        public ExcelStringWriter(string[] columns)
        {
            this.columns = columns;

            wb = new XLWorkbook();
            sheet = wb.AddWorksheet("Amazing Dialogue!");

            // Create the header
            for (int j = 0; j < columns.Length; j++)
            {
                sheet.Cell(rowIndex, j + 1).Value = columns[j];
            }

            sheet.Row(rowIndex).Style.Font.Bold = true;
            sheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.DarkGray;
            sheet.Row(rowIndex).Style.Font.FontColor = XLColor.White;
            sheet.SheetView.FreezeRows(1);

            // The first column has a border on the right hand side
            sheet.Column("A").Style.Border.SetRightBorder(XLBorderStyleValues.Thick);
            sheet.Column("A").Style.Border.SetRightBorderColor(XLColor.Black);

            // The second column is indent slightly so that it's
            // not hard up against the border
            sheet.Column("B").Style.Alignment.Indent = 5;

            // The columns always contain text (don't try to infer it to
            // be any other type, like numbers or currency)
            foreach (var col in sheet.Columns())
            {
                col.DataType = XLDataType.Text;
                col.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            rowIndex += 1;
        }

        public void WriteColumn(string value)
        {
            sheet.Cell(rowIndex, columnIndex).Value = value;
            columnIndex += 1;
        }

        public void EndRow()
        {
            rowIndex += 1;
            columnIndex = 1;
        }

        public void EndBlock()
        {
            // Add the dividing line between this block and the next
            sheet.Row(rowIndex - 1).Style.Border.SetBottomBorder(XLBorderStyleValues.Thick);
            sheet.Row(rowIndex - 1).Style.Border.SetBottomBorderColor(XLColor.Black);

            // The next row is twice as high, to create some visual
            // space between the block we're ending and the next
            // one.
            sheet.Row(rowIndex).Height = sheet.RowHeight * 2;
        }

        public void Format(HashSet<string> characters)
        {
            // Wrap the column containing lines, and set it to a
            // sensible initial width
            for (int j = 0; j < columns.Length; j++)
            {
                if (columns[j].Equals("text"))
                {
                    sheet.Column(j + 1).Style.Alignment.WrapText = true;
                    sheet.Column(j + 1).Width = 80;
                    break;
                }
            }

            // colouring every character
            // we do this by moving around the hue wheel and a 20-40% saturation
            // this creates a mostly low collision colour for labelling characters
            int colourIncrementor = 0;
            Random random = new Random();
            double range = (0.4 - 0.2) + 0.2; // putting this out here so I can tweak it as needed: (max - min) + min
            foreach (var character in characters)
            {
                sheet.RangeUsed().AddConditionalFormat().WhenIsTrue($"=$A1=\"{character}\"").Fill.SetBackgroundColor(ColorFromHSV(360.0 / characters.Count * colourIncrementor, random.NextDouble() * range, 1));
                colourIncrementor += 1;
            }

            XLColor ColorFromHSV(double hue, double saturation, double value)
            {
                int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
                double f = (hue / 60) - Math.Floor(hue / 60);

                value = value * 255;
                int v = Convert.ToInt32(value);
                int p = Convert.ToInt32(value * (1 - saturation));
                int q = Convert.ToInt32(value * (1 - f * saturation));
                int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

                switch (hi)
                {
                    case 0:
                        return XLColor.FromArgb(255, v, t, p);
                    case 1:
                        return XLColor.FromArgb(255, q, v, p);
                    case 2:
                        return XLColor.FromArgb(255, p, v, t);
                    case 3:
                        return XLColor.FromArgb(255, p, q, v);
                    case 4:
                        return XLColor.FromArgb(255, t, p, v);
                    default:
                        return XLColor.FromArgb(255, v, p, q);
                }
            }
        }

        public byte[] ReturnFile()
        {
            byte[] bytes = { };
            using (var ms = new MemoryStream())
            {
                wb.SaveAs(ms);
                bytes = ms.ToArray();
            }

            return bytes;
        }
    }
}
