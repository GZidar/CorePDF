using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    /// <summary>
    /// Image content to be shown on the page. Positions are set to be the LLC of the image
    /// Height and Width are optional unless you wish to alter the images aspect ratio.
    /// </summary>
    public class Image : Content
    {
        /// <summary>
        /// A value between 0 and 1. Used as a multiple of the source image's width and height
        /// </summary>
        public decimal ScaleFactor { get; set; } = 1;

        /// <summary>
        /// A reference to an image that has been added to the document
        /// </summary>
        public string ImageName { get; set; }

        public override void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            var imageFile = pageRoot.Document.GetImage(ImageName);
            if (imageFile == null) return;

            var result = "q\n";
            if (Height > 0 && Width > 0)
            {
                // the height and width have been specified so just use them
                result += string.Format("{0} 0 0 {1} {2} {3} cm\n", Width, Height, PosX, PosY);
            }
            else
            {
                // calculate the dimensions based on the source image height and width and the scale factor
                result += string.Format("{0} 0 0 {1} {2} {3} cm\n", imageFile.Width * ScaleFactor, imageFile.Height * ScaleFactor, PosX, PosY);
            }
            result += string.Format("/{0} Do\n", imageFile.Id);
            result += "Q";

            _encodedData = Encoding.UTF8.GetBytes(result);

            base.PrepareStream(compress);
        }
    }
}