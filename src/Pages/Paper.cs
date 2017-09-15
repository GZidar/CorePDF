using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.Pages
{
    public static class Paper
    {
        public static List<Size> Sizes()
        {
            var result = new List<Size>()
            {
                new Size
                {
                    Name = "a3P",
                    Height = 1191,
                    Width = 842,
                    Orientation = Orientation.Portrait
                },
                new Size
                {
                    Name = "a3L",
                    Height = 1191,
                    Width = 842,
                    Orientation = Orientation.Landscape
                },
                new Size
                {
                    Name = "a4P",
                    Height = 842,
                    Width = 595,
                    Orientation = Orientation.Portrait
                },
                new Size
                {
                    Name = "a4L",
                    Height = 842,
                    Width = 595,
                    Orientation = Orientation.Landscape
                },
                new Size
                {
                    Name = "a5P",
                    Height = 595,
                    Width = 420,
                    Orientation = Orientation.Portrait
                },
                new Size
                {
                    Name = "a5L",
                    Height = 595,
                    Width = 420,
                    Orientation = Orientation.Landscape
                }
            };

            return result;
        }

        public static Size Size(string size)
        {
            return Sizes().FirstOrDefault(s => s.Name.ToLower() == size.ToLower());
        }
    }

}
