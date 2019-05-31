using System;
using System.Runtime.InteropServices;

namespace Dcrew.Framework.BitPacker
{
    public class BitPackedDataReader
    {
        public static byte ReadByte(byte[] fromBuffer, int numberOfBits, int readBitOffset)
        {
            int bytePtr = readBitOffset >> 3;
            int startReadAtIndex = readBitOffset - (bytePtr * 8);
            if (startReadAtIndex == 0 && numberOfBits == 8)
                return fromBuffer[bytePtr];
            byte returnValue = (byte)(fromBuffer[bytePtr] >> startReadAtIndex);
            int numberOfBitsInSecondByte = numberOfBits - (8 - startReadAtIndex);
            if (numberOfBitsInSecondByte < 1)
                return (byte)(returnValue & (255 >> (8 - numberOfBits)));
            byte second = fromBuffer[bytePtr + 1];
            second &= (byte)(255 >> (8 - numberOfBitsInSecondByte));
            return (byte)(returnValue | (byte)(second << (numberOfBits - numberOfBitsInSecondByte)));
        }
        public static void ReadBytes(byte[] fromBuffer, int numberOfBytes, int readBitOffset, byte[] destination, int destinationByteOffset)
        {
            int readPtr = readBitOffset >> 3;
            int startReadAtIndex = readBitOffset - (readPtr * 8);
            if (startReadAtIndex == 0)
            {
                Buffer.BlockCopy(fromBuffer, readPtr, destination, destinationByteOffset, numberOfBytes);
                return;
            }
            int secondPartLen = 8 - startReadAtIndex;
            int secondMask = 255 >> secondPartLen;
            for (int i = 0; i < numberOfBytes; i++)
            {
                int b = fromBuffer[readPtr] >> startReadAtIndex;
                readPtr++;
                int second = fromBuffer[readPtr] & secondMask;
                destination[destinationByteOffset++] = (byte)(b | (second << secondPartLen));
            }
        }
        public static ushort ReadUInt16(byte[] fromBuffer, int numberOfBits, int readBitOffset)
        {
            ushort returnValue;
            if (numberOfBits <= 8)
            {
                returnValue = ReadByte(fromBuffer, numberOfBits, readBitOffset);
                return returnValue;
            }
            returnValue = ReadByte(fromBuffer, 8, readBitOffset);
            numberOfBits -= 8;
            readBitOffset += 8;
            if (numberOfBits <= 8)
                returnValue |= (ushort)(ReadByte(fromBuffer, numberOfBits, readBitOffset) << 8);
#if BIGENDIAN
			uint retVal = returnValue;
			retVal = ((retVal & 0x0000ff00) >> 8) | ((retVal & 0x000000ff) << 8);
			return (ushort)retVal;
#else
            return returnValue;
#endif
        }
        public static uint ReadUInt32(byte[] fromBuffer, int numberOfBits, int readBitOffset)
        {
            uint returnValue;
            if (numberOfBits <= 8)
            {
                returnValue = ReadByte(fromBuffer, numberOfBits, readBitOffset);
                return returnValue;
            }
            returnValue = ReadByte(fromBuffer, 8, readBitOffset);
            numberOfBits -= 8;
            readBitOffset += 8;
            if (numberOfBits <= 8)
            {
                returnValue |= (uint)(ReadByte(fromBuffer, numberOfBits, readBitOffset) << 8);
                return returnValue;
            }
            returnValue |= (uint)(ReadByte(fromBuffer, 8, readBitOffset) << 8);
            numberOfBits -= 8;
            readBitOffset += 8;
            if (numberOfBits <= 8)
            {
                uint r = ReadByte(fromBuffer, numberOfBits, readBitOffset);
                r <<= 16;
                returnValue |= r;
                return returnValue;
            }
            returnValue |= (uint)(ReadByte(fromBuffer, 8, readBitOffset) << 16);
            numberOfBits -= 8;
            readBitOffset += 8;
            returnValue |= (uint)(ReadByte(fromBuffer, numberOfBits, readBitOffset) << 24);
#if BIGENDIAN
			return ((returnValue & 0xff000000) >> 24) | ((returnValue & 0x00ff0000) >> 8) | ((returnValue & 0x0000ff00) << 8) | ((returnValue & 0x000000ff) << 24);
#else
            return returnValue;
#endif
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

        public byte[] Data => _data;

        public int LengthBits => _lengthBits;
        public int LengthBytes => (_lengthBits + 7) >> 3;
        public int ReadBits => _readBits;
        public bool EndOfData => ReadBits >= LengthBits;

        protected byte[] _data;
        protected int _lengthBits;
        protected int _readBits;

        public bool ReadBool()
        {
            var retval = ReadByte(_data, 1, _readBits);
            _readBits += 1;
            return (retval == 1);
        }

        public sbyte ReadSByte()
        {
            var retval = ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return (sbyte)retval;
        }
        public byte ReadByte()
        {
            var retval = ReadByte(_data, 8, _readBits);
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
            result = ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return true;
        }
        public byte ReadByte(int numberOfBits)
        {
            var retval = ReadByte(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public byte[] ReadBytes(int numberOfBytes)
        {
            var retval = new byte[numberOfBytes];
            ReadBytes(_data, numberOfBytes, _readBits, retval, 0);
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
            ReadBytes(_data, numberOfBytes, _readBits, result, 0);
            _readBits += (8 * numberOfBytes);
            return true;
        }
        public void ReadBytes(byte[] into, int offset, int numberOfBytes)
        {
            ReadBytes(_data, numberOfBytes, _readBits, into, offset);
            _readBits += (8 * numberOfBytes);
            return;
        }

        public short ReadShort()
        {
            var retval = ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (short)retval;
        }
        public ushort ReadUShort()
        {
            var retval = ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (ushort)retval;
        }

        public int ReadInt()
        {
            var retval = ReadUInt32(_data, 32, _readBits);
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
            result = (int)ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        public int ReadInt(int numberOfBits)
        {
            var retval = ReadUInt32(_data, numberOfBits, _readBits);
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
        public uint ReadUInt()
        {
            var retval = ReadUInt32(_data, 32, _readBits);
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
            result = ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        public uint ReadUInt(int numberOfBits)
        {
            var retval = ReadUInt32(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public int ReadRangedInt(int min, int max)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = ReadUInt(numBits);
            return (int)(min + rvalue);
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
        public float ReadRangedFloat(float min, float max, int numberOfBits)
        {
            var range = max - min;
            var maxVal = (1 << numberOfBits) - 1;
            var encodedVal = (float)ReadUInt(numberOfBits);
            var unit = encodedVal / maxVal;
            return min + (unit * range);
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
        public ulong ReadULong()
        {
            var low = ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            var high = ReadUInt32(_data, 32, _readBits);
            var retval = low + (high << 32);
            _readBits += 32;
            return retval;
        }
        public ulong ReadULong(int numberOfBits)
        {
            ulong retval;
            if (numberOfBits <= 32)
                retval = ReadUInt32(_data, numberOfBits, _readBits);
            else
            {
                retval = ReadUInt32(_data, 32, _readBits);
                retval |= (ulong)ReadUInt32(_data, numberOfBits - 32, _readBits + 32) << 32;
            }
            _readBits += numberOfBits;
            return retval;
        }
        public long ReadRangedLong(long min, long max)
        {
            var range = (ulong)(max - min);
            var numBits = BitsToHoldULong(range);
            var rvalue = ReadULong(numBits);
            return min + (long)rvalue;
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