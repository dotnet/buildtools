using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Xunit.UwpClient
{
    internal class StreamWrapper : Stream, IStream
    {
        private Stream stream;

        public StreamWrapper(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead
        {
            get
            {
                return this.stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.stream.Position;
            }

            set
            {
                this.stream.Position = value;
            }
        }

        public void Clone(out IStream ppstm)
        {
            var clone = new StreamWrapper(this.stream);
            ppstm = clone;
        }

        public void Commit(int grfCommitFlags)
        {
            this.stream.Flush();
        }

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            var bytes = new byte[cb];
            int read = this.stream.Read(bytes, 0, (int)cb);
            if (pcbRead != IntPtr.Zero)
            {
                Marshal.WriteInt64(pcbRead, read);
            }
            pstm.Write(bytes, (int)cb, pcbWritten);
        }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public void LockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.stream.Read(buffer, offset, count);
        }

        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            var read = this.Read(pv, 0, cb);
            if (pcbRead != IntPtr.Zero)
            {
                Marshal.WriteInt64(pcbRead, read);
            }
        }

        public void Revert()
        {

        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.stream.Seek(offset, origin);
        }

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
            var pos = this.Seek(dlibMove, (SeekOrigin)dwOrigin);
            if (plibNewPosition != IntPtr.Zero)
            {
                Marshal.WriteInt64(plibNewPosition, pos);
            }
        }

        public override void SetLength(long value)
        {
            this.stream.SetLength(value);
        }

        public void SetSize(long libNewSize)
        {
            this.SetLength(libNewSize);
        }

        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG();
            pstatstg.cbSize = this.stream.Length;
        }

        public void UnlockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);
        }

        public void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            this.Write(pv, 0, cb);
            if (pcbWritten != IntPtr.Zero)
            {
                Marshal.WriteInt64(pcbWritten, cb);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
            base.Dispose(disposing);
        }
    }
}
