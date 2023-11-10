using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class Helper
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);
        public static void RemoveOnBoot(string filePath) => MoveFileEx(filePath, null, 0x4);
        public static bool IsLink(string data) => data.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && data.Contains("://");
        public static bool OnInstallPath => Program.PathsEquals(Program.currentAssembly.Location, Program.installPath);
        public static string tempFilesPath => Path.Combine(Path.GetTempPath(), Application.ProductName);
        public static string DownloadFile(string uri, string ext)
        {
            using (MD5 hashAlg = MD5.Create())
            {
                //Console.WriteLine("Down " + uri + " " + ext);

                if (!Directory.Exists(tempFilesPath))
                {
                    Directory.CreateDirectory(tempFilesPath);
                    RemoveOnBoot(tempFilesPath);
                }

                string filePath = Path.Combine(tempFilesPath, BitConverter.ToString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(uri + ext)))).Replace("-","") + ext;

                if (!File.Exists(filePath))
                {
                    File.WriteAllBytes(filePath, Program.wc.DownloadData(uri));

                    RemoveOnBoot(filePath);
                }

                return filePath;
            }
        }

        public static string GetTempFilePath(string path, string ext)
        {
            string tarjetFilePath = null;
            while (tarjetFilePath == null || File.Exists(tarjetFilePath)) tarjetFilePath = Path.Combine(path, Path.GetRandomFileName() + ext);
            return tarjetFilePath;
        }
    }
}
