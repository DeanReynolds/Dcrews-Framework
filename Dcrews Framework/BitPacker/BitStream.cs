using System;
using System.Runtime.InteropServices;

namespace Dcrew.Framework.BitPacker
{
    public class BitStream
    {
        const int _overAllocateAmount = 4;

        public static int BitsToHoldUInt(uint value)
        {
            var bits = 1;
            while ((value >>= 1) != 0)
                bits++;
            return bits;
        }
        public static int BitsToHoldULong(ulong value)
        {
            var bits = 1;
            while ((value >>= 1) != 0)
                bits++;
            return bits;
        }

        static uint SwapByteOrder(uint value) => ((value & 0xff000000) >> 24) | ((value & 0x00ff0000) >> 8) | ((value & 0x0000ff00) << 8) | ((value & 0x000000ff) << 24);
        static ulong SwapByteOrder(ulong value) => ((value & 0xff00000000000000L) >> 56) | ((value & 0x00ff000000000000L) >> 40) | ((value & 0x0000ff0000000000L) >> 24) | ((value & 0x000000ff00000000L) >> 8) | ((value & 0x00000000ff000000L) << 8) | ((value & 0x0000000000ff0000L) << 24) | ((value & 0x000000000000ff00L) << 40) | ((value & 0x00000000000000ffL) << 56);

        public byte[] Data
        {
            get
            {
                var data = new byte[LengthBytes];
                Array.Copy(_data, data, LengthBytes);
                return data;
            }
        }

        public int LengthBits => _lengthBits - 3;
        public int LengthBytes => (_lengthBits - 3 + 7) >> 3;
        public int ReadBits => _readBits - 3;
        public bool EndOfData => ReadBits >= LengthBits;

        protected byte[] _data;
        protected int _lengthBits;
        protected int _readBits;

        public void Write(bool value)
        {
            EnsureSize(_lengthBits + 1);
            BitWriter.WriteByte((value ? (byte)1 : (byte)0), 1, _data, _lengthBits);
            _lengthBits += 1;
        }
        public bool ReadBool()
        {
            var retval = BitWriter.ReadByte(_data, 1, _readBits);
            _readBits += 1;
            return (retval == 1);
        }

