using Dcrew.Framework.BitPacker;
using LiteNetLib.Utils;
using Microsoft.Xna.Framework;
using System;

namespace LiteNetLib
{
    public class NetBitPackedDataReader : BitPackedDataReader
    {
        public NetBitPackedDataReader(NetDataReader dataReader)
        {
            _data = new byte[dataReader.UserDataSize];
            Array.Copy(dataReader.RawData, dataReader.UserDataOffset, _data, 0, dataReader.UserDataSize);
            _lengthBits = (dataReader.UserDataSize << 3) - ReadByte(_data, _readBits = 3, 0);
        }

        public new byte[] Data => _data;

        public new int LengthBits => _lengthBits - 3;
        public new int LengthBytes => (_lengthBits - 3 + 7) >> 3;
        public new int ReadBits => _readBits - 3;

        public float ReadAngle(int bits) => MGMath.UnpackAngle((int)ReadUInt(bits), bits);
    }
}