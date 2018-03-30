using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    public class Table : Content
    {
        //public TableRow Header { get; set; }
        public List<TableRow> Rows { get; set; }
        public BorderPattern Border { get; set; }

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
            return new TableRow(this);
        }
    }

    public class TableRow
    {
        public readonly Table Table;
        public List<TableCell> Columns { get; set; }
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
                            result = cell.ImageContent.Height;
                        }
                    }
                    else
                    {
                        if (cell.TextContent.Height > result)
                        {
                            result = cell.TextContent.Height;
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
            return new TableCell(this);
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
