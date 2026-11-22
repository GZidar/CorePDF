using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF.Conformance
{
    /// <summary>
    /// Builds a small ICC (version 2) display profile describing the sRGB colour space. PDF/A
    /// documents must say what colour space their RGB colours are in, which is done by
    /// embedding a profile like this one as the document's output intent.
    /// </summary>
    internal static class SrgbIccProfile
    {
        public const string Description = "sRGB IEC61966-2.1";

        private static readonly Lazy<byte[]> _profile = new Lazy<byte[]>(Build);

        public static byte[] Bytes => _profile.Value;

        private static byte[] Build()
        {
            // the sRGB primaries, chromatically adapted to the D50 illuminant that ICC profiles use
            var tags = new List<(string Signature, byte[] Data)>
            {
                ("desc", TextDescription(Description)),
                ("cprt", Text("No copyright, use freely")),
                ("wtpt", Xyz(0.9642, 1.0, 0.8249)),
                ("rXYZ", Xyz(0.4360747, 0.2225045, 0.0139322)),
                ("gXYZ", Xyz(0.3850649, 0.7168786, 0.0971045)),
                ("bXYZ", Xyz(0.1430804, 0.0606169, 0.7141733)),
                ("rTRC", SrgbCurve()),
                ("gTRC", SrgbCurve()),
                ("bTRC", SrgbCurve())
            };

            var tagTableLength = 4 + (tags.Count * 12);
            var offset = 128 + tagTableLength;

            var tagTable = new MemoryStream();
            var tagData = new MemoryStream();
            WriteUInt32(tagTable, (uint)tags.Count);

            foreach (var (signature, data) in tags)
            {
                tagTable.Write(Encoding.ASCII.GetBytes(signature), 0, 4);
                WriteUInt32(tagTable, (uint)(offset + tagData.Length));
                WriteUInt32(tagTable, (uint)data.Length);

                tagData.Write(data, 0, data.Length);
                while (tagData.Length % 4 != 0) tagData.WriteByte(0);
            }

            var size = offset + (int)tagData.Length;
            var header = new MemoryStream();
            WriteUInt32(header, (uint)size); // profile size
            WriteUInt32(header, 0); // preferred CMM
            WriteUInt32(header, 0x02100000); // version 2.1
            header.Write(Encoding.ASCII.GetBytes("mntrRGB XYZ "), 0, 12); // display device, RGB data, XYZ connection space
            foreach (var value in new ushort[] { 2024, 1, 1, 0, 0, 0 }) WriteUInt16(header, value); // creation date
            header.Write(Encoding.ASCII.GetBytes("acsp"), 0, 4); // profile file signature
            header.Write(new byte[24], 0, 24); // platform, flags, manufacturer, model, attributes
            WriteUInt32(header, 0); // rendering intent: perceptual
            WriteS15Fixed16(header, 0.9642); // D50 illuminant
            WriteS15Fixed16(header, 1.0);
            WriteS15Fixed16(header, 0.8249);
            header.Write(new byte[48], 0, 48); // creator and reserved bytes

            var result = new MemoryStream();
            header.WriteTo(result);
            tagTable.WriteTo(result);
            tagData.WriteTo(result);
            return result.ToArray();
        }

        private static byte[] Xyz(double x, double y, double z)
        {
            var result = new MemoryStream();
            result.Write(Encoding.ASCII.GetBytes("XYZ "), 0, 4);
            WriteUInt32(result, 0);
            WriteS15Fixed16(result, x);
            WriteS15Fixed16(result, y);
            WriteS15Fixed16(result, z);
            return result.ToArray();
        }

        private static byte[] SrgbCurve()
        {
            const int points = 1024;

            var result = new MemoryStream();
            result.Write(Encoding.ASCII.GetBytes("curv"), 0, 4);
            WriteUInt32(result, 0);
            WriteUInt32(result, points);
            for (var i = 0; i < points; i++)
            {
                var v = i / (double)(points - 1);
                var linear = v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
                WriteUInt16(result, (ushort)Math.Round(linear * 65535));
            }
            return result.ToArray();
        }

        private static byte[] TextDescription(string text)
        {
            var ascii = Encoding.ASCII.GetBytes(text + "\0");

            var result = new MemoryStream();
            result.Write(Encoding.ASCII.GetBytes("desc"), 0, 4);
            WriteUInt32(result, 0);
            WriteUInt32(result, (uint)ascii.Length);
            result.Write(ascii, 0, ascii.Length);
            WriteUInt32(result, 0); // unicode language code
            WriteUInt32(result, 0); // unicode description length
            WriteUInt16(result, 0); // scriptcode code
            result.WriteByte(0); // scriptcode description length
            result.Write(new byte[67], 0, 67); // scriptcode description
            return result.ToArray();
        }

        private static byte[] Text(string text)
        {
            var result = new MemoryStream();
            result.Write(Encoding.ASCII.GetBytes("text"), 0, 4);
            WriteUInt32(result, 0);
            var ascii = Encoding.ASCII.GetBytes(text + "\0");
            result.Write(ascii, 0, ascii.Length);
            return result.ToArray();
        }

        private static void WriteS15Fixed16(Stream stream, double value)
        {
            WriteUInt32(stream, unchecked((uint)(int)Math.Round(value * 65536)));
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
