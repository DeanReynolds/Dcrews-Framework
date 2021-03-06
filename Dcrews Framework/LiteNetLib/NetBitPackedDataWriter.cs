﻿using Dcrew.Framework.BitPacker;
using Microsoft.Xna.Framework;
using System;

namespace LiteNetLib
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
                var lengthBytes = (_lengthBits + 7) >> 3;
                var data = new byte[lengthBytes];
                Array.Copy(_data, data, data.Length);
                WriteByte((byte)((lengthBytes << 3) - _lengthBits), 3, data, 0);
                return data;
            }
        }

        public new int LengthBits => _lengthBits - 3;
        public new int LengthBytes => (_lengthBits - 3 + 7) >> 3;

        public void WriteAngle(float value, int bits) => Write((uint)MGMath.PackAngle(value, bits), bits);
    }
}