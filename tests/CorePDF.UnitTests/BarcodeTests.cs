using CorePDF.Pages;
using CorePDF.TypeFaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class BarcodeTests
    {
        private readonly Mock<PageRoot> _pageRoot = new Mock<PageRoot>();
        private readonly Size _pageSize = new Size { Width = 500, Height = 500, Orientation = Orientation.Portrait };
        private readonly List<Font> _fonts = Fonts.Styles().Concat(Fonts.Barcodes()).ToList();

        public BarcodeTests()
        {
            for (var i = 0; i < _fonts.Count; i++) _fonts[i].Id = "F" + (i + 1);
        }

        private string Prepare(Contents.Barcode barcode)
        {
            barcode.PrepareStream(_pageRoot.Object, _pageSize, _fonts, false);
            return barcode.GetEncodedString();
        }

        public static IEnumerable<object[]> PrintableCharacters()
        {
            return Enumerable.Range(0x20, 0x7F - 0x20).Select(c => new object[] { ((char)c).ToString() });
        }

        [Theory]
        [MemberData(nameof(PrintableCharacters))]
        public void Code128_EveryCheckValue_IsDrawn(string text)
        {
            // a single character gives every possible check value (including 95-102, the special codes)
            var stream = Prepare(new Contents.Barcode { Text = text, FontFace = Fonts.BARCODE128, PosX = 100, PosY = 100 });

            Assert.Contains(" l\n", stream);
        }

        [Fact]
        public void Code128_LowerCase_IsSupported()
        {
            var lower = Prepare(new Contents.Barcode { Text = "abc", FontFace = Fonts.BARCODE128, PosX = 100, PosY = 100 });
            var upper = Prepare(new Contents.Barcode { Text = "ABC", FontFace = Fonts.BARCODE128, PosX = 100, PosY = 100 });

            Assert.NotEqual(lower, upper);
        }

        [Fact]
        public void Code39_LowerCase_IsEncodedAsUpperCase()
        {
            var lower = Prepare(new Contents.Barcode { Text = "abc", FontFace = Fonts.BARCODE39, PosX = 100, PosY = 100 });
            var upper = Prepare(new Contents.Barcode { Text = "ABC", FontFace = Fonts.BARCODE39, PosX = 100, PosY = 100 });

            Assert.Equal(upper, lower);
        }

        [Theory]
        [InlineData(Fonts.BARCODE128, "café")]
        [InlineData(Fonts.BARCODE39, "a#b")]
        public void UnsupportedCharacter_ThrowsHelpfulError(string font, string text)
        {
            var ex = Assert.Throws<ArgumentException>(() => Prepare(new Contents.Barcode { Text = text, FontFace = font, PosX = 100, PosY = 100 }));

            Assert.Contains(font, ex.Message);
        }
    }
}
