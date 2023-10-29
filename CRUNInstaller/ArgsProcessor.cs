using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

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

            if (lowered[0] == "help")
            {
                Commands.Help.ShowHelp();

                return;
            }
            else if (lowered[0] == "run")
            {
                bool showWindow = bool.Parse(lowered[1]);
                bool shellExecute = bool.Parse(lowered[2]);

                CustomRun(args[3], args[4], showWindow, shellExecute);
            }
            else if (lowered[0] == "cmd")
            {
                bool showWindow = bool.Parse(lowered[1]);
                bool autoClose = bool.Parse(lowered[2]);

                string execute = args[3];

                if (execute.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && execute.Contains("://")) execute = DownloadFile(execute, ".bat");

                CustomRun("cmd.exe", (autoClose ? "/c " : "/k ") + "/d \"" + execute + "\"", showWindow, true);
            }
            else if (lowered[0] == "ps1")
            {
                bool showWindow = bool.Parse(lowered[1]);
                bool shellExecute = bool.Parse(lowered[2]);

                string execute = args[3];

                if (execute.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && execute.Contains("://")) execute = DownloadFile(execute, ".ps1");

                CustomRun(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"& \"" + execute + "\"\"", showWindow, shellExecute);
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