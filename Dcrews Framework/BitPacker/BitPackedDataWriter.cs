using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dcrew.Framework.BitPacker
{
    public class BitPackedDataWriter
    {
        const int _overAllocateAmount = 4;

        public static void WriteByte(byte source, int numberOfBits, byte[] destination, int destBitOffset)
        {
            source = (byte)(source & (0xFF >> (8 - numberOfBits)));
            int p = destBitOffset >> 3;
            int bitsUsed = destBitOffset & 0x7;
            int bitsFree = 8 - bitsUsed;
            int bitsLeft = bitsFree - numberOfBits;
            if (bitsLeft >= 0)
            {
                int mask = (0xFF >> bitsFree) | (0xFF << (8 - bitsLeft));
                destination[p] = (byte)((destination[p] & mask) | (source << bitsUsed));
                return;
            }
            destination[p] = (byte)((destination[p] & (0xFF >> bitsFree)) | (source << bitsUsed));
            p += 1;
            destination[p] = (byte)((destination[p] & (0xFF << (numberOfBits - bitsFree))) | (source >> bitsFree));
        }
        public static void WriteBytes(byte[] source, int sourceByteOffset, int numberOfBytes, byte[] destination, int destBitOffset)
        {
            int dstBytePtr = destBitOffset >> 3;
            int firstPartLen = (destBitOffset % 8);
            if (firstPartLen == 0)
            {
                Buffer.BlockCopy(source, sourceByteOffset, destination, dstBytePtr, numberOfBytes);
                return;
            }
            int lastPartLen = 8 - firstPartLen;
            for (int i = 0; i < numberOfBytes; i++)
            {
                byte src = source[sourceByteOffset + i];
                destination[dstBytePtr] &= (byte)(255 >> lastPartLen);
                destination[dstBytePtr] |= (byte)(src << firstPartLen);
                dstBytePtr++;
                destination[dstBytePtr] &= (byte)(255 << firstPartLen);
                destination[dstBytePtr] |= (byte)(src >> lastPartLen);
            }
        }
        public static void WriteUInt16(ushort source, int numberOfBits, byte[] destination, int destinationBitOffset)
        {
            if (numberOfBits == 0)
                return;
#if BIGENDIAN
			uint intSource = source;
			intSource = ((intSource & 0x0000ff00) >> 8) | ((intSource & 0x000000ff) << 8);
			source = (ushort)intSource;
#endif
            if (numberOfBits <= 8)
            {
                WriteByte((byte)source, numberOfBits, destination, destinationBitOffset);
                return;
            }
            WriteByte((byte)source, 8, destination, destinationBitOffset);
            numberOfBits -= 8;
            if (numberOfBits > 0)
                WriteByte((byte)(source >> 8), numberOfBits, destination, destinationBitOffset + 8);
        }
        public static int WriteUInt32(uint source, int numberOfBits, byte[] destination, int destinationBitOffset)
        {
#if BIGENDIAN
			source = ((source & 0xff000000) >> 24) | ((source & 0x00ff0000) >> 8) | ((source & 0x0000ff00) << 8) | ((source & 0x000000ff) << 24);
#endif
            int returnValue = destinationBitOffset + numberOfBits;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)source, numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)source, 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 8), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 8), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 16), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 16), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            WriteByte((byte)(source >> 24), numberOfBits, destination, destinationBitOffset);
            return returnValue;
        }
        public static int WriteUInt64(ulong source, int numberOfBits, byte[] destination, int destinationBitOffset)
        {
#if BIGENDIAN
			source = ((source & 0xff00000000000000L) >> 56) |
				((source & 0x00ff000000000000L) >> 40) |
				((source & 0x0000ff0000000000L) >> 24) |
				((source & 0x000000ff00000000L) >> 8) |
				((source & 0x00000000ff000000L) << 8) |
				((source & 0x0000000000ff0000L) << 24) |
				((source & 0x000000000000ff00L) << 40) |
				((source & 0x00000000000000ffL) << 56);
#endif
            int returnValue = destinationBitOffset + numberOfBits;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)source, numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)source, 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 8), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 8), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 16), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 16), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 24), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 24), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 32), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 32), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 40), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 40), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 48), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 48), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            if (numberOfBits <= 8)
            {
                WriteByte((byte)(source >> 56), numberOfBits, destination, destinationBitOffset);
                return returnValue;
            }
            WriteByte((byte)(source >> 56), 8, destination, destinationBitOffset);
            destinationBitOffset += 8;
            numberOfBits -= 8;
            return returnValue;
        }

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

        public byte[] Data => _data;

        public int LengthBits => _lengthBits;
        public int LengthBytes => (_lengthBits + 7) >> 3;

        protected byte[] _data;
        protected int _lengthBits;

        public void Write(bool value)
        {
            EnsureSize(_lengthBits + 1);
            WriteByte((value ? (byte)1 : (byte)0), 1, _data, _lengthBits);
            _lengthBits += 1;
        }

        public void Write(sbyte source)
        {
            EnsureSize(_lengthBits + 8);
            WriteByte((byte)source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        public void Write(byte source)
        {
            EnsureSize(_lengthBits + 8);
            WriteByte(source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        public void Write(byte[] source)
        {
            var bits = source.Length * 8;
            EnsureSize(_lengthBits + bits);
            WriteBytes(source, 0, source.Length, _data, _lengthBits);
            _lengthBits += bits;
        }

        public void Write(short source)
        {
            EnsureSize(_lengthBits + 16);
            WriteUInt16((ushort)source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }
        public void Write(ushort source)
        {
            EnsureSize(_lengthBits + 16);
            WriteUInt16(source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }

        public void Write(int source)
        {
            EnsureSize(_lengthBits + 32);
            WriteUInt32((uint)source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        public void Write(uint source)
        {
            EnsureSize(_lengthBits + 32);
            WriteUInt32(source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        public void Write(uint source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            WriteUInt32(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        public int WriteRangedInt(int min, int max, int value)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = (uint)(value - min);
            Write(rvalue, numBits);
            return numBits;
        }
        public int WriteVariableUInt(uint value)
        {
            var retval = 1;
            var num1 = value;
            while (num1 >= 0x80)
            {
                Write((byte)(num1 | 0x80));
                num1 = num1 >> 7;
                retval++;
            }
            Write((byte)num1);
            return retval;
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
        public void WriteRangedFloat(float value, float min, float max, int numberOfBits)
        {
            var range = max - min;
            var unit = (value - min) / range;
            var maxVal = (1 << numberOfBits) - 1;
            Write((uint)(maxVal * unit), numberOfBits);
        }

        public void Write(long source)
        {
            EnsureSize(_lengthBits + 64);
            var usource = (ulong)source;
            WriteUInt64(usource, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        public void Write(ulong source)
        {
            EnsureSize(_lengthBits + 64);
            WriteUInt64(source, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        public void Write(ulong source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            WriteUInt64(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        public int WriteRangedLong(long min, long max, long value)
        {
            var range = (ulong)(max - min);
            var numBits = BitsToHoldULong(range);
            var rvalue = (ulong)(value - min);
            Write(rvalue, numBits);
            return numBits;
        }

        public void Write(double source)
        {
            var val = BitConverter.GetBytes(source);
            Write(val);
        }

        public void Write(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                WriteVariableUInt(0);
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(source);
            WriteVariableUInt((uint)bytes.Length);
            Write(bytes);
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