using CorePDF.Contents;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.IO;
using System.Text;
using Xunit;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class ImageTests
    {
        private Mock<Size> _pageSize = new Mock<Size>();
        private Mock<PageRoot> _pageRoot = new Mock<PageRoot>();
        private Mock<List<Font>> _fonts = new Mock<List<Font>>();
        private Mock<Document> _document = new Mock<Document>();

        private Image _sut;

        public ImageTests()
        {
            var byteData = new byte[27];

            byteData[0] = 255;
            byteData[1] = 0;
            byteData[2] = 0;
            byteData[3] = 0;
            byteData[4] = 255;
            byteData[5] = 0;
            byteData[6] = 0;
            byteData[7] = 0;
            byteData[8] = 255;
            byteData[9] = 0;
            byteData[10] = 0;
            byteData[11] = 0;
            byteData[12] = 255;
            byteData[13] = 255;
            byteData[14] = 255;
            byteData[15] = 128;
            byteData[16] = 128;
            byteData[17] = 128;
            byteData[18] = 255;
            byteData[19] = 0;
            byteData[20] = 0;
            byteData[21] = 255;
            byteData[22] = 0;
            byteData[23] = 0;
            byteData[24] = 255;
            byteData[25] = 0;
            byteData[26] = 0;

            _pageRoot.SetupGet(pr => pr.Document).Returns(_document.Object);
            _document.Setup(d => d.GetImage(It.IsAny<string>())).Returns(new Embeds.ImageFile
            {
                Id = "I1",
                ByteData = byteData,
                Width = 3,
                Height = 3,
                Type = Embeds.ImageFile.IMAGEDATA
            });

            _sut = new Image();
        }

        [Theory]
        [InlineData(100, 200, 1)]
        [InlineData(100, 200, 10)]
        public void CreateImage_ExpectSuccess(int posX, int posY, decimal scale)
        {
            // Arrange
            var result = "";

            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.ScaleFactor = scale;

            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var imageDef = "/I1 Do";
            var placement = string.Format("{0} 0 0 {0} {1} {2} cm", scale * 3, posX, posY); // the 3 comes from the image definition in the constructor

            // Act
            using (var memStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memStream))
                {
                    _sut.Publish(writer);
                }

                result = Encoding.UTF8.GetString(memStream.ToArray());
            }
            var lines = result.Split('\n');
            int.TryParse(lines.FirstOrDefault(l => l.Contains("/Length"))?.Trim().Substring(7), out int length);
            var instream = false;
            var stream = "";

            foreach (var line in lines)
            {
                if (line.Contains("endstream"))
                {
                    instream = false;
                    break;
                }
                if (line.Contains("stream"))
                {
                    instream = true;
                    continue;
                }
                if (instream)
                {
                    stream += line + "\n";
                }
            }

            // Assert
            Assert.False(string.IsNullOrEmpty(result));
            Assert.True(lines.Any(l => l.Contains("/Length")));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.True(lines.Any(l => l.Contains(imageDef)));
            Assert.True(lines.Any(l => l.Contains(placement)));
        }

        [Theory]
        //[InlineData(0, 0, 25, 25, -30, 0, 0, 25, 25)]
        [InlineData(50, 25, 25, 25, -30, 0, 1, 100, 50)]
        public void CreateCubicBezier_fromArc_ExpectSuccess(decimal startX, decimal startY, decimal rx, decimal ry, double rotation, int longArc, int sweep, decimal endX, decimal endY)
        {
            var curvature = 0.55191502449m;


            var bounds = new Rectangle();

            var curX = startX;
            var curY = startY;
            var curRX = rx;
            var curRY = ry;

            var count = 0;

            // recalculate the radii based on the position of the end point
            var radius = (decimal)Math.Sqrt((double)((endX - startX)*(endX - startX) + (endY - startY)*(endY - startY))) / 2m;
            rotation = 0 - Math.Atan((double)((endY - startY) / (endX - startX))) * 180 / Math.PI;

            do
            {
                if (count == 0 || count == 2)
                {
                    curRX = radius;
                    curRY = ry;
                }
                else
                {
                    curRX = rx;
                    curRY = radius;
                }

                // rotate the box 
                rotation = rotation + (90 * count);

                // convert degress to radians
                var radians = (rotation * Math.PI / 180f);

                // calculate the bounding box

                // bottom left corner
                bounds.D.X = curX;
                bounds.D.Y = curY;

                // bottom right corner
                bounds.C.X = bounds.D.X + (curRX * (decimal)Math.Cos(0 - radians));
                bounds.C.Y = bounds.D.Y + (curRX * (decimal)Math.Sin(0 - radians));

                // top left corner
                bounds.A.X = bounds.D.X + (curRY * (decimal)Math.Sin(radians));
                bounds.A.Y = bounds.D.Y + (curRY * (decimal)Math.Cos(0 - radians));

                // top right corner
                bounds.B.X = bounds.A.X + (curRX * (decimal)Math.Cos(0 - radians));
                bounds.B.Y = bounds.A.Y + (curRX * (decimal)Math.Sin(0 - radians));

                var path = string.Format("<path fill='none' stroke='green' d='M {0} {1} L {2} {3} {4} {5} {6} {7} z'/>\n", 
                    bounds.D.X + 200, 
                    200 - bounds.D.Y,
                    bounds.C.X + 200,
                    200 - bounds.C.Y,
                    bounds.B.X + 200,
                    200 - bounds.B.Y,
                    bounds.A.X + 200,
                    200 - bounds.A.Y
                    );

                // Calculate the control points (two of these will be used depending on the sweep and the direction)

                // control point 1
                var cpX1 = bounds.D.X + ((curRY * curvature) * (decimal)Math.Sin(radians));
                var cpY1 = bounds.D.Y + ((curRY * curvature) * (decimal)Math.Cos(0 - radians));

                // control point 2
                var cpX2 = bounds.A.X + ((curRX - (curRX * curvature)) * (decimal)Math.Cos(0 - radians));
                var cpY2 = bounds.A.Y + ((curRX - (curRX * curvature)) * (decimal)Math.Sin(0 - radians));

                // control point 3
                var cpX3 = bounds.D.X + ((curRX * curvature) * (decimal)Math.Cos(0 - radians));
                var cpY3 = bounds.D.Y + ((curRX * curvature) * (decimal)Math.Sin(0 - radians));

                // control point 4
                var cpX4 = bounds.C.X + ((curRY - (curRY * curvature)) * (decimal)Math.Sin(radians));
                var cpY4 = bounds.C.Y + ((curRY - (curRY * curvature)) * (decimal)Math.Cos(0 - radians));

                path += string.Format("<path stroke='blue' d='M {0} {1} v1'/>\n", cpX1 + 200, 200 - cpY1);
                path += string.Format("<path stroke='blue' d='M {0} {1} v1'/>\n", cpX2 + 200, 200 - cpY2);
                path += string.Format("<path stroke='blue' d='M {0} {1} v1'/>\n", cpX3 + 200, 200 - cpY3);
                path += string.Format("<path stroke='blue' d='M {0} {1} v1'/>\n", cpX4 + 200, 200 - cpY4);

                // calculate curve
                path += string.Format("<path fill='none' stroke='yellow' d='M {0} {1} C {2} {3} {4} {5} {6} {7}' />",
                    curX + 200,
                    200 - curY,
                    cpX1 + 200,
                    200 - cpY1,
                    cpX2 + 200,
                    200 - cpY2,
                    bounds.B.X + 200,
                    200 - bounds.B.Y);

                curX = bounds.B.X;
                curY = bounds.B.Y;
                count++;

            } while (!PointInRectangle(new Point(endX, endY), bounds));


        }

        private bool PointInRectangle(Point m, Rectangle r)
        {
            var AB = Vector(r.A, r.B);
            var AM = Vector(r.A, m);
            var BC = Vector(r.B, r.C);
            var BM = Vector(r.B, m);
            var dotABAM = Dot(AB, AM);
            var dotABAB = Dot(AB, AB);
            var dotBCBM = Dot(BC, BM);
            var dotBCBC = Dot(BC, BC);
            return 0 <= dotABAM && dotABAM <= dotABAB && 0 <= dotBCBM && dotBCBM <= dotBCBC;
        }

        private Point Vector(Point p1, Point p2)
        {
            return new Point {
                X = (p2.X - p1.X),
                Y = (p2.Y - p1.Y)
            };
        }

        public decimal Dot(Point u, Point v)
        {
            return u.X * v.X + u.Y * v.Y;
        }
    }

    public class Rectangle
    {
        public Point A { get; set; } = new Point();
        public Point B { get; set; } = new Point();
        public Point C { get; set; } = new Point();
        public Point D { get; set; } = new Point();
    }

    public class Point
    {
        private decimal _x;
        private decimal _y;

        public decimal X
        {
            get
            {
                return _x;
            }

            set
            {
                _x = Math.Round(value, 5);
            }
        }
        public decimal Y
        {
            get
            {
                return _y;
            }

            set
            {
                _y = Math.Round(value, 5);
            }
        }

        public Point(decimal? x = null, decimal? y = null)
        {
            if (x.HasValue)
            {
                X = x.Value;
            }
            if (y.HasValue)
            {
                Y = y.Value;
            }
        }
    }
}
