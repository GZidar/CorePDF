using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace CorePDF.IntegrationTests
{
    [Trait("Category", "Integration")]
    public class FileCreationTests : IDisposable
    {
        private Document _sut { get; set; }
        private string _fileName { get; set; }

        public FileCreationTests()
        {
            _fileName = DateTime.Now.Ticks.ToString() + ".pdf";
        }

        public void Dispose()
        {
            // clean up any files that might have been created by the tests
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }
        }

        [Fact]
        public void CreatePDF_WithTable_ExpectFileCreated()
        {
            _sut = new Document();

            // Arrange
            var table = new Table();
            table.Width = 400;
            table.PosX = 20;
            table.PosY = 300;
            table.Border = new BorderPattern
            {
                Top = new Stroke
                {
                    Width = 1
                },
                Left = new Stroke
                {
                    Width = 1
                },
                Bottom = new Stroke
                {
                    Width = 1
                },
                Right = new Stroke
                {
                    Width = 1
                }
            };
            table.Padding.Bottom = 6;

            var row1 = table.AddRow();
            var row1Column1 = row1.AddColumn();
            row1Column1.Width = 50;
            row1Column1.TextContent = new TextBox
            {
                Text = "Row 1 Column 1",
                FontFace = Fonts.FONTSANSSERIFBOLD,
                FontSize = 20,
                TextAlignment = Alignment.Left
            };

            var row1Column2 = row1.AddColumn();
            row1Column2.Width = 50;
            row1Column2.TextContent = new TextBox
            {
                Text = "Row 1 Column 2",
                FontFace = Fonts.FONTSANSSERIFBOLD,
                FontSize = 20,
                TextAlignment = Alignment.Left
            };

            var row2 = table.AddRow();
            var row2Column1 = row2.AddColumn();
            row2Column1.Width = 33.33M;
            row2Column1.TextContent = new TextBox
            {
                Text = "Row 2 Column 1",
                FontSize = 20,
                TextAlignment = Alignment.Left
            };

            var row2Column2 = row2.AddColumn();
            row2Column2.Width = 33.33M;
            row2Column2.TextContent = new TextBox
            {
                Text = "Row 2 Column 2",
                FontFace = Fonts.FONTSANSSERIFITALIC,
                FontSize = 20,
                TextAlignment = Alignment.Center
            };

            var row2Column3 = row2.AddColumn();
            row2Column3.Width = 33.34M;
            row2Column3.TextContent = new TextBox
            {
                Text = "Row 2 Column 3",
                FontFace = Fonts.FONTSANSSERIFBOLDITALIC,
                FontSize = 20,
                TextAlignment = Alignment.Right
            };

            var row3 = table.AddRow();
            var row3Column1 = row3.AddColumn();
            row3Column1.Width = 100M;
            row3Column1.TextContent = new TextBox
            {
                Text = "Row 3 Column 1",
                FontFace = Fonts.FONTSANSSERIF,
                FontSize = 20,
                TextAlignment = Alignment.Left
            };

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    table
                }
            });

            // Act
            using (var filestream = new FileStream(_fileName, FileMode.Create, FileAccess.Write))
            {
                _sut.Publish(filestream);
            }

            // Assert
            Assert.True(File.Exists(_fileName), "The file was not created");
        }

        [Fact]
        public void CreatePDF_WithEmbeddedFont_ExpectFileCreated()
        {
            _sut = new Document();

            // Widths are based on the characters being organised in the ASCII order (see next line)
            //  !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~
            // note: space is the first character
            _sut.EmbedFont(new FontFile
            {
                Name = "GreatVibes-Regular",
                BaseFont = "GreatVibes",
                FilePath = "GreatVibes-Regular.ttf",
                MaximumWidth = 2031,
                AverageWidth = 286,
                Descent = -400,
                StemV = 28,
                Ascent = 851,
                CapHeight = 850,
                BoundingBoxLeft = -402,
                ItalicAngle = 0,
                Widths = new List<int>
                {
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                    259, 350, 318, 746, 449, 621, 606, 158, 350, 518, 431, 458, 153, 406, 208,
                    548, 502, 378, 488, 458, 464, 445, 421, 488, 420, 435, 256, 255, 329, 458,
                    329, 382, 956, 712, 1041, 710, 1014, 815, 1048, 699, 1381, 912, 966, 1144,
                    712, 1329, 1039, 726, 890, 752, 1024, 901, 918, 942, 975, 1344, 807, 959,
                    677, 585, 867, 556, 370, 806, 211, 350, 345, 260, 367, 246, 198, 392, 332,
                    174, 177, 363, 212, 507, 335, 335, 336, 342, 260, 256, 200, 357, 262, 476,
                    333, 399, 341, 369, 485, 485, 485, 0
                }
            });

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new TextBox
                    {
                        Text = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCD\nEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcde\nfghijklmnopqrstuvwxyz{|}~",
                        FontFace = "GreatVibes-Regular",
                        FontSize = 20,
                        PosX = 40,
                        PosY = 400,
                        Width = 500,
                        TextAlignment = Alignment.Left
                    },
                }
            });

            _sut.CompressContent = true;

            // Act
            using (var filestream = new FileStream(_fileName, FileMode.Create, FileAccess.Write))
            {
                _sut.Publish(filestream);
            }

            // Assert
            Assert.True(File.Exists(_fileName), "The file was not created");
        }

        [Fact]
        public void CreatePDF_WithImageByteData_ExpectFileCreated()
        {
            // arrange
            byte[] byteData;
            using (var filestream = new FileStream("mapimage2.jpeg", FileMode.Open, FileAccess.Read))
            {
                using (var memstream = new MemoryStream())
                {
                    filestream.CopyTo(memstream);
                    byteData = memstream.ToArray();
                }
            }

            _sut = new Document();

            _sut.Images = new List<Embeds.ImageFile>
            {
                new Embeds.ImageFile
                {
                    Name = "bytedata",
                    FileData = byteData
                }
            };

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new Image
                    {
                        ImageName = "bytedata",
                        PosX = 20,
                        PosY = 600,
                        ScaleFactor = 0.16m
                    }
                }
            });

            // Act
            using (var filestream = new FileStream(_fileName, FileMode.Create, FileAccess.Write))
            {
                _sut.Publish(filestream);
            }

            // Assert
            Assert.True(File.Exists(_fileName), "The file was not created");
        }

        [Fact]
        public void CreatePDF_ExpectFileCreated()
        {
            // Arrange
            _sut = new Document();

            _sut.Images = new List<Embeds.ImageFile>
            {
                new Embeds.ImageFile
                {
                    Name = "vector",
                    FilePath = "sample.svg"
                },
                //new Embeds.ImageFile
                //{
                //    Name = "smiley",
                //    FilePath = "smiley.jpg"
                //},
                //new Embeds.ImageFile
                //{
                //    Name = "sample",
                //    FilePath = "sample.png"
                //},
                //new Embeds.ImageFile
                //{
                //    Name = "toucan",
                //    FilePath = "rpng2-bg16-toucan.png"
                //},
            };

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new Image
                    {
                        ImageName = "vector",
                        PosX = 20,
                        PosY = 600,
                        ScaleFactor = 0.4m
                    },
                    //new Image
                    //{
                    //    ImageName = "toucan",
                    //    PosX = 300,
                    //    PosY = 600
                    //},
                    //new Image
                    //{
                    //    ImageName = "sample",
                    //    PosX = 400,
                    //    PosY = 600
                    //},
                    //new Image
                    //{
                    //    ImageName = "smiley",
                    //    PosX = 200,
                    //    PosY = 600,
                    //    ScaleFactor = 0.2m
                    //},
                    new TextBox
                    {
                        Text = "This is a test document",
                        FontSize = 30,
                        PosX = 250,
                        PosY = 400,
                        TextAlignment = Alignment.Center
                    },
                    new Shape
                    {
                        Type = Polygon.Rectangle,
                        PosX = 200,
                        PosY = 200,
                        Height = 300,
                        Width = 300,
                        FillColor = "#ffffff",
                        ZIndex = 0
                    },
                    new Shape
                    {
                        Type = Polygon.Ellipses,
                        PosX = 350,
                        PosY = 350,
                        Stroke = new Stroke
                        {
                            Color = "#ff0000"
                        },
                        Height = 500,
                        Width = 300,
                        ZIndex = 10
                    }
                }
            });

            // Act
            using (var filestream = new FileStream(_fileName, FileMode.Create, FileAccess.Write))
            {
                _sut.Publish(filestream);
            }

            // Assert
            Assert.True(File.Exists(_fileName), "The file was not created");
        }
    }
}
