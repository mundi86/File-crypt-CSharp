using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test_output
{
    class Program
    {
        static void Main(string[] args)
        {

            test xxx = new test();

            crytec.PolyAES x = new crytec.PolyAES();

            String testjpg = @"C:\Users\mundi\Desktop\fuck10.mp4";
            // byte[] b = System.IO.File.ReadAllBytes(testjpg);
            // byte[] b = xxx.ReadFileContents(testjpg);
            byte[] data;
            using (StreamReader sr = new StreamReader(testjpg))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    sr.BaseStream.CopyTo(ms);
                    data = ms.ToArray();
                }
            }

            System.IO.File.WriteAllBytes(@"C:\Users\mundi\Desktop\c.mp4", x.PolyAES256Encrypt(data, "test"));
          // System.IO.File.WriteAllText("x.txt", Convert.ToBase64String(System.IO.File.ReadAllBytes("c.mp4")));

           // // compress
           //FileInfo myfile = new FileInfo("x.txt");
           //Compress(myfile);

           //// decompress
           //FileInfo myfile1 = new FileInfo("x.txt.gz");
           //Decompress(myfile1);
           
           //System.IO.File.WriteAllBytes("c1.jpg", Convert.FromBase64String(System.IO.File.ReadAllText("x.txt")));
           // byte[] b1 = System.IO.File.ReadAllBytes("c1.jpg");
           // System.IO.File.WriteAllBytes("en.jpg", x.PolyAES256Decrypt(b1, "test"));


        }

        public static void Compress(FileInfo fileToCompress)
        {
            using (FileStream originalFileStream = fileToCompress.OpenRead())
            {
                if ((File.GetAttributes(fileToCompress.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & fileToCompress.Extension != ".gz")
                {
                    using (FileStream compressedFileStream = File.Create(fileToCompress.FullName + ".gz"))
                    {
                        using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
                        {
                            originalFileStream.CopyTo(compressionStream);
                            Console.WriteLine("Compressed {0} from {1} to {2} bytes.",
                                fileToCompress.Name, fileToCompress.Length.ToString(), compressedFileStream.Length.ToString());
                        }
                    }
                }
            }
        }

        public static void Decompress(FileInfo fileToDecompress)
        {
            using (FileStream originalFileStream = fileToDecompress.OpenRead())
            {
                string currentFileName = fileToDecompress.FullName;
                string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);

                using (FileStream decompressedFileStream = File.Create(newFileName))
                {
                    using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedFileStream);
                        Console.WriteLine("Decompressed: {0}", fileToDecompress.Name);
                    }
                }
            }
        }


       
    }
}
