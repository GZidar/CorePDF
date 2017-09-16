namespace CorePDF.Pages
{
    /// <summary>
    /// Defines the available sizes for the pages in the document
    /// </summary>
    public class Size
    {
        /// <summary>
        /// The name of the page size
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The height of the page in points
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The width of the page in points
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Defines the orientation of the page (Portrait or Landscape)
        /// </summary>
        public Orientation Orientation { get; set; }

        /// <summary>
        /// Determines the content width which is calculated from the page width
        /// and orientation.
        /// </summary>
        public int ContentWidth
        {
            get
            {
                var width = Width;
                if (Orientation == Orientation.Landscape)
                {
                    width = Height;
                }
                return width;
            }
        }

        /// <summary>
        /// Determines the content width which is calculated from the page width
        /// and orientation.
        /// </summary>
        public int ContentHeight
        {
            get
            {
                var height = Height;
                if (Orientation == Orientation.Landscape)
                {
                    height = Width;
                }
                return height;
            }
        }
    }

}
