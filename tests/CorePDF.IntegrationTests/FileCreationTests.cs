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
        public void CreatePDF_ExpectFileCreated()
        {
            // Arrange
            _sut = new Document();

            _sut.Images = new List<Embeds.ImageFile>
            {
                new Embeds.ImageFile
                {
                    Name = "logo",
                    FilePath = "smiley.jpg",
                    Type = Embeds.ImageFile.FILETYPEJPG,
                    Height = 299,
                    Width = 299
                },
                new Embeds.ImageFile
                {
                    Name = "sample",
                    FilePath = "sample.png",
                    Type = Embeds.ImageFile.FILETYPEPNG
                }
            };

            _sut.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new Image
                    {
                        ImageName = "sample",
                        PosX = 400,
                        PosY = 500
                    },
                    new Image
                    {
                        ScaleFactor = 0.1m,
                        ImageName = "logo",
                        PosX = 200,
                        PosY = 500
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
    }
}
