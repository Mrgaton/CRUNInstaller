using System.IO;
using System.Runtime.InteropServices;

namespace CRUNInstaller
{
    internal class Helper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        public static void RemoveFileOnBoot(string filePath) => MoveFileEx(filePath, null, 0x4);
        public static bool OnInstallPath => Program.PathsEquals(Program.currentAssembly.Location, Program.installPath);
        public static string DownloadFile(string uri, string ext)
        {
            string filePath = GetTempFilePath(ext);
            File.WriteAllBytes(filePath, Program.wc.DownloadData(uri));
            RemoveFileOnBoot(filePath);
            return filePath;
        }

        public static string GetTempFilePath(string ext)
        {
            string tarjetFilePath = null;
            while (tarjetFilePath == null || File.Exists(tarjetFilePath)) tarjetFilePath = Path.GetTempFileName() + ext;
            return tarjetFilePath;
        }
    }
}
