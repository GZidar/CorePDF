using System;
using System.Globalization;

namespace CorePDF
{
    /// <summary>
    /// Switches the current thread to the invariant culture until disposed.
    /// PDF and SVG syntax always use '.' as the decimal separator, '-' as the minus sign and
    /// the Gregorian calendar, so all formatting and parsing must ignore the caller's culture.
    /// </summary>
    internal sealed class InvariantCultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public InvariantCultureScope()
        {
            _originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }
    }
}
