using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CRUNInstaller
{
    internal class ArgsProcessor
    {
        public static void ProcessArguments(string[] args)
        {
            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            bool showWindow = true;
            bool shellExecute = true;

            string executePath = null;

            //MessageBox.Show("\"" + lowered[0] + "\"");

            switch (lowered[0])
            {
                case "help":
                    Commands.Help.ShowHelp();
                    break;

                case "uninstall":
                    Commands.Installer.Uninstall();
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

                    if (executePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && executePath.Contains("://")) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun("cmd.exe", "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true);
                    break;

                case "ps1":
                    showWindow = bool.Parse(lowered[1]);
                    shellExecute = bool.Parse(lowered[2]);

                    executePath = args[3];

                    if (executePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && executePath.Contains("://")) executePath = Helper.DownloadFile(executePath, ".ps1");

                    CustomRun(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute);
                    break;
            }
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