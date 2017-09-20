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
        public string StartCharacter { get; set; }
        public string StopCharacter { get; set; }
        public List<string> Definitions { get; set; }
    }
}
