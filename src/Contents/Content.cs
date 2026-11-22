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
        private LinkArea _linkBounds;

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

        /// <summary>
        /// A URL (eg: https://example.com or mailto:someone@example.com) that is opened when
        /// the content is clicked. The clickable area is the area the content covers on the page.
        /// </summary>
        public string Hyperlink { get; set; }

        public virtual void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            base.PrepareStream(compress);
        }

        /// <summary>
        /// Records the area of the page covered by the content. Called while the content is being
        /// prepared, as that is when its final size and position are known.
        /// </summary>
        protected void SetLinkBounds(decimal left, decimal bottom, decimal right, decimal top)
        {
            _linkBounds = new LinkArea
            {
                Left = left < right ? left : right,
                Right = left < right ? right : left,
                Bottom = bottom < top ? bottom : top,
                Top = bottom < top ? top : bottom,
                Uri = Hyperlink
            };
        }

        /// <summary>
        /// The clickable links within this content
        /// </summary>
        internal virtual IEnumerable<LinkArea> GetLinks()
        {
            if (!string.IsNullOrWhiteSpace(Hyperlink) && _linkBounds != null)
            {
                _linkBounds.Uri = Hyperlink;
                yield return _linkBounds;
            }
        }
    }

    /// <summary>
    /// A clickable area on the page
    /// </summary>
    internal class LinkArea
    {
        public decimal Left { get; set; }
        public decimal Bottom { get; set; }
        public decimal Right { get; set; }
        public decimal Top { get; set; }
        public string Uri { get; set; }
    }
}
