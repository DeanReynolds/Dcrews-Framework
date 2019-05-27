using Dcrew.Framework.BitPacker;
using LiteNetLib.Utils;
using System;

namespace Dcrew.Framework.LiteNetLib
{
    public class NetBitPacket : BitStream
    {
        public NetBitPacket()
        {
            EnsureSize(_lengthBits = 3);
        }

        public NetBitPacket(NetDataReader dataReader)
        {
            _data = new byte[dataReader.UserDataSize];
            Array.Copy(dataReader.RawData, dataReader.UserDataOffset, _data, 0, dataReader.UserDataSize);
            _lengthBits = ((dataReader.UserDataSize * 8) - BitWriter.ReadByte(_data, (_readBits = 3), 0));
        }

        public new byte[] Data
        {
            get
            {
                var data = new byte[LengthBytes];
                BitWriter.WriteByte((byte)((LengthBytes * 8) - _lengthBits), 3, _data, 0);
                Array.Copy(_data, data, LengthBytes);
                return data;
            }
        }

        public new int LengthBits => _lengthBits - 3;
        public new int LengthBytes => (_lengthBits - 3 + 7) >> 3;
        public new int ReadBits => _readBits - 3;
    }
}