using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MosaicGallery
{
    public class PngMetadataReader
    {
        public static string ReadMetadata(string filename)
        {
            using (var fs = File.OpenRead(filename))
            {
                byte[] header = new byte[8];
                fs.Read(header, 0, 8);
                if (!header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                {
                    return null;
                }

                using (var br = new BinaryReader(fs, Encoding.ASCII, true))
                {
                    while (fs.Position != fs.Length)
                    {
                        uint chunkLength = br.ReadUInt32BE();
                        string chunkType = new string(br.ReadChars(4));
                        if (chunkType == "tEXt")
                        {
                            var bytes = br.ReadBytes((int)chunkLength);
                            var strings = Encoding.UTF8.GetString(bytes).Split('\0');
                            return strings.Last();
                        }
                        else if (chunkType == "IHDR")
                        {
                            fs.Position += chunkLength;
                            br.ReadUInt32BE();
                        }
                        else
                        {
                            return null;
                        }
                    }
                }

            }

            return null;
        }
    }

    static class BinaryReaderExtensionMethods
    {
        static public UInt16 ReadUInt16BE(this BinaryReader br)
        {
            return (UInt16)IPAddress.NetworkToHostOrder(br.ReadInt16());
        }
        static public UInt32 ReadUInt32BE(this BinaryReader br)
        {
            return (UInt32)IPAddress.NetworkToHostOrder(br.ReadInt32());
        }
    }
}