        public void Write(sbyte source)
        {
            EnsureSize(_lengthBits + 8);
            BitWriter.WriteByte((byte)source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        public sbyte ReadSByte()
        {
            var retval = BitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return (sbyte)retval;
        }
        public void Write(byte source)
        {
            EnsureSize(_lengthBits + 8);
            BitWriter.WriteByte(source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        public byte ReadByte()
        {
            var retval = BitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return retval;
        }
        public bool ReadByte(out byte result)
        {
            if (_lengthBits - _readBits < 8)
            {
                result = 0;
                return false;
            }
            result = BitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return true;
        }
        public byte ReadByte(int numberOfBits)
        {
            var retval = BitWriter.ReadByte(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public void Write(byte[] source)
        {
            var bits = source.Length * 8;
            EnsureSize(_lengthBits + bits);
            BitWriter.WriteBytes(source, 0, source.Length, _data, _lengthBits);
            _lengthBits += bits;
        }
        public byte[] ReadBytes(int numberOfBytes)
        {
            var retval = new byte[numberOfBytes];
            BitWriter.ReadBytes(_data, numberOfBytes, _readBits, retval, 0);
            _readBits += (8 * numberOfBytes);
            return retval;
        }
        public bool ReadBytes(int numberOfBytes, out byte[] result)
        {
            if (_lengthBits - _readBits + 7 < (numberOfBytes * 8))
            {
                result = null;
                return false;
            }
            result = new byte[numberOfBytes];
            BitWriter.ReadBytes(_data, numberOfBytes, _readBits, result, 0);
            _readBits += (8 * numberOfBytes);
            return true;
        }
        public void ReadBytes(byte[] into, int offset, int numberOfBytes)
        {
            BitWriter.ReadBytes(_data, numberOfBytes, _readBits, into, offset);
            _readBits += (8 * numberOfBytes);
            return;
        }

        public void Write(short source)
        {
            EnsureSize(_lengthBits + 16);
            BitWriter.WriteUInt16((ushort)source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }
        public short ReadShort()
        {
            var retval = BitWriter.ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (short)retval;
        }
        public void Write(ushort source)
        {
            EnsureSize(_lengthBits + 16);
            BitWriter.WriteUInt16(source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }
        public ushort ReadUShort()
        {
            var retval = BitWriter.ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (ushort)retval;
        }

        public void Write(int source)
        {
            EnsureSize(_lengthBits + 32);
            BitWriter.WriteUInt32((uint)source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        public int ReadInt()
        {
            var retval = BitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return (int)retval;
        }
        public bool ReadInt(out int result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0;
                return false;
            }
            result = (int)BitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        public int ReadInt(int numberOfBits)
        {
            var retval = BitWriter.ReadUInt32(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            if (numberOfBits == 32)
                return (int)retval;
            var signBit = 1 << (numberOfBits - 1);
            if ((retval & signBit) == 0)
                return (int)retval;
            unchecked
            {
                var mask = ((uint)-1) >> (33 - numberOfBits);
                var tmp = (retval & mask) + 1;
                return -((int)tmp);
            }
        }
        public void Write(uint source)
        {
            EnsureSize(_lengthBits + 32);
            BitWriter.WriteUInt32(source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        public void Write(uint source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            BitWriter.WriteUInt32(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        public uint ReadUInt()
        {
            var retval = BitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return retval;
        }
        public bool ReadUInt(out uint result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0;
                return false;
            }
            result = BitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        public uint ReadUInt(int numberOfBits)
        {
            var retval = BitWriter.ReadUInt32(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public int WriteRangedInt(int min, int max, int value)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = (uint)(value - min);
            Write(rvalue, numBits);
            return numBits;
        }
        public int ReadRangedInt(int min, int max)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = ReadUInt(numBits);
            return (int)(min + rvalue);
        }

        public void Write(float source)
        {
            SingleUIntUnion su;
            su.UIntValue = 0;
            su.SingleValue = source;
#if BIGENDIAN
			su.UIntValue = NetUtility.SwapByteOrder(su.UIntValue);
#endif
            Write(su.UIntValue);
        }
        public float ReadFloat()
        {
            if ((_readBits & 7) == 0)
            {
                var retval = BitConverter.ToSingle(_data, _readBits >> 3);
                _readBits += 32;
                return retval;
            }
            var bytes = new byte[4];
            ReadBytes(bytes, 0, 4);
            return BitConverter.ToSingle(bytes, 0);
        }
        public bool ReadFloat(out float result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0;
                return false;
            }
            if ((_readBits & 7) == 0)
            {
                result = BitConverter.ToSingle(_data, _readBits >> 3);
                _readBits += 32;
                return true;
            }
            var bytes = new byte[4];
            ReadBytes(bytes, 0, 4);
            result = BitConverter.ToSingle(bytes, 0);
            return true;
        }
        public void WriteRangedFloat(float value, float min, float max, int numberOfBits)
        {
            var range = max - min;
            var unit = (value - min) / range;
            var maxVal = (1 << numberOfBits) - 1;
            Write((uint)(maxVal * unit), numberOfBits);
        }
        public float ReadRangedFloat(float min, float max, int numberOfBits)
        {
            var range = max - min;
            var maxVal = (1 << numberOfBits) - 1;
            var encodedVal = (float)ReadUInt(numberOfBits);
            var unit = encodedVal / maxVal;
            return min + (unit * range);
        }

        public void Write(long source)
        {
            EnsureSize(_lengthBits + 64);
            var usource = (ulong)source;
            BitWriter.WriteUInt64(usource, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        public long ReadLong()
        {
            unchecked
            {
                var retval = ReadULong();
                var longRetval = (long)retval;
                return longRetval;
            }
        }
        public long ReadLong(int numberOfBits) => (long)ReadULong(numberOfBits);
        public void Write(ulong source)
        {
            EnsureSize(_lengthBits + 64);
            BitWriter.WriteUInt64(source, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        public void Write(ulong source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            BitWriter.WriteUInt64(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        public ulong ReadULong()
        {
            var low = BitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            var high = BitWriter.ReadUInt32(_data, 32, _readBits);
            var retval = low + (high << 32);
            _readBits += 32;
            return retval;
        }
        public ulong ReadULong(int numberOfBits)
        {
            ulong retval;
            if (numberOfBits <= 32)
                retval = BitWriter.ReadUInt32(_data, numberOfBits, _readBits);
            else
            {
                retval = BitWriter.ReadUInt32(_data, 32, _readBits);
                retval |= (ulong)BitWriter.ReadUInt32(_data, numberOfBits - 32, _readBits + 32) << 32;
            }
            _readBits += numberOfBits;
            return retval;
        }
        public int WriteRangedLong(long min, long max, long value)
        {
            var range = (ulong)(max - min);
            var numBits = BitsToHoldULong(range);
            var rvalue = (ulong)(value - min);
            Write(rvalue, numBits);
            return numBits;
        }
        public long ReadRangedLong(long min, long max)
        {
            var range = (ulong)(max - min);
            var numBits = BitsToHoldULong(range);
            var rvalue = ReadULong(numBits);
            return min + (long)rvalue;
        }

        public void Write(double source)
        {
            var val = BitConverter.GetBytes(source);
            Write(val);
        }
        public double ReadDouble()
        {
            if ((_readBits & 7) == 0)
            {
                var retval = BitConverter.ToDouble(_data, _readBits >> 3);
                _readBits += 64;
                return retval;
            }
            var bytes = new byte[8];
            ReadBytes(bytes, 0, 8);
            return BitConverter.ToDouble(bytes, 0);
        }

        protected void EnsureSize(int bits)
        {
            var byteLen = ((bits + 7) >> 3);
            if (_data == null)
                _data = new byte[byteLen + _overAllocateAmount];
            else if (_data.Length < byteLen)
                Array.Resize(ref _data, byteLen + _overAllocateAmount);
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct SingleUIntUnion
        {
            [FieldOffset(0)]
            public float SingleValue;

            [FieldOffset(0)]
            public uint UIntValue;
        }
    }
}