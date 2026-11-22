using System.Collections.Generic;

namespace CorePDF.Writing
{
    /// <summary>
    /// A general purpose PDF object, used for the supporting objects that the public object
    /// model doesn't need to know about (font descriptors, metadata streams, annotations, etc).
    /// </summary>
    internal sealed class RawObject : PDFObject
    {
        public RawObject(int objectNumber, Dictionary<string, dynamic> dictionary, byte[] streamData = null, bool compress = false)
        {
            ObjectNumber = objectNumber;
            _pdfObject = dictionary;

            if (streamData != null)
            {
                _encodedData = streamData;
                PrepareStream(compress);
            }
        }
    }
}
