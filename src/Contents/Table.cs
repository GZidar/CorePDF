using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    public class Table : Content
    {
        //public TableRow Header { get; set; }
        public List<TableRow> Rows { get; set; } = new List<TableRow>();
        public BorderPattern Border { get; set; }
        public int CellPadding { get; set; } = 2;

        /// <summary>
        /// (Readonly) The sum of all the row heights
        /// </summary>
        public new int Height
        {
            get
            {
                var result = 0;
                foreach (var row in Rows)
                {
                    result += row.Height;
                }

                //if (Header != null)
                //{
                //    result += -Header.Height;
                //}

                return result;
            }
        }

        //public TableRow AddHeader()
        //{
        //    return new TableRow(this);
        //}

        public TableRow AddRow()
        {
            var row = new TableRow(this);
            Rows.Add(row);

            return row;
        }

        public override void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            if (Rows == null || Rows.Count == 0) return;

            var result = "";

            var posX = PosX;
            var posY = PosY;

            // do the content
            var revRows = new List<TableRow>();
            revRows.AddRange(Rows);
            revRows.Reverse();

            foreach (var row in revRows)
            {
                var rowHeight = 0;

                foreach (var cell in row.Columns)
                {
                    var cellWidth = (int)(Width * cell.Width / 100M);
                    if (cell.TextContent != null)
                    {
                        cell.TextContent.Width = cellWidth - (2 * CellPadding);

                        switch (cell.TextContent.TextAlignment)
                        {
                            case Alignment.Center:
                                cell.TextContent.PosX = posX + (cellWidth / 2);
                                break;
                            case Alignment.Right:
                                cell.TextContent.PosX = posX + cellWidth - CellPadding;
                                break;
                            default:
                                cell.TextContent.PosX = posX + CellPadding;
                                break;
                        }
                        cell.TextContent.PosY = posY + CellPadding;

                        cell.TextContent.PrepareStream(pageRoot, pageSize, fonts, false);
                        result += cell.TextContent.GetEncodedString() + "\n";

                        posX += cellWidth;
                        if (rowHeight < (cell.TextContent.Height + (2 * CellPadding)))
                        {
                            rowHeight = (cell.TextContent.Height + (2 * CellPadding));
                        }
                    }
                }

                // calculate the new row start co-ordinates 
                posY += rowHeight;
                posX = PosX;
            }

            // do the borders
            if (Border != null)
            {
                posX = PosX;
                posY = PosY;

                foreach (var row in revRows)
                {
                    foreach (var cell in row.Columns)
                    {
                        var cellWidth = (int)(Width * cell.Width / 100M);
                        var cellHeight = row.Height;

                        var shape = new Shape {
                            Type = Polygon.Line
                        };

                        if (Border.Bottom != null)
                        {
                            shape.Width = cellWidth;
                            shape.Height = 0;
                            shape.PosX = posX;
                            shape.PosY = posY;

                            shape.PrepareStream(pageRoot, pageSize, fonts, false);
                            result += shape.GetEncodedString() + "\n";
                        }

                        //if (Border.Top != null)
                        //{
                        //    shape.Width = cellWidth;
                        //    shape.Height = 0;
                        //    shape.PosX = posX;
                        //    shape.PosY = posY + cellHeight;

                        //    shape.PrepareStream(pageRoot, pageSize, fonts, false);
                        //    result += shape.GetEncodedString() + "\n";
                        //}

                        //if (Border.Left != null)
                        //{
                        //    shape.Width = 0;
                        //    shape.Height = cellHeight;
                        //    shape.PosX = posX;
                        //    shape.PosY = posY;

                        //    shape.PrepareStream(pageRoot, pageSize, fonts, false);
                        //    result += shape.GetEncodedString() + "\n";
                        //}

                        //if (Border.Right != null)
                        //{
                        //    shape.Width = 0;
                        //    shape.Height = cellHeight;
                        //    shape.PosX = posX + cellWidth;
                        //    shape.PosY = posY;

                        //    shape.PrepareStream(pageRoot, pageSize, fonts, false);
                        //    result += shape.GetEncodedString() + "\n";
                        //}

                        posX += cellWidth;
                    }

                    // calculate the new row start co-ordinates 
                    posY += row.Height;
                    posX = PosX;

                    break;
                }
            }

            _encodedData = Encoding.UTF8.GetBytes(result);

            base.PrepareStream(compress);
        }
    }

    public class TableRow
    {
        public readonly Table Table;
        public List<TableCell> Columns { get; set; } = new List<TableCell>();
        public BorderPattern Border { get; set; }

        /// <summary>
        /// (Readonly) The maximim height of all the cells on this row
        /// </summary>
        public int Height
        {
            get
            {
                var result = 0;
                foreach (var cell in Columns)
                {
                    if (cell.ImageContent != null)
                    {
                        if (cell.ImageContent.Height > result)
                        {
                            result = cell.ImageContent.Height + (2 * Table.CellPadding);
                        }
                    }
                    else
                    {
                        if (cell.TextContent.Height > result)
                        {
                            result = cell.TextContent.Height + (2 * Table.CellPadding);
                        }
                    }
                }

                return result;
            } 
        }

        public TableRow(Table table)
        {
            Table = table;
        }

        public TableCell AddColumn()
        {
            var cell = new TableCell(this);
            Columns.Add(cell);

            return cell;
        }
    }

    public class TableCell
    {
        public readonly TableRow Row;
        /// <summary>
        /// A percentage of the total table width 
        /// </summary>
        public decimal Width { get; set; }
        public TextBox TextContent { get; set; }
        public Image ImageContent { get; set; }
        public BorderPattern Border { get; set; }

        public TableCell(TableRow row)
        {
            Row = row;
        }
    }

    public class BorderPattern
    {
        public Stroke Left { get; set; }
        public Stroke Right { get; set; }
        public Stroke Top { get; set; }
        public Stroke Bottom { get; set; }
    }
}
