using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace CRUNInstaller
{
    internal static class Helper
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        public static bool RemoveFilesOnBoot = true;

        public static void RemoveOnBoot(string filePath)
        {
            if (RemoveFilesOnBoot) MoveFileEx(filePath, null, 0x4);
        }

        public static bool IsLink(string data) => data != null && !string.IsNullOrEmpty(data) && data.TrimStart('!').StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && data.Contains("://");

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

        public static SHA384 hashAlg = SHA384.Create();

        public static char fileNameCharSeparator = '^';

        public static void DownloadZip(string zipFileUrl)
        {
            string tempFilesPath = Directory.GetCurrentDirectory();

            string[] urlSplited = zipFileUrl.Split(fileNameCharSeparator);
            string folder = urlSplited.Length > 1 ? urlSplited[1] : zipFileUrl.Hash();

            string combinedFolder = Path.Combine(tempFilesPath, folder);
            string hashFileName = Path.Combine(combinedFolder, zipFileUrl.Hash() + ".db");

            if (zipFileUrl[0] != '!' && File.Exists(hashFileName)) return;
            else if (!Directory.Exists(combinedFolder)) Directory.CreateDirectory(combinedFolder);

            UnzipFromMemory(Program.client.GetStreamAsync(urlSplited[0].TrimStart('!')).Result, string.IsNullOrEmpty(folder) ? tempFilesPath : combinedFolder);

            RemoveOnBoot(combinedFolder);

            if (File.Exists(hashFileName))
            {
                File.Create(hashFileName);
                File.SetAttributes(hashFileName, FileAttributes.System | FileAttributes.Hidden);

                RemoveOnBoot(hashFileName);
            }

            Directory.SetCurrentDirectory(combinedFolder);
        }

        public static void DownloadFiles(string[] filesUris = null)
        {
            string tempFilesPath = Directory.GetCurrentDirectory();

            foreach (var file in filesUris)
            {
                var splited = file.Split(fileNameCharSeparator);

                string url = splited[0];
                string fileName = splited.Length > 0 ? splited[1] : null;

                string path = Path.Combine(tempFilesPath, fileName != null ? fileName.Replace('/', '\\') : file.Split('/').Last());

                if (file[0] != '!' && File.Exists(path)) continue;

                using (FileStream fs = File.OpenWrite(path))
                {
                    fs.SetLength(0);

                    using (Stream ns = Program.client.GetStreamAsync(url.TrimStart('!')).Result)
                    {
                        ns.CopyTo(fs);
                    }
                }

                //File.WriteAllBytes(path, Program.wc.DownloadData(url));
            }
        }

        public static string DownloadFile(string url, string ext = null)
        {
            if (ext == null) 
                ext = Path.GetExtension(url.Split('/').Last().Split('?')[0]);

            string tempFilesPath = Directory.GetCurrentDirectory();

            string fileName = null;

            if (url.Contains(fileNameCharSeparator))
            {
                var splitedInfo = url.Split(fileNameCharSeparator);
                url = splitedInfo[0];
                fileName = splitedInfo[1];
            }

            string filePath = fileName != null ? Path.Combine(tempFilesPath, fileName.Replace('/', '\\')) : Path.Combine(tempFilesPath, (url.Split('?')[0] + ext).Hash()) + (string.IsNullOrEmpty(ext) ? ".exe" : ext);

            if (url[0] == '!' || !File.Exists(filePath))
            {
                using (FileStream fs = File.OpenWrite(filePath))
                using (var request = new HttpRequestMessage(HttpMethod.Get, url.TrimStart('!')))
                {
                    using (var response = Program.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        if (!response.IsSuccessStatusCode) 
                            throw new Exception("Could not download file: " + response.StatusCode + " " + response.ReasonPhrase);

                        using (var ns = response.Content.ReadAsStreamAsync().Result)
                        {
                            ns.CopyTo(fs);
                        }
                    }
                }


                RemoveOnBoot(filePath);
            }

            return filePath;
        }

        //private static char GetHexLoweredValue(int i) => (i < 10) ? ((char)(i + 48)) : ((char)(i - 10 + 97));
        private static char GetHexValue(int i) => (i < 10) ? ((char)(i + 48)) : ((char)(i - 10 + 65));

        public static string Hash(this string str) => Base64Url.ToBase64Url(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(str)));

        public static string ToHex(byte[] value)
        {
            char[] array = new char[value.Length * 2];

            int i, di = 0;

            for (i = 0; i < array.Length; i += 2)
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

        public static void UnzipFromMemory(Stream zipStream, string outputPath)
        {
            using (var archive = new ZipArchive(zipStream))
            {
                foreach (var entry in archive.Entries)
                {
                    var entryPath = Path.Combine(outputPath, entry.FullName.Replace('/', '\\'));

                    if (entry.FullName[entry.FullName.Length - 1] == '/')
                    {
                        if (!Directory.Exists(entryPath)) Directory.CreateDirectory(entryPath);

                        continue;
                    }

                    entry.ExtractToFile(entryPath, overwrite: true);
                }
            }
        }
        public static void KillClonedInstances()
        {
            Process currentProcess = Process.GetCurrentProcess();

            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Program.currentAssembly.Location)))
            {
                if (p.Id != currentProcess.Id)
                {
                    p.Kill();
                }
            }
        }
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

        public static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void EnsureElevated()
        {
            if (IsAdministrator())
                return;
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
                Verb = "runas",
                UseShellExecute = true
            };

            Process.Start(processStartInfo);

            Environment.Exit(0);
        }
        public static RegistryHive ParseHive(string hive)
        {
            switch (hive.Replace("_", "").ToUpper())
            {
                case "HKLM":
                case "HKEYLOCALMACHINE":
                    return RegistryHive.LocalMachine;
                case "HKCU":
                case "HKEYCURRENTUSER":
                    return RegistryHive.CurrentUser;
                case "HKCR":
                case "HKEYCLASSESROOT":
                    return RegistryHive.ClassesRoot;
                case "HKU":
                case "HKEYUSERS":
                    return RegistryHive.Users;
                case "HKCC":
                case "HKEYCURRENTCONFIG":
                    return RegistryHive.CurrentConfig;
                default:
                    throw new ArgumentException($"Unknown hive: {hive}");
            }
        }
        public static class Base64Url
        {
            public static string ToBase64Url(byte[] data) => Convert.ToBase64String(data).Trim('=').Replace('+', '-').Replace('/', '_');

            public static byte[] FromBase64Url(string data) => Convert.FromBase64String(data.Replace('_', '/').Replace('-', '+').PadRight(data.Length + (4 - data.Length % 4) % 4, '='));
        }
    }
}