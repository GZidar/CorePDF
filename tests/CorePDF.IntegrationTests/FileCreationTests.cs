using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
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
        public void CreatePDF_WithEmbeddedFont_ExpectFileCreated()
        {
            _sut = new Document();

            _sut.EmbedFont(new FontFile
                {
                    Name = "Great Vibes - Regular",
                    BaseFont = "Great Vibes",
                    FilePath = "GreatVibes-Regular.ttf",
                    Widths = new List<int>
                    {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,333,420,500,500,833,778,
                        333,333,333,500,675,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,675,675,
                        675,500,920,611,611,667,722,611,611,722,722,333,444,667,556,833,667,722,611,722,611,500,556,
                        722,611,833,611,556,556,389,278,389,422,500,333,500,500,444,500,444,278,500,500,278,278,444,
                        278,722,500,500,500,500,389,389,278,500,444,667,444,444,389,400,275,400,541,389,500,500,167,
                        500,500,500,500,214,556,500,333,333,500,500,500,500,500,250,523,350,333,556,556,500,889,1000,
                        500,333,333,333,333,333,333,333,333,333,333,333,333,333,889,889,276,556,722,944,310,667,278,
                        278,500,667,500
                    }
                }
            );

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new TextBox
                    {
                        Text = "This is a test document",
                        FontFace = "Great Vibes - Regular",
                        FontSize = 30,
                        PosX = 250,
                        PosY = 400,
                        TextAlignment = Alignment.Center
                    },
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
