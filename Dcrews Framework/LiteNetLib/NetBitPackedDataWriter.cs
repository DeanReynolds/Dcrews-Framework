using Dcrew.Framework.BitPacker;
using System;

namespace Dcrew.Framework.LiteNetLib
{
    public class NetBitPackedDataWriter : BitPackedDataWriter
    {
        public NetBitPackedDataWriter()
        {
            EnsureSize(_lengthBits = 3);
        }

        public new byte[] Data
        {
            get
            {
                var data = new byte[LengthBytes];
                WriteByte((byte)((LengthBytes * 8) - _lengthBits), 3, _data, 0);
                Array.Copy(_data, data, LengthBytes);
                return data;
            }
        }

        public new int LengthBits => _lengthBits - 3;
        public new int LengthBytes => (_lengthBits - 3 + 7) >> 3;
        public new int ReadBits => _readBits - 3;
    }
}