using System.IO;
using System.Security.Cryptography;

namespace LiteNetLib
{
    public class NetFile
    {
        public States State { get; internal set; }

        public readonly FileStream Stream;
        public readonly byte[] MD5Hash;

        public enum States { AWAITING_HASH, IDENTICAL_FILE, FILE_DIFFERENT }

        public NetFile(FileStream fileStream)
        {
            Stream = fileStream;
            var bytes = new byte[fileStream.Length];
            fileStream.Position = 0;
            fileStream.Read(bytes, 0, bytes.Length);
            MD5Hash = new MD5CryptoServiceProvider().ComputeHash(bytes);
        }
    }
}