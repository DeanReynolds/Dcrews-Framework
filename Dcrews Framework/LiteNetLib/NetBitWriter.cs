using System;

namespace Dcrew.Framework.LiteNetLib
{
    public static class NetBitWriter
    {
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

        [CLSCompliant(false)]
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

        [CLSCompliant(false)]
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

        [CLSCompliant(false)]
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
    }
}