using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test_output
{
    class test
    {
        private const int defaultBlockSize = 81920;
        public virtual byte[] ReadFileContents(string filePath)
        {
            return InternalReadFileContent(filePath, defaultBlockSize);
        }

        private byte[] ReadFileContents(string filePath, int blockSize)
        {
            return InternalReadFileContent(filePath, blockSize);
        }

        public virtual byte[] InternalReadFileContent(string filePath, int blockSize)
        {
            if (!File.Exists(filePath)) { return null; }

            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                byte[] content = new byte[fs.Length];
                using (MemoryStream ms = new MemoryStream(content))
                {
                    fs.CopyTo(ms, blockSize);
                    return content;
                }
            }
        }


    }
}
