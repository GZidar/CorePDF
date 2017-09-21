using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Embeds
{
    public class TokenisedSVG
    {
        public List<PDFPath> Paths { get; set; } = new List<PDFPath>();
    }

    public class PDFPath
    {
        public string Command { get; set; }
        public List<PDFPathParam> Parameters { get; set; }

        public PDFPath(string command, List<PDFPathParam> args = null)
        {
            Command = command;

            Parameters = args;
        }
    }

    public class PDFPathParam
    {
        public decimal Value { get; set; }
        public string Operation { get; set; }
    }
}
