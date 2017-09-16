using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.Pages
{
    /// <summary>
    /// Defines the sizes and orientation of the pages used in the document
    /// </summary>
    public static class Paper
    {
        // The list of currently defined paper sizes
        public const string PAGEA4PORTRAIT = "a4P";
        public const string PAGEA4LANDSCAPE = "a4L";
        public const string PAGEA3PORTRAIT = "a3P";
        public const string PAGEA3LANDSCAPE = "a3L";
        public const string PAGEA5PORTRAIT = "a5P";
        public const string PAGEA5LANDSCAPE = "a5L";

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

        /// <summary>
        /// Returns a specific page size from the list matching the passed in value: eg a4P
        /// </summary>
        /// <param name="size">the requested size</param>
        /// <returns></returns>
        public static Size Size(string size)
        {
            return Sizes().FirstOrDefault(s => s.Name.ToLower() == size.ToLower());
        }
    }

}
