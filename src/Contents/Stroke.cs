using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    /// <summary>
    /// Defines the stroke used when drawing shapes
    /// </summary>
    public class Stroke
    {
        /// <summary>
        /// The width of the stroke.
        /// </summary>
        public decimal Width { get; set; } = 1;

        /// <summary>
        /// Specifies the color of the stroke line. This is specified using HTML hexadecimal 
        /// color syntax and must be 6 characters long. eg: 
        ///     #ffffff = white, 
        ///     #ff0000 is red, 
        ///     #000000 = black.
        /// Will default to black if left blank.
        /// </summary>
        public string Color { get; set; } = "";

        /// <summary>
        /// Specifies the dash pattern that will be used to draw the line. This
        /// is provided in the form [a b] c, where a is the number of dots on, 
        /// b is the number of dot's off and c is where in the pattern to start.
        /// 
        /// See page 217 of the PDF Specification for more details regarding what 
        /// the second number means. 0 here means that the first dash begins 
        /// immediately.
        /// </summary>
        public string DashPattern { get; set; } = "";
    }
}
