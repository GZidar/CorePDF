namespace CorePDF
{
    /// <summary>
    /// The archival standard (if any) that the document should conform to
    /// </summary>
    public enum PdfConformance
    {
        /// <summary>
        /// A standard PDF document
        /// </summary>
        None,

        /// <summary>
        /// PDF/A-2b (ISO 19005-2, basic conformance): the document is self contained so it can
        /// be reliably reproduced in the future. All fonts must be embedded, so text must use
        /// fonts added with Document.EmbedFont rather than the standard PDF fonts.
        /// </summary>
        PdfA2B,

        /// <summary>
        /// PDF/A-2u (ISO 19005-2, Unicode conformance): PDF/A-2b with the additional guarantee
        /// that all text can be extracted as Unicode (eg: for searching and copying).
        /// </summary>
        PdfA2U
    }
}
