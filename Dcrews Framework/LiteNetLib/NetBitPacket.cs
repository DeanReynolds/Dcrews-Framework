using LiteNetLib.Utils;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Dcrew.Framework.LiteNetLib
{
    public class NetBitPacket
    {
        const int c_bufferSize = 64;
        const int c_overAllocateAmount = 4;
        
        static object s_buffer;

        [CLSCompliant(false)]
        public static int BitsToHoldUInt(uint value)
        {
            int bits = 1;
            while ((value >>= 1) != 0)
                bits++;
            return bits;
        }
        [CLSCompliant(false)]
        public static int BitsToHoldULong(ulong value)
        {
            int bits = 1;
            while ((value >>= 1) != 0)
                bits++;
            return bits;
        }

        static uint SwapByteOrder(uint value) => ((value & 0xff000000) >> 24) | ((value & 0x00ff0000) >> 8) | ((value & 0x0000ff00) << 8) | ((value & 0x000000ff) << 24);
        static ulong SwapByteOrder(ulong value) => ((value & 0xff00000000000000L) >> 56) | ((value & 0x00ff000000000000L) >> 40) | ((value & 0x0000ff0000000000L) >> 24) | ((value & 0x000000ff00000000L) >> 8) | ((value & 0x00000000ff000000L) << 8) | ((value & 0x0000000000ff0000L) << 24) | ((value & 0x000000000000ff00L) << 40) | ((value & 0x00000000000000ffL) << 56);

        public NetBitPacket()
        {
            EnsureSize(_lengthBits = 3);
        }

        public NetBitPacket(NetDataReader dataReader)
        {
            _data = new byte[dataReader.UserDataSize];
            Array.Copy(dataReader.RawData, dataReader.UserDataOffset, _data, 0, dataReader.UserDataSize);
            _lengthBits = ((dataReader.UserDataSize * 8) - NetBitWriter.ReadByte(_data, (_readBits = 3), 0));
        }

        public byte[] Data
        {
            get
            {
                var data = new byte[LengthBytes];
                NetBitWriter.WriteByte((byte)((LengthBytes * 8) - _lengthBits), 3, _data, 0);
                Array.Copy(_data, data, LengthBytes);
                return data;
            }
        }

        public int LengthBits => (_lengthBits - 3);
        public int LengthBytes => ((_lengthBits + 7) >> 3);
        public int ReadBits => (_readBits - 3);
        public bool EndOfData => (ReadBits >= LengthBits);

        byte[] _data;
        int _lengthBits;
        int _readBits;

        public void Put(bool value)
        {
            EnsureSize(_lengthBits + 1);
            NetBitWriter.WriteByte((value ? (byte)1 : (byte)0), 1, _data, _lengthBits);
            _lengthBits += 1;
        }
        public bool GetBool()
        {
            var retval = NetBitWriter.ReadByte(_data, 1, _readBits);
            _readBits += 1;
            return (retval == 1);
        }

        [CLSCompliant(false)]
        public void Put(sbyte source)
        {
            EnsureSize(_lengthBits + 8);
            NetBitWriter.WriteByte((byte)source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        [CLSCompliant(false)]
        public sbyte GetSByte()
        {
            var retval = NetBitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return (sbyte)retval;
        }
        public void Put(byte source)
        {
            EnsureSize(_lengthBits + 8);
            NetBitWriter.WriteByte(source, 8, _data, _lengthBits);
            _lengthBits += 8;
        }
        public byte GetByte()
        {
            var retval = NetBitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return retval;
        }
        public bool GetByte(out byte result)
        {
            if (_lengthBits - _readBits < 8)
            {
                result = 0;
                return false;
            }
            result = NetBitWriter.ReadByte(_data, 8, _readBits);
            _readBits += 8;
            return true;
        }
        public byte GetByte(int numberOfBits)
        {
            byte retval = NetBitWriter.ReadByte(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public void Put(byte[] source)
        {
            int bits = source.Length * 8;
            EnsureSize(_lengthBits + bits);
            NetBitWriter.WriteBytes(source, 0, source.Length, _data, _lengthBits);
            _lengthBits += bits;
        }
        public byte[] GetBytes(int numberOfBytes)
        {
            byte[] retval = new byte[numberOfBytes];
            NetBitWriter.ReadBytes(_data, numberOfBytes, _readBits, retval, 0);
            _readBits += (8 * numberOfBytes);
            return retval;
        }
        public bool GetBytes(int numberOfBytes, out byte[] result)
        {
            if (_lengthBits - _readBits + 7 < (numberOfBytes * 8))
            {
                result = null;
                return false;
            }
            result = new byte[numberOfBytes];
            NetBitWriter.ReadBytes(_data, numberOfBytes, _readBits, result, 0);
            _readBits += (8 * numberOfBytes);
            return true;
        }
        public void GetBytes(byte[] into, int offset, int numberOfBytes)
        {
            NetBitWriter.ReadBytes(_data, numberOfBytes, _readBits, into, offset);
            _readBits += (8 * numberOfBytes);
            return;
        }

        public void Put(short source)
        {
            EnsureSize(_lengthBits + 16);
            NetBitWriter.WriteUInt16((ushort)source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }
        public short GetShort()
        {
            var retval = NetBitWriter.ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (short)retval;
        }
        [CLSCompliant(false)]
        public void Put(ushort source)
        {
            EnsureSize(_lengthBits + 16);
            NetBitWriter.WriteUInt16(source, 16, _data, _lengthBits);
            _lengthBits += 16;
        }
        [CLSCompliant(false)]
        public ushort GetUShort()
        {
            var retval = NetBitWriter.ReadUInt16(_data, 16, _readBits);
            _readBits += 16;
            return (ushort)retval;
        }

        public void Put(int source)
        {
            EnsureSize(_lengthBits + 32);
            NetBitWriter.WriteUInt32((uint)source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        [CLSCompliant(false)]
        public int GetInt()
        {
            uint retval = NetBitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return (int)retval;
        }
        [CLSCompliant(false)]
        public bool GetInt(out int result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0;
                return false;
            }
            result = (int)NetBitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        public int GetInt(int numberOfBits)
        {
            var retval = NetBitWriter.ReadUInt32(_data, numberOfBits, _readBits);
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
        public void Put(uint source)
        {
            EnsureSize(_lengthBits + 32);
            NetBitWriter.WriteUInt32(source, 32, _data, _lengthBits);
            _lengthBits += 32;
        }
        [CLSCompliant(false)]
        public void Put(uint source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            NetBitWriter.WriteUInt32(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        [CLSCompliant(false)]
        public uint GetUInt()
        {
            var retval = NetBitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return retval;
        }
        [CLSCompliant(false)]
        public bool GetUInt(out uint result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0;
                return false;
            }
            result = NetBitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            return true;
        }
        [CLSCompliant(false)]
        public uint GetUInt(int numberOfBits)
        {
            var retval = NetBitWriter.ReadUInt32(_data, numberOfBits, _readBits);
            _readBits += numberOfBits;
            return retval;
        }
        public int PutRangedInt(int min, int max, int value)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = (uint)(value - min);
            Put(rvalue, numBits);
            return numBits;
        }
        public int GetRangedInt(int min, int max)
        {
            var range = (uint)(max - min);
            var numBits = BitsToHoldUInt(range);
            var rvalue = GetUInt(numBits);
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
        public float GetFloat()
        {
            if ((_readBits & 7) == 0)
            {
                var retval = BitConverter.ToSingle(_data, _readBits >> 3);
                _readBits += 32;
                return retval;
            }
            var bytes = (byte[])Interlocked.Exchange(ref s_buffer, null) ?? new byte[c_bufferSize];
            GetBytes(bytes, 0, 4);
            var res = BitConverter.ToSingle(bytes, 0);
            s_buffer = bytes;
            return res;
        }
        public bool GetFloat(out float result)
        {
            if (_lengthBits - _readBits < 32)
            {
                result = 0.0f;
                return false;
            }
            if ((_readBits & 7) == 0)
            {
                result = BitConverter.ToSingle(_data, _readBits >> 3);
                _readBits += 32;
                return true;
            }
            var bytes = (byte[])Interlocked.Exchange(ref s_buffer, null) ?? new byte[c_bufferSize];
            GetBytes(bytes, 0, 4);
            result = BitConverter.ToSingle(bytes, 0);
            s_buffer = bytes;
            return true;
        }
        public void PutRangedFloat(float value, float min, float max, int numberOfBits)
        {
            float range = max - min;
            float unit = ((value - min) / range);
            int maxVal = (1 << numberOfBits) - 1;
            Put((uint)((float)maxVal * unit), numberOfBits);
        }
        public float GetRangedFloat(float min, float max, int numberOfBits)
        {
            float range = max - min;
            int maxVal = (1 << numberOfBits) - 1;
            float encodedVal = (float)GetUInt(numberOfBits);
            float unit = encodedVal / (float)maxVal;
            return min + (unit * range);
        }

        public void Put(long source)
        {
            EnsureSize(_lengthBits + 64);
            var usource = (ulong)source;
            NetBitWriter.WriteUInt64(usource, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        public long GetLong()
        {
            unchecked
            {
                ulong retval = GetULong();
                long longRetval = (long)retval;
                return longRetval;
            }
        }
        public long GetLong(int numberOfBits) => (long)GetULong(numberOfBits);
        [CLSCompliant(false)]
        public void Put(ulong source)
        {
            EnsureSize(_lengthBits + 64);
            NetBitWriter.WriteUInt64(source, 64, _data, _lengthBits);
            _lengthBits += 64;
        }
        [CLSCompliant(false)]
        public void Put(ulong source, int numberOfBits)
        {
            EnsureSize(_lengthBits + numberOfBits);
            NetBitWriter.WriteUInt64(source, numberOfBits, _data, _lengthBits);
            _lengthBits += numberOfBits;
        }
        [CLSCompliant(false)]
        public ulong GetULong()
        {
            var low = NetBitWriter.ReadUInt32(_data, 32, _readBits);
            _readBits += 32;
            var high = NetBitWriter.ReadUInt32(_data, 32, _readBits);
            var retval = low + (high << 32);
            _readBits += 32;
            return retval;
        }
        [CLSCompliant(false)]
        public ulong GetULong(int numberOfBits)
        {
            ulong retval;
            if (numberOfBits <= 32)
                retval = NetBitWriter.ReadUInt32(_data, numberOfBits, _readBits);
            else
            {
                retval = NetBitWriter.ReadUInt32(_data, 32, _readBits);
                retval |= (ulong)NetBitWriter.ReadUInt32(_data, numberOfBits - 32, _readBits + 32) << 32;
            }
            _readBits += numberOfBits;
            return retval;
        }
        public int PutRangedLong(long min, long max, long value)
        {
            ulong range = (ulong)(max - min);
            int numBits = BitsToHoldULong(range);
            ulong rvalue = (ulong)(value - min);
            Put(rvalue, numBits);
            return numBits;
        }
        public long GetRangedLong(long min, long max)
        {
            ulong range = (ulong)(max - min);
            int numBits = BitsToHoldULong(range);
            ulong rvalue = GetULong(numBits);
            return min + (long)rvalue;
        }

        public void Put(double source)
        {
            byte[] val = BitConverter.GetBytes(source);
            Put(val);
        }
        public double GetDouble()
        {
            if ((_readBits & 7) == 0)
            {
                var retval = BitConverter.ToDouble(_data, _readBits >> 3);
                _readBits += 64;
                return retval;
            }
            var bytes = (byte[])Interlocked.Exchange(ref s_buffer, null) ?? new byte[c_bufferSize];
            GetBytes(bytes, 0, 8);
            var res = BitConverter.ToDouble(bytes, 0);
            s_buffer = bytes;
            return res;
        }

        void EnsureSize(int bits)
        {
            var byteLen = ((bits + 7) >> 3);
            if (_data == null)
                _data = new byte[byteLen + c_overAllocateAmount];
            else if (_data.Length < byteLen)
                Array.Resize(ref _data, byteLen + c_overAllocateAmount);
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct SingleUIntUnion
        {
            [FieldOffset(0)]
            public float SingleValue;

            [FieldOffset(0)]
            [CLSCompliant(false)]
            public uint UIntValue;
        }
    }
}