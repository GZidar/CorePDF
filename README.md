# CorePDF
A basic PDF library that works with .net core that you can do with as you please.

## Important
This library does not (yet) support the creation of encrypted or secure PDFs. So it should not be used
to store sensitive information. 

## Example
``` csharp
document = new Document();

// Add any images that you want to include in the document
document.Images = new List<Embeds.ImageFile>
{
    new Embeds.ImageFile
    {
        Name = "smiley",
        FilePath = "smiley.jpg"
    }
};

// Add any extra fonts you want to include in the document. Please note that 
// the widths (below) are based on the characters being organised in the ASCII order (see next line)
//  !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~
// note: space is the first non-zero sized character (position 32)
document.EmbedFont(new FontFile
{
    Name = "GreatVibes-Regular",
    BaseFont = "GreatVibes",
    FilePath = "GreatVibes-Regular.ttf",
    MaximumWidth = 2031, // this value is specific to the font being embeded
    AverageWidth = 286, // this value is specific to the font being embeded
    Descent = -400, // this value is specific to the font being embeded
    StemV = 28, // this value is specific to the font being embeded
    CapHeight = 850, // this value is specific to the font being embeded
    ItalicAngle = 0, // this value is specific to the font being embeded
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

// Add the content for each page
document.Pages.Add(new Page
{
    PageSize = Paper.PAGEA4PORTRAIT,
    Contents = new List<Content>()
    {
        new Image
        {
            ImageName = "smiley",
            PosX = 200,
            PosY = 600,
            ScaleFactor = 0.4m
        },
        new TextBox
        {
            Text = "This is a test document",
            FontFace = "GreatVibes-Regular",
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

// Make the file
using (var filestream = new FileStream("sample.pdf", FileMode.Create, FileAccess.Write))
{
    document.Publish(filestream);
}
```

# License
Copyright (c) 2017 CorePDF Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Please note
This codebase uses [SixLabors/ImageSharp](https://github.com/SixLabors/ImageSharp) 
for image file manipulation and support which is released under a different license. So 
please make sure that you include all relevant notices and attributions in any projects 
that include this codebase.

### ImageSharp License
Copyright 2012 James South

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

### Great Vibes Open Font License
Copyright (c) 2012, TypeSETit, LLC (typesetit@att.net), 
with Reserved Font Name "Great Vibes"

This Font Software is licensed under the SIL Open Font License, Version 1.1.

https://raw.githubusercontent.com/GZidar/CorePDF/development/src/OFL.txt
