using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.Pages
{
    public class Size
    {
        public string Name { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public Orientation Orientation { get; set; }
        public int ContentWidth
        {
            get
            {
                var width = Width;
                if (Orientation == Orientation.Landscape)
                {
                    width = Height;
                }
                return width;
            }
        }
        public int ContentHeight
        {
            get
            {
                var height = Height;
                if (Orientation == Orientation.Landscape)
                {
                    height = Width;
                }
                return height;
            }
        }
    }

}
