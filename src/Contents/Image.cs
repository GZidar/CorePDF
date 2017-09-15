using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    public class Image : Content
    {
        public decimal ScaleFactor { get; set; } = 1;
        public ImageFile ImageFile { get; set; }

        public override void PrepareStream(Size pageSize, List<Font> fonts, bool compress)
        {
            var result = "q\n";
            if (Height > 0 && Width > 0)
            {
                // the height and width have been specified so just use them
                result += string.Format("{0} 0 0 {1} {2} {3} cm\n", Width, Height, PosX, PosY);
            }
            else
            {
                // calculate the dimensions based on the source image height and width and the scale factor
                result += string.Format("{0} 0 0 {1} {2} {3} cm\n", ImageFile.Width * ScaleFactor, ImageFile.Height * ScaleFactor, PosX, PosY);
            }
            result += string.Format("/{0} Do\n", ImageFile.Id);
            result += "Q\n";

            _encodedData = Encoding.UTF8.GetBytes(result);

            base.PrepareStream(compress);
        }
    }
}