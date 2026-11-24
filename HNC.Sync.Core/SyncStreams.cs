using System;
using System.IO;
using System.Text;

namespace HNC.Sync
{
    public sealed class SyncWriter : ISyncWriter
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly BinaryWriter _bw;

        public SyncWriter() { _bw = new BinaryWriter(_ms, Encoding.UTF8); }

        public void WriteBool(bool v) { _bw.Write(v); }
        public void WriteInt(int v) { _bw.Write(v); }
        public void WriteLong(long v) { _bw.Write(v); }
        public void WriteFloat(float v) { _bw.Write(v); }
        public void WriteDouble(double v) { _bw.Write(v); }

        public void WriteString(string v)
        {
            _bw.Write(v != null);
            if (v != null) _bw.Write(v);
        }

        public void WriteBytes(byte[] v)
        {
            if (v == null) { _bw.Write(-1); return; }
            _bw.Write(v.Length);
            _bw.Write(v);
        }

        public byte[] ToArray() { _bw.Flush(); return _ms.ToArray(); }
    }

    public sealed class SyncReader : ISyncReader
    {
        private readonly BinaryReader _br;

        public SyncReader(byte[] data) { _br = new BinaryReader(new MemoryStream(data ?? new byte[0]), Encoding.UTF8); }

        public bool ReadBool() { return _br.ReadBoolean(); }
        public int ReadInt() { return _br.ReadInt32(); }
        public long ReadLong() { return _br.ReadInt64(); }
        public float ReadFloat() { return _br.ReadSingle(); }
        public double ReadDouble() { return _br.ReadDouble(); }
        public string ReadString() { return _br.ReadBoolean() ? _br.ReadString() : null; }

        public byte[] ReadBytes()
        {
            int n = _br.ReadInt32();
            return n < 0 ? null : _br.ReadBytes(n);
        }
    }
}
