using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.TypeFaces
{
    public class Barcode : Font
    {
        /// <summary>
        /// The list of valid characters in the barcode
        /// </summary>
        public string CharacterSet { get; set; }
        public List<string> Definitions { get; set; }
        public Func<string, string> PreProcessor { get; set; }

    }
}
