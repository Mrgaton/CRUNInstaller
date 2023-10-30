using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class ArgsProcessor
    {
        private static WebClient wc = new WebClient();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        public static void RemoveFileOnBoot(string FilePath) => MoveFileEx(FilePath, null, 0x4);

        public static void ProcessArguments(string[] args)
        {
            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            bool showWindow = true;
            bool shellExecute = true;

            string executePath = null;

            MessageBox.Show("\"" + lowered[0] + "\"");

            switch (lowered[0])
            {
                case "help":
                    Commands.Help.ShowHelp();
                    break;

                case "uninstall":
                    Commands.Uninstaller.Uninstall();
                    break;

                case "run":
                    showWindow = bool.Parse(lowered[1]);
                    shellExecute = bool.Parse(lowered[2]);

                    CustomRun(args[3], args[4], showWindow, shellExecute);
                    break;

                case "cmd":
                    showWindow = bool.Parse(lowered[1]);
                    bool autoClose = bool.Parse(lowered[2]);

                    executePath = args[3];

                    if (executePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && executePath.Contains("://")) executePath = DownloadFile(executePath, ".bat");

                    CustomRun("cmd.exe", "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true);
                    break;

                case "ps1":
                    showWindow = bool.Parse(lowered[1]);
                    shellExecute = bool.Parse(lowered[2]);

                    executePath = args[3];

                    if (executePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && executePath.Contains("://")) executePath = DownloadFile(executePath, ".ps1");

                    CustomRun(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute);
                    break;
            }
        }

        private static string DownloadFile(string uri, string ext)
        {
            string filePath = GetTempFilePath(ext);
            File.WriteAllBytes(filePath, wc.DownloadData(uri));
            RemoveFileOnBoot(filePath);
            return filePath;
        }

        private static string GetTempFilePath(string ext)
        {
            string tarjetFilePath = null;
            while (tarjetFilePath == null || File.Exists(tarjetFilePath)) tarjetFilePath = Path.GetTempFileName() + ext;
            return tarjetFilePath;
        }

        private static void CustomRun(string fileName, string arguments = null, bool showWindow = true, bool shellExecute = false)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,

                UseShellExecute = shellExecute,
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
            });
        }
    }
}