using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CorePDF.Writing
{
    /// <summary>
    /// Wraps the stream the document is being written to. It keeps its own byte count, so
    /// the cross reference table can be built without the underlying stream needing to be
    /// seekable (eg: an HTTP response body), records where each object starts, and hashes
    /// the output so a deterministic document /ID can be produced.
    /// </summary>
    internal sealed class PdfOutputStream : Stream
    {
        private readonly Stream _inner;
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        private long _position;

        public PdfOutputStream(Stream inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// The byte offset at which each object number was written
        /// </summary>
        public Dictionary<int, long> ObjectOffsets { get; } = new Dictionary<int, long>();

        public void RecordObject(int objectNumber, long position)
        {
            if (objectNumber <= 0)
            {
                throw new InvalidOperationException("An object was published before being given an object number.");
            }
            if (ObjectOffsets.ContainsKey(objectNumber))
            {
                throw new InvalidOperationException($"Object number {objectNumber} was published more than once.");
            }

            ObjectOffsets[objectNumber] = position;
        }

        /// <summary>
        /// Returns a hash of everything written so far
        /// </summary>
        public byte[] GetHashSoFar()
        {
            return _hash.GetCurrentHash();
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _position;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _hash.AppendData(buffer, offset, count);
            _position += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
            _hash.AppendData(buffer);
            _position += buffer.Length;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // the caller owns the underlying stream, so it is deliberately left open
            if (disposing)
            {
                _hash.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
