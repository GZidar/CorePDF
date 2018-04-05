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

        /// <summary>
        /// Specifies the border widths and stroke patterns to use for the 
        /// borders of each cell within the table.
        /// </summary>
        public BorderPattern Border { get; set; }

        /// <summary>
        /// Specifies the size of the gap (in points) between the cell border
        /// and the content.
        /// </summary>
        public CellPadding Padding { get; set; }= new CellPadding(2);

        /// <summary>
        /// Controls how the PosX and PosY coordinates relate to the placement of
        /// the table on the page. When set to "Bottom" (the default) the PosX and PosY
        /// values indicate the Lower Left Corner of the table. When set to "Top" the
        /// coordinates indicate the Top Left Corner of the table.
        /// </summary>
        public PositionAnchor Position { get; set; } = PositionAnchor.Bottom;

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
            var allRows = new List<TableRow>();
            allRows.AddRange(Rows);

            if (Position == PositionAnchor.Bottom)
            {
                // process the rows from the bottom up
                allRows.Reverse();
            }

            foreach (var row in allRows)
            {
                var rowHeight = 0;

                foreach (var cell in row.Columns)
                {
                    var cellWidth = (int)(Width * cell.Width / 100M);
                    if (cell.TextContent != null)
                    {
                        cell.TextContent.Position = Position;
                        cell.TextContent.Width = cellWidth - (Padding.Left + Padding.Right);

                        switch (cell.TextContent.TextAlignment)
                        {
                            case Alignment.Center:
                                cell.TextContent.PosX = posX + (cellWidth / 2);
                                break;
                            case Alignment.Right:
                                cell.TextContent.PosX = posX + cellWidth - Padding.Right;
                                break;
                            default:
                                cell.TextContent.PosX = posX + Padding.Left;
                                break;
                        }

                        if (Position == PositionAnchor.Bottom)
                        {
                            cell.TextContent.PosY = posY + Padding.Bottom;
                        }
                        else
                        {
                            cell.TextContent.PosY = posY - (cell.TextContent.LineHeight + Padding.Top);
                        }

                        cell.TextContent.PrepareStream(pageRoot, pageSize, fonts, false);
                        result += cell.TextContent.GetEncodedString() + "\n";

                        posX += cellWidth;
                        if (rowHeight < (cell.TextContent.Height + (Padding.Top + Padding.Bottom)))
                        {
                            rowHeight = (cell.TextContent.Height + (Padding.Top + Padding.Bottom));
                        }
                    }
                }

                // calculate the new row start co-ordinates 
                if (Position == PositionAnchor.Bottom)
                {
                    posY += rowHeight;
                }
                else
                {
                    posY -= rowHeight;
                }
                posX = PosX;
            }

            // do the borders
            if (Border != null)
            {
                posX = PosX;
                posY = PosY;

                foreach (var row in allRows)
                {
                    var remWidthPct = 100M;
                    var remWidthPoint = Width;

                    foreach (var cell in row.Columns)
                    {
                        var cellWidth = (int)(remWidthPoint * cell.Width / remWidthPct);
                        var cellHeight = row.Height;

                        var shape = new Shape {
                            Type = Polygon.Line
                        };

                        if (Border.Bottom != null)
                        {
                            shape.Width = cellWidth;
                            shape.Height = 0;
                            shape.PosX = posX;

                            if (Position == PositionAnchor.Bottom)
                            {
                                shape.PosY = posY;
                            }
                            else
                            {
                                shape.PosY = posY - cellHeight;
                            }

                            shape.PrepareStream(pageRoot, pageSize, fonts, false);
                            result += shape.GetEncodedString() + "\n";
                        }

                        if (Border.Top != null)
                        {
                            shape.Width = cellWidth;
                            shape.Height = 0;
                            shape.PosX = posX;

                            if (Position == PositionAnchor.Bottom)
                            {
                                shape.PosY = posY + cellHeight;
                            }
                            else
                            {
                                shape.PosY = posY;
                            }

                            shape.PrepareStream(pageRoot, pageSize, fonts, false);
                            result += shape.GetEncodedString() + "\n";
                        }

                        if (Border.Left != null)
                        {
                            shape.Width = 0;
                            shape.Height = cellHeight;
                            shape.PosX = posX;

                            if (Position == PositionAnchor.Bottom)
                            {
                                shape.PosY = posY;
                            }
                            else
                            {
                                shape.PosY = posY - cellHeight;
                            }

                            shape.PrepareStream(pageRoot, pageSize, fonts, false);
                            result += shape.GetEncodedString() + "\n";
                        }

                        if (Border.Right != null)
                        {
                            shape.Width = 0;
                            shape.Height = cellHeight;
                            shape.PosX = posX + cellWidth;

                            if (Position == PositionAnchor.Bottom)
                            {
                                shape.PosY = posY;
                            }
                            else
                            {
                                shape.PosY = posY - cellHeight;
                            }

                            shape.PrepareStream(pageRoot, pageSize, fonts, false);
                            result += shape.GetEncodedString() + "\n";
                        }

                        posX += cellWidth;
                        remWidthPct -= cell.Width;
                        remWidthPoint -= cellWidth;
                    }

                    // calculate the new row start co-ordinates 
                    if (Position == PositionAnchor.Bottom)
                    {
                        posY += row.Height;
                    }
                    else
                    {
                        posY -= row.Height;
                    }
                    posX = PosX;
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
                            result = cell.ImageContent.Height + (Table.Padding.Top + Table.Padding.Bottom);
                        }
                    }
                    else
                    {
                        if (cell.TextContent.Height > result)
                        {
                            result = cell.TextContent.Height + (Table.Padding.Top + Table.Padding.Bottom);
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

    public class CellPadding
    {
        public CellPadding(int value)
        {
            Left = value;
            Right = value;
            Top = value;
            Bottom = value;
        }

        public int Left { get; set; }
        public int Right { get; set; }
        public int Top { get; set; }
        public int Bottom { get; set; }
    }
}
