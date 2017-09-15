using CorePDF.Pages;
using CorePDF.TypeFaces;
using System.Collections.Generic;

namespace CorePDF.Contents
{
    /// <summary>
    /// This contains the various elements that are to be shown on the page. It is
    /// important to note that the positions are relative to the bottom left corner 
    /// of the page's media box.
    /// </summary>
    public class Content : PDFObject
    {
        /// <summary>
        /// This relates to the X co-ordinate of Lower Left Corner (LLC) of the content 
        /// block, unless it is a textbox, then this relates to the below:
        ///     - Left Aligned: Lower Left of the top line of the text box
        ///     - Centred: Mid point of the top line of the text box
        ///     - Right Aligned: Lower Right of the top line of the text box
        /// for an ellipses the position is the centre of the shape
        /// </summary>
        public int PosX { get; set; }

        /// <summary>
        /// This relates to the Y co-ordinate of the Lower Left Corner (LLC) of the content 
        /// block, unless it is a textbox, then this relates to the below:
        ///     - Left Aligned: Lower Left of the top line of the text box
        ///     - Centred: Mid point of the top line of the text box
        ///     - Right Aligned: Lower Right of the top line of the tex box
        /// for an ellipses the position is the centre of the shape
        /// </summary>
        public int PosY { get; set; }

        /// <summary>
        /// Vertical positioning of the content. Items with lower values are shown below
        /// those with higher values
        /// </summary>
        public int ZIndex { get; set; }

        /// <summary>
        /// The width of the content in points
        /// </summary>
        public int Width { get; set; } = 0;

        /// <summary>
        /// The height of the content in points
        /// </summary>
        public int Height { get; set; } = 0;

        public virtual void PrepareStream(Size pageSize, List<Font> fonts, bool compress)
        {
            base.PrepareStream(compress);
        }
    }

}
