using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace LiteNetLib
{
    public class NetPacket
    {
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
        public class ServerOnlySend : Attribute { }
        [AttributeUsage(AttributeTargets.Class)]
        public class ServerNoRelay : Attribute { }
        [AttributeUsage(AttributeTargets.Class)]
        public class InitialServerData : Attribute { }
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
        public class ClientOnlySend : Attribute { }
        [AttributeUsage(AttributeTargets.Field)]
        public class PeerID : Attribute { }
        [AttributeUsage(AttributeTargets.Field)]
        public class RangedInt : Attribute
        {
            public int Min,
                Max;

            public RangedInt(int min, int max)
            {
                Min = min;
                Max = max;
            }
        }

        public int SenderID { get; internal set; }

        public virtual void Process() { }

        internal static (Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads) GetProcessors(FieldInfo[] fields)
        {
            var writes = new List<Action<NetBitPackedDataWriter, object>>(fields.Length);
            var reads = new List<Action<NetBitPackedDataReader, object>>(fields.Length);
            (Action<NetBitPackedDataWriter, object> Write, Func<NetBitPackedDataReader, object> Read) GetRAW(Type t)
            {
                Func<NetBitPackedDataReader, object> read = null;
                Action<NetBitPackedDataWriter, object> write = null;
                if (t == typeof(bool))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((bool)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadBool();
                }
                else if (t == typeof(sbyte))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((sbyte)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadSByte();
                }
                else if (t == typeof(byte))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((byte)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadByte();
                }
                else if (t == typeof(short))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((short)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadShort();
                }
                else if (t == typeof(ushort))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((ushort)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadUShort();
                }
                else if (t == typeof(int))
                {
                    var b = (RangedInt)t.GetCustomAttribute(typeof(RangedInt));
                    if (b != null)
                    {
                        write = (NetBitPackedDataWriter writer, object obj) => writer.WriteRangedInt(b.Min, b.Max, (int)obj);
                        read = (NetBitPackedDataReader reader) => reader.ReadRangedInt(b.Min, b.Max);
                    }
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((int)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadInt();
                }
                else if (t == typeof(uint))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((uint)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadUInt();
                }
                else if (t == typeof(long))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((long)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadLong();
                }
                else if (t == typeof(ulong))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((ulong)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadULong();
                }
                else if (t == typeof(float))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((float)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadFloat();
                }
                else if (t == typeof(double))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.Write((double)obj);
                    read = (NetBitPackedDataReader reader) => reader.ReadDouble();
                }
                else if (t == typeof(Vector2))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => { writer.Write(((Vector2)obj).X); writer.Write(((Vector2)obj).Y); };
                    read = (NetBitPackedDataReader reader) => new Vector2(reader.ReadFloat(), reader.ReadFloat());
                }
                else if (t == typeof(Vector3))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => { writer.Write(((Vector3)obj).X); writer.Write(((Vector3)obj).Y); writer.Write(((Vector3)obj).Z); };
                    read = (NetBitPackedDataReader reader) => new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                }
                else if (t == typeof(Vector4))
                {
                    write = (NetBitPackedDataWriter writer, object obj) => { writer.Write(((Vector4)obj).X); writer.Write(((Vector4)obj).Y); writer.Write(((Vector4)obj).Z); writer.Write(((Vector4)obj).W); };
                    read = (NetBitPackedDataReader reader) => new Vector4(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                }
                else if (t.IsEnum)
                {
                    write = (NetBitPackedDataWriter writer, object obj) => writer.WriteRangedInt(Enum.GetValues(t).Cast<int>().Min(), Enum.GetValues(t).Cast<int>().Max(), (int)obj);
                    read = (NetBitPackedDataReader reader) => Convert.ChangeType(Enum.GetValues(t).GetValue(reader.ReadRangedInt(Enum.GetValues(t).Cast<int>().Min(), Enum.GetValues(t).Cast<int>().Max())), t);
                }
                return (write, read);
            }
            for (var i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                var bitPackedTMP = f.GetCustomAttribute(typeof(PeerID));
                if (bitPackedTMP != null)
                {
                    writes.Add((NetBitPackedDataWriter writer, object packet) => writer.WriteRangedInt(0, NetServer._maxPlayersIndex, (int)f.GetValue(packet)));
                    reads.Add((NetBitPackedDataReader reader, object packet) => f.SetValue(packet, reader.ReadRangedInt(0, NetServer._maxPlayersIndex)));
                    continue;
                }
                bool IsTuple(Type t)
                {
                    if (!t.IsGenericType)
                        return false;
                    var gT = t.GetGenericTypeDefinition();
                    return gT == typeof(ValueTuple<>) || gT == typeof(ValueTuple<,>) || gT == typeof(ValueTuple<,,>) || gT == typeof(ValueTuple<,,,>) || gT == typeof(ValueTuple<,,,,>) || gT == typeof(ValueTuple<,,,,,>) || gT == typeof(ValueTuple<,,,,,,>) || (gT == typeof(ValueTuple<,,,,,,,>) && IsTuple(t.GetGenericArguments()[7]));
                }
                if (f.FieldType.IsArray)
                {
                    var rank = f.FieldType.GetArrayRank();
                    var lengths = new int[rank];
                    var indices = new int[rank];
                    var t = f.FieldType.GetElementType();
                    if (t.IsClass || (t.IsValueType && !t.IsPrimitive && !t.IsEnum) || IsTuple(t))
                    {
                        var fields2 = t.GetFields().Where(x => x.IsPublic && !x.IsStatic).ToArray();
                        var (writes2, reads2) = GetProcessors(fields2);
                        var inst = Activator.CreateInstance(t);
                        writes.Add((NetBitPackedDataWriter writer, object o) =>
                        {
                            var arr = (Array)f.GetValue(o);
                            for (int j = 0; j < rank; j++)
                            {
                                writer.Write(lengths[j] = arr.GetLength(j));
                                indices[j] = 0;
                            }
                            void WriteArray(int index)
                            {
                                for (var j = 0; j < lengths[index]; j++)
                                {
                                    indices[index] = j;
                                    if (index == rank - 1)
                                        for (var k = 0; k < writes2.Length; k++)
                                            writes2[k](writer, arr.GetValue(indices));
                                    else
                                        WriteArray(index + 1);
                                }
                            }
                            WriteArray(0);
                        });
                        reads.Add((NetBitPackedDataReader reader, object o) =>
                        {
                            for (int j = 0; j < rank; j++)
                            {
                                lengths[j] = reader.ReadInt();
                                indices[j] = 0;
                            }
                            var arr = Array.CreateInstance(t, lengths);
                            void ReadArray(int index)
                            {
                                for (var j = 0; j < lengths[index]; j++)
                                {
                                    indices[index] = j;
                                    if (index == rank - 1)
                                        for (var k = 0; k < reads2.Length; k++)
                                        {
                                            reads2[k](reader, inst);
                                            arr.SetValue(inst, indices);
                                        }
                                    else
                                        ReadArray(index + 1);
                                }
                            }
                            ReadArray(0);
                            f.SetValue(o, arr);
                        });
                    }
                    else
                    {
                        var (w, r) = GetRAW(t);
                        writes.Add((NetBitPackedDataWriter writer, object o) =>
                        {
                            var arr = (Array)f.GetValue(o);
                            for (int j = 0; j < rank; j++)
                            {
                                writer.Write(lengths[j] = arr.GetLength(j));
                                indices[j] = 0;
                            }
                            void WriteArray(int index)
                            {
                                for (var j = 0; j < lengths[index]; j++)
                                {
                                    indices[index] = j;
                                    if (index == rank - 1)
                                        w(writer, arr.GetValue(indices));
                                    else
                                        WriteArray(index + 1);
                                }
                            }
                            WriteArray(0);
                        });
                        reads.Add((NetBitPackedDataReader reader, object o) =>
                        {
                            for (int j = 0; j < rank; j++)
                            {
                                lengths[j] = reader.ReadInt();
                                indices[j] = 0;
                            }
                            var arr = Array.CreateInstance(t, lengths);
                            void ReadArray(int index)
                            {
                                for (var j = 0; j < lengths[index]; j++)
                                {
                                    indices[index] = j;
                                    if (index == rank - 1)
                                        arr.SetValue(r(reader), indices);
                                    else
                                        ReadArray(index + 1);
                                }
                            }
                            ReadArray(0);
                            f.SetValue(o, arr);
                        });
                    }
                }
                if (f.FieldType.IsClass || (f.FieldType.IsValueType && !f.FieldType.IsPrimitive && !f.FieldType.IsEnum) || IsTuple(f.FieldType))
                {
                    if (f.FieldType == typeof(MemoryStream))
                    {
                        MemoryStream ms;
                        writes.Add((NetBitPackedDataWriter writer, object o) =>
                        {
                            ms = (MemoryStream)f.GetValue(o);
                            ms.Position = 0;
                            writer.Write((int)ms.Length);
                            for (var j = 0; j < ms.Length; j++)
                                writer.Write((byte)ms.ReadByte());
                        });
                        reads.Add((NetBitPackedDataReader reader, object o) => f.SetValue(o, new MemoryStream(reader.ReadBytes(reader.ReadInt()))));
                        continue;
                    }
                    if (f.FieldType == typeof(FileStream))
                    {
                        FileStream fs;
                        writes.Add((NetBitPackedDataWriter writer, object o) =>
                        {
                            fs = (FileStream)f.GetValue(o);
                            var bytes = new byte[fs.Length];
                            fs.Position = 0;
                            fs.Read(bytes, 0, bytes.Length);
                            var hash = new MD5CryptoServiceProvider().ComputeHash(bytes);
                            writer.Write(fs.Name.Replace($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\", ""));
                            writer.Write(hash.Length);
                            writer.Write(hash);
                            writer.Write(bytes.Length);
                            writer.Write(bytes);
                        });
                        reads.Add((NetBitPackedDataReader reader, object o) =>
                        {
                            string filePath = reader.ReadString(),
                                dir = $"{Path.GetDirectoryName(filePath)}\\",
                                fileName = Path.GetFileName(filePath);
                            var hash = reader.ReadBytes(reader.ReadInt());
                            checkAuth:
                            if (File.Exists(filePath))
                            {
                                var fs2 = File.OpenRead(filePath);
                                var hashBytes2 = new byte[fs2.Length];
                                fs2.Read(hashBytes2, 0, hashBytes2.Length);
                                var hash2 = new MD5CryptoServiceProvider().ComputeHash(hashBytes2);
                                if (hash.SequenceEqual(hash2))
                                {
                                    f.SetValue(o, fs2);
                                    reader.ReadBytes(reader.ReadInt());
                                    return;
                                }
                                fs2.Close();
                                fs2.Dispose();
                                fileName = $"_{fileName}";
                                filePath = $"{dir}\\{fileName}";
                                goto checkAuth;
                            }
                            var arr = reader.ReadBytes(reader.ReadInt());
                            fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                            fs.Write(arr, 0, arr.Length);
                            fs.Flush();
                            fs.Close();
                            fs.Dispose();
                            fs = File.OpenRead(filePath);
                            f.SetValue(o, fs);
                        });
                        continue;
                    }
                    if (f.FieldType == typeof(NetFile))
                    {
                        NetFile fs;
                        writes.Add((NetBitPackedDataWriter writer, object o) =>
                        {
                            fs = (NetFile)f.GetValue(o);
                            if (NetServer.IsRunning)
                            {
                                writer.WriteRangedInt(0, 2, (int)fs.State);
                                writer.Write(fs.Stream.Name.Replace($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\", ""));
                                if (fs.State == NetFile.States.AWAITING_HASH)
                                {
                                    writer.Write(fs.MD5Hash.Length);
                                    writer.Write(fs.MD5Hash);
                                }
                                else if (fs.State == NetFile.States.FILE_DIFFERENT)
                                {
                                    var bytes = new byte[fs.Stream.Length];
                                    fs.Stream.Position = 0;
                                    fs.Stream.Read(bytes, 0, bytes.Length);
                                    writer.Write(bytes.Length);
                                    writer.Write(bytes);
                                }
                                return;
                            }
                            if (fs == null)
                                writer.WriteRangedInt(0, 2, (int)NetFile.States.FILE_DIFFERENT);
                            else
                                writer.WriteRangedInt(0, 2, (int)fs.State);
                        });
                        reads.Add((NetBitPackedDataReader reader, object o) =>
                        {
                            fs = (NetFile)f.GetValue(o);
                            var state = (NetFile.States)reader.ReadRangedInt(0, 2);
                            if (NetServer.IsRunning)
                            {
                                fs.State = state;
                                return;
                            }
                            string filePath = reader.ReadString(),
                                dir = $"{Path.GetDirectoryName(filePath)}\\",
                                fileName = Path.GetFileName(filePath);
                            if (state == NetFile.States.AWAITING_HASH)
                            {
                                var hash = reader.ReadBytes(reader.ReadInt());
                                checkAuth:
                                if (File.Exists(filePath))
                                {
                                    var fs2 = File.OpenRead(filePath);
                                    var hashBytes2 = new byte[fs2.Length];
                                    fs2.Read(hashBytes2, 0, hashBytes2.Length);
                                    var hash2 = new MD5CryptoServiceProvider().ComputeHash(hashBytes2);
                                    if (hash.SequenceEqual(hash2))
                                    {
                                        f.SetValue(o, new NetFile(fs2) { State = NetFile.States.IDENTICAL_FILE });
                                        return;
                                    }
                                    fs2.Close();
                                    fs2.Dispose();
                                    fileName = $"_{fileName}";
                                    filePath = $"{dir}\\{fileName}";
                                    goto checkAuth;
                                }
                            }
                            else if (state == NetFile.States.FILE_DIFFERENT)
                            {
                                checkAuth2:
                                if (File.Exists(filePath))
                                {
                                    fileName = $"_{fileName}";
                                    filePath = $"{dir}\\{fileName}";
                                    goto checkAuth2;
                                }
                                var arr = reader.ReadBytes(reader.ReadInt());
                                var fs2 = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                                fs2.Write(arr, 0, arr.Length);
                                fs2.Flush();
                                fs2.Close();
                                fs2.Dispose();
                                fs = new NetFile(File.OpenRead(filePath));
                                f.SetValue(o, fs);
                            }
                        });
                        continue;
                    }
                    var fields2 = f.FieldType.GetFields().Where(x => x.IsPublic && !x.IsStatic).ToArray();
                    var (writes2, reads2) = GetProcessors(fields2);
                    for (var j = 0; j < fields2.Length; j++)
                    {
                        var (w, r) = (writes2[j], reads2[j]);
                        writes.Add((NetBitPackedDataWriter writer, object o) => w(writer, f.GetValue(o)));
                        reads.Add((NetBitPackedDataReader reader, object o) => r(reader, f.GetValue(o)));
                    }
                }
                else
                {
                    var (w, r) = GetRAW(f.FieldType);
                    writes.Add((NetBitPackedDataWriter writer, object o) => w(writer, f.GetValue(o)));
                    reads.Add((NetBitPackedDataReader reader, object o) => f.SetValue(o, r(reader)));
                }
            }
            return (writes.ToArray(), reads.ToArray());
        }
    }
}