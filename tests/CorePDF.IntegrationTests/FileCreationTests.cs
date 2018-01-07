using CorePDF.Contents;
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
        public void CreatePDF_WithImageByteData_ExpectFileCreated()
        {
            // arrange
            _sut = new Document();
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

            _sut.Images = new List<Embeds.ImageFile>
            {
                new Embeds.ImageFile
                {
                    Name = "bytedata",
                    ByteData = byteData,
                    Width = 3,
                    Height = 3,
                    Type = Embeds.ImageFile.IMAGEDATA
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
                        ScaleFactor = 10m
                    },
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
