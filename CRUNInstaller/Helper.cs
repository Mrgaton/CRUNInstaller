using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CRUNInstaller
{
    internal class Helper
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        public static void RemoveOnBoot(string filePath) => MoveFileEx(filePath, null, 0x4);

        public static bool IsLink(string data) => data.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && data.Contains("://");

        public static bool PathsEquals(string path1, string path2) => StringComparer.InvariantCultureIgnoreCase.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2));

        public static bool OnInstallPath => PathsEquals(Program.currentAssembly.Location, Program.installPath);

        public static string GetHeaderValue(WebHeaderCollection headers, string headerName)
        {
            foreach (string key in headers.AllKeys)
            {
                if (string.Equals(key, headerName, StringComparison.OrdinalIgnoreCase)) return headers[key];
            }

            return null;
        }

        public static MD5 hashAlg = MD5.Create();

        public static string DownloadFile(string uri, string ext)
        {
            string tempFilesPath = Directory.GetCurrentDirectory();

            byte[] data = Program.wc.DownloadData(uri);

            uri += GetHeaderValue(Program.wc.ResponseHeaders, "ETag") ?? "";

            if (!Directory.Exists(tempFilesPath))
            {
                Directory.CreateDirectory(tempFilesPath);
                RemoveOnBoot(tempFilesPath);
            }

            string filePath = Path.Combine(tempFilesPath, BitConverter.ToString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(uri.Split('?')[0] + ext)))).Replace("-", "") + ext;

            if (!File.Exists(filePath))
            {
                File.WriteAllBytes(filePath, data);
                RemoveOnBoot(filePath);
            }

            return filePath;
        }

        public static string ToSafeBase64(byte[] b) => Convert.ToBase64String(b).Replace('/', '-').Trim('=');

        public static string GetTempFilePath(string path, string ext)
        {
            string tarjetFilePath = null;
            while (tarjetFilePath == null || File.Exists(tarjetFilePath)) tarjetFilePath = Path.Combine(path, Path.GetRandomFileName() + ext);
            return tarjetFilePath;
        }
    }
}