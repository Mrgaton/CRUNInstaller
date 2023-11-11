using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal static class ArgsProcessor
    {
        public static void ProcessArguments(string[] args)
        {
            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            if (args.Length <= 1)
            {
                switch (lowered[0])
                {
                    case "install":
                        Commands.Installer.Install();
                        break;

                    case "uninstall":
                        Commands.Installer.Uninstall();
                        break;

                    default:
                        Commands.Help.ShowHelp();
                        break;
                }

                return;
            }

            args = args.Select(arg => Environment.ExpandEnvironmentVariables(arg)).ToArray();

            //MessageBox.Show(string.Join("\" \"",args));

            bool showWindow = bool.Parse(lowered[1]);
            bool shellExecute = true;

            string executePath = args[3];

            switch (lowered[0])
            {
                case "run":
                    shellExecute = bool.Parse(lowered[2]);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, Path.GetExtension(executePath.Split('/').Last().Split('?')[0]));

                    CustomRun(executePath, args[4], showWindow, shellExecute);
                    break;

                case "cmd":
                    bool autoClose = bool.Parse(lowered[2]);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun(string.Join("", (new[] { 'e', 'x', 'e', '.', 'D', 'M', 'C' }).Reverse().ToArray()), "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true);
                    break;

                case "ps1":
                    shellExecute = bool.Parse(lowered[2]);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".ps1");

                    CustomRun(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute);
                    break;

                default:
                    Commands.Help.ShowHelp();
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