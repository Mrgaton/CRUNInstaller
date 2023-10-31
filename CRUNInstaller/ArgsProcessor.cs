using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class ArgsProcessor
    {
        public static void ProcessArguments(string[] args)
        {
            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            MessageBox.Show("\""+ string.Join("\" \"",args) + "\"");

            if (args.Length <= 1)
            {
                switch (lowered[0])
                {
                    case "help":
                        Commands.Help.ShowHelp();
                        break;

                    case "uninstall":
                        Commands.Installer.Uninstall();
                        break;
                }

                return;
            }

            bool showWindow = bool.Parse(lowered[1]);
            bool shellExecute = true;

            string executePath = args[3];

            switch (lowered[0])
            {
                case "run":
                    shellExecute = bool.Parse(lowered[2]);


                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, Path.GetExtension(executePath.Split('/').Last()));

                    CustomRun(executePath, args[4], showWindow, shellExecute);
                    break;

                case "cmd":
                    bool autoClose = bool.Parse(lowered[2]);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun("cmd.exe", "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true);
                    break;

                case "ps1":
                    shellExecute = bool.Parse(lowered[2]);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".ps1");

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