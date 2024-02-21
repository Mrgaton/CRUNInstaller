using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CRUNInstaller
{
    internal static class Helper
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

        public static void DownloadFiles(string[] filesUris = null)
        {
            string tempFilesPath = Directory.GetCurrentDirectory();

            foreach (var file in filesUris)
            {
                var splited = file.Split('^');

                string fname = splited.Last();
                string url = splited[0];

                string path = Path.Combine(tempFilesPath, splited.Length > 1 ? fname : file.Split('/').Last());

                if (File.Exists(path)) continue;

                File.WriteAllBytes(path, Program.wc.DownloadData(url));
            }
        }

        public static string DownloadFile(string uri, string ext)
        {
            string tempFilesPath = Directory.GetCurrentDirectory();

            string fileName = null;

            if (uri.Contains('^'))
            {
                var splitedInfo = uri.Split('^');
                fileName = splitedInfo.Last();
                uri = splitedInfo[0];
            }

            byte[] data = Program.wc.DownloadData(uri);

            uri += GetHeaderValue(Program.wc.ResponseHeaders, "ETag") ?? "";

            if (!Directory.Exists(tempFilesPath))
            {
                Directory.CreateDirectory(tempFilesPath);
                RemoveOnBoot(tempFilesPath);
            }

            string filePath;

            if (fileName != null)
            {
                filePath = Path.Combine(tempFilesPath, fileName);
            }
            else
            {
                filePath = Path.Combine(tempFilesPath, ToHex(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(uri.Split('?')[0] + ext)))) + ext;
            }

            if (!File.Exists(filePath))
            {
                File.WriteAllBytes(filePath, data);
                RemoveOnBoot(filePath);
            }

            return filePath;
        }

        //private static char GetHexLoweredValue(int i) => (i < 10) ? ((char)(i + 48)) : ((char)(i - 10 + 97));
        private static char GetHexValue(int i) => (i < 10) ? ((char)(i + 48)) : ((char)(i - 10 + 65));

        public static string ToHex(byte[] value)
        {
            int l = value.Length * 2;
            char[] array = new char[l];
            int i, di = 0;

            for (i = 0; i < l; i += 2)
            {
                byte b = value[di++];
                array[i] = GetHexValue(b / 16);
                array[i + 1] = GetHexValue(b % 16);
            }

            return new string(array);
        }

        /*public static int GetHexVal(int val) => val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        public static int GetUpperedHexVal(int val) => val - (val < 58 ? 48 : 55);
        public static int GetLoweredHexVal(int val) => val - (val < 58 ? 48 : 87);
        public static byte[] FromHex(string hex)
        {
            if (hex.Length % 2 == 1) throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }*/

        public static string ToSafeBase64(byte[] b) => Convert.ToBase64String(b).Replace('/', '-').Trim('=');

        public static string GetTempFilePath(string path, string ext)
        {
            string tarjetFilePath = null;
            while (tarjetFilePath == null || File.Exists(tarjetFilePath)) tarjetFilePath = Path.Combine(path, Path.GetRandomFileName() + ext);
            return tarjetFilePath;
        }

        public static bool ConsoleAtached()
        {
            try
            {
                if (Console.CursorLeft > int.MinValue) return true;
            }
            catch { }

            return false;
        }
    }
}