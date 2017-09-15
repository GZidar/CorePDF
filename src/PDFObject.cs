using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace CorePDF
{
    public interface IPDFObject
    {
        void PrepareStream(bool compress = false);
        void Publish(StreamWriter stream);
    }

    public abstract class PDFObject : IPDFObject
    {
        protected byte[] _encodedData { get; set; }
        protected Dictionary<string, dynamic> _pdfObject { get; set; } = new Dictionary<string, dynamic>();
        private bool _compressed { get; set; }

        /// <summary>
        /// The ID of the object within the PDF document - this value is set during document creation
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The number of the object within the PDF document - this value is set during document creation
        /// </summary>
        public int ObjectNumber { get; set; }

        /// <summary>
        /// This will be calculated as the document is published
        /// </summary>
        public long BytePosition { get; set; }

        /// <summary>
        /// This string will be output at the top of the PDF object it relates to
        /// </summary>
        public string Comment { get; set; }

        public virtual void PrepareStream(bool compress = false)
        {
            if (compress)
            {
                using (var orgStream = new MemoryStream(_encodedData))
                {
                    using (var cmpStream = new MemoryStream())
                    {
                        using (var zipStream = new DeflaterOutputStream(cmpStream))
                        {
                            orgStream.CopyTo(zipStream);
                        }

                        _compressed = true;
                        _encodedData = cmpStream.ToArray();
                    }
                }
            }
        }

        public virtual void Publish(StreamWriter stream)
        {
            stream.Flush();
            BytePosition = stream.BaseStream.Position;

            var comment = (string.IsNullOrEmpty(Comment) ? "" : " % " + Comment);
            var result = string.Format("{0} 0 obj{1}\n", ObjectNumber, comment);

            // Add any derived keys to the object definition
            if (_encodedData != null && _encodedData.Length > 0)
            {
                _pdfObject.Add("/Length", _encodedData.Length.ToString()); // minus one here to exclude the newline character added to the stream
            }
            if (_compressed)
            {
                _pdfObject.Add("/Filter", "/FlateDecode");
            }

            result += ToPDFObject(_pdfObject);

            stream.Write(result);
            if (_encodedData != null && _encodedData.Length > 0)
            {
                stream.Write("stream\n");
                stream.Flush();
                stream.BaseStream.Write(_encodedData, 0, _encodedData.Length);
                stream.Write("\nendstream\n");
            }
            result = "endobj\n";
            stream.Write(result);
            stream.Flush();
        }

        /// <summary>
        /// Converts the HTML Color strings to PDF color formats
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        protected string ToPDFColor(string color)
        {
            if (color.StartsWith("#"))
            {
                color = color.Substring(1);
            }

            var r = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            var g = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            var b = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            var result = "";
            result += Math.Round(r / 255m, 2) + " ";
            result += Math.Round(g / 255m, 2) + " ";
            result += Math.Round(b / 255m, 2);

            return result;
        }

        /// <summary>
        /// Converts the object dictionary to the PDF syntax
        /// </summary>
        /// <param name="input"></param>
        /// <param name="indent"></param>
        /// <returns></returns>
        protected string ToPDFObject(IDictionary input, string indent = "")
        {
            var result = "<<\n";
            var count = 0;

            foreach (DictionaryEntry item in input)
            {
                count++;
                if (item.Value is IDictionary)
                {
                    result += indent + "  " + item.Key + " " + ToPDFObject((IDictionary)item.Value, indent + "  ");
                }
                else
                {
                    result += indent + string.Format("  {0} {1}\n", item.Key, item.Value);
                }
            }

            result += indent + ">>\n";

            return result;
        }
    }
}
