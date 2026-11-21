using CorePDF.Writing;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CorePDF
{
    public interface IPDFObject
    {
        void PrepareStream(bool compress = false);
        void Publish(StreamWriter stream);
        string GetEncodedString();
    }

    /// <summary>
    /// The base object for the generated PDF document
    /// </summary>
    public abstract class PDFObject : IPDFObject
    {
        protected byte[] _encodedData { get; set; }
        protected Dictionary<string, dynamic> _pdfObject { get; set; } = new Dictionary<string, dynamic>();
        protected bool _compressed { get; set; }

        /// <summary>
        /// The ID of the object within the PDF document - this value is set during document creation
        /// </summary>
        [JsonIgnore, IgnoreDataMember]
        public string Id { get; set; }

        /// <summary>
        /// The number of the object within the PDF document - this value is set during document creation
        /// </summary>
        [JsonIgnore, IgnoreDataMember]
        public int ObjectNumber { get; set; }

        /// <summary>
        /// This will be calculated as the document is published
        /// </summary>
        [JsonIgnore, IgnoreDataMember]
        public long BytePosition { get; set; }

        /// <summary>
        /// This string will be output at the top of the PDF object it relates to
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// True if the object has stream data
        /// </summary>
        internal bool HasStream => _encodedData != null && _encodedData.Length > 0;

        public virtual string GetEncodedString()
        {
            return Encoding.UTF8.GetString(_encodedData);
        }

        public virtual void PrepareStream(bool compress = false)
        {
            // callers set fresh (uncompressed) data before preparing it
            _compressed = false;

            if (compress && _encodedData != null)
            {
                // the zlib format is what the PDF FlateDecode filter expects
                using (var cmpStream = new MemoryStream())
                {
                    using (var zipStream = new ZLibStream(cmpStream, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        zipStream.Write(_encodedData, 0, _encodedData.Length);
                    }

                    _compressed = true;
                    _encodedData = cmpStream.ToArray();
                }
            }
        }

        public virtual void Publish(StreamWriter stream)
        {
            stream.Flush();
            BytePosition = stream.BaseStream.Position;

            if (stream.BaseStream is PdfOutputStream output)
            {
                // record where the object starts for the document's cross reference table
                output.RecordObject(ObjectNumber, BytePosition);
            }

            var comment = (string.IsNullOrEmpty(Comment) ? "" : " % " + Comment);
            var result = string.Format("{0} 0 obj{1}\n", ObjectNumber, comment);

            // Add any derived keys to a copy of the object definition, so the object can be
            // published again (eg: if the document is published more than once)
            var pdfObject = new Dictionary<string, dynamic>(_pdfObject);
            if (_encodedData != null && _encodedData.Length > 0)
            {
                pdfObject["/Length"] = _encodedData.Length.ToString();
            }
            if (_compressed)
            {
                pdfObject["/Filter"] = "/FlateDecode";
            }

            result += ToPDFObject(pdfObject);

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
                if (color.Length == 3)
                {
                    // if the hex color provided in short form turn it into long form
                    color = string.Format("{0}{0}{1}{1}{2}{2}", color[0], color[1], color[2]);
                }
            }

            var result = "";
            if (int.TryParse(color, NumberStyles.AllowHexSpecifier, null, out int value))
            {
                var r = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                var g = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                var b = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                result += Math.Round(r / 255m, 2) + " ";
                result += Math.Round(g / 255m, 2) + " ";
                result += Math.Round(b / 255m, 2);
            }
            else
            {
                // this means that the color could be in the form of a word
                var systemColor = (Color)(new ColorConverter().ConvertFrom(color));

                result += Math.Round(systemColor.R / 255m, 2) + " ";
                result += Math.Round(systemColor.G / 255m, 2) + " ";
                result += Math.Round(systemColor.B / 255m, 2);
            }

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
