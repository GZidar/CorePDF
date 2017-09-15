using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    public class Stroke
    {
        public decimal Width { get; set; } = 1;
        public string Color { get; set; } = "";
        public string DashPattern { get; set; } = "";
    }
}
