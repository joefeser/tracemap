using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Core {

    /// <summary>
    /// Wrapper around a byte array that is designed for low level writes to a byte array.
    /// </summary>
    public class ByteArrayWriter {

        private Encoding _encoding = UTF8Encoding.UTF8;

        private byte[] _buffer;

        public int Position {
            get;
            private set;
        }

        public ByteArrayWriter(int length)
            : this(new byte[length]) {
        }

        public ByteArrayWriter(byte[] buffer) {
            _buffer = buffer;
        }

        public byte[] Buffer() {
            return _buffer;
        }

        /// <summary>
        /// Reset position to 0
        /// </summary>
        public void Reset() {
            Position = 0;
        }

        /// <summary>
        /// If we are going to store the writer, we want to truncate the buffer to save memory.
        /// </summary>
        public void Truncate() {
            if (Position == 0) {
                _buffer = new byte[128];
                return;
            }
            var buffer = new byte[Position];
            System.Buffer.BlockCopy(_buffer, 0, buffer, 0, Position);
            _buffer = buffer;
        }

        public virtual void Write(bool value) {
            var buffer = value ? ((byte)1) : ((byte)0);
            _buffer[Position++] = buffer;
        }

        public virtual void Write(byte value) {
            _buffer[Position++] = value;
        }

        public virtual void Write(byte[] buffer) {
            if (buffer == null) {
                throw new ArgumentNullException("buffer");
            }
            System.Buffer.BlockCopy(buffer, 0, _buffer, Position, buffer.Length);
            Position += buffer.Length;
        }

        public virtual void Write(short value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
        }

        public virtual void Write(int value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
            this._buffer[Position++] = (byte)(value >> 16);
            this._buffer[Position++] = (byte)(value >> 24);
        }

        public virtual void Write(long value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
            this._buffer[Position++] = (byte)(value >> 16);
            this._buffer[Position++] = (byte)(value >> 24);
            this._buffer[Position++] = (byte)(value >> 32);
            this._buffer[Position++] = (byte)(value >> 40);
            this._buffer[Position++] = (byte)(value >> 48);
            this._buffer[Position++] = (byte)(value >> 56);
        }

        [CLSCompliant(false)]
        public virtual void Write(sbyte value) {
            this._buffer[Position++] = (byte)value;
        }

        /// <summary>
        /// Verify you have space before calling write.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int GetByteCount(string value) {
            if (string.IsNullOrEmpty(value)) {
                return 0;
            }
            return this._encoding.GetByteCount(value);
        }

        /// <summary>
        /// Get the bytes for a string.
        /// </summary>
        /// <param name="value">The string</param>
        /// <returns></returns>
        public byte[] GetBytes(string value) {
            if (string.IsNullOrEmpty(value)) {
                return new byte[] { }; //return an empty byte array, the string may be empty
            }
            return this._encoding.GetBytes(value);
        }

        public virtual void Write(string value) {
            if (string.IsNullOrEmpty(value)) {
                return;
            }
            var bytes = _encoding.GetBytes(value);
            Write(bytes);
        }

        [CLSCompliant(false)]
        public virtual void Write(ushort value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
        }

        [CLSCompliant(false)]
        public virtual void Write(uint value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
            this._buffer[Position++] = (byte)(value >> 16);
            this._buffer[Position++] = (byte)(value >> 24);
        }

        [CLSCompliant(false)]
        public virtual void Write(ulong value) {
            this._buffer[Position++] = (byte)value;
            this._buffer[Position++] = (byte)(value >> 8);
            this._buffer[Position++] = (byte)(value >> 16);
            this._buffer[Position++] = (byte)(value >> 24);
            this._buffer[Position++] = (byte)(value >> 32);
            this._buffer[Position++] = (byte)(value >> 40);
            this._buffer[Position++] = (byte)(value >> 48);
            this._buffer[Position++] = (byte)(value >> 56);
        }

        public virtual void Write(byte[] buffer, int index, int count) {
            if (buffer == null) {
                throw new ArgumentNullException("buffer");
            }
            System.Buffer.BlockCopy(buffer, index, _buffer, Position, count);
            Position += count - index;
        }

    }
}
