using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal static class ArgsProcessor
    {
        private static Dictionary<string, string> argsSplited = new Dictionary<string, string>();

        private static bool GetArgBool(string argName, bool defaultValue) => argsSplited.ContainsKey(argName) ? bool.Parse(argsSplited[argName]) : defaultValue;

        private static string defaultTempPath = Path.Combine(Path.GetTempPath(), Program.programProduct);
        public static void ProcessArguments(string[] args)
        {
            args = args.Select(arg => Environment.ExpandEnvironmentVariables(arg)).ToArray();

            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            foreach (string argument in args)
            {
                int index = argument.IndexOf('=');

                if (index != -1) argsSplited.Add(argument.Substring(0, index), argument.Substring(index + 1).Trim('\"'));
            }

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

            //MessageBox.Show(string.Join("\" \"",args));

            bool showWindow = GetArgBool("showWindow", true);
            bool shellExecute = GetArgBool("shellExecute", true);
            bool requestUac = GetArgBool("requestUac", false);

            if (argsSplited.TryGetValue("currentDir", out string currentDirPath)) Directory.SetCurrentDirectory(currentDirPath);
            else
            {
                if (!Directory.Exists(defaultTempPath)) Directory.CreateDirectory(defaultTempPath);

                Directory.SetCurrentDirectory(defaultTempPath);
            }

            argsSplited.TryGetValue("run", out string executePath);
            argsSplited.TryGetValue("args", out string arguments);

            switch (lowered[0])
            {
                case "run":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, Path.GetExtension(executePath.Split('/').Last().Split('?')[0]));

                    CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                    break;

                case "cmd":
                    bool autoClose = GetArgBool("autoClose", true);

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun(string.Join("", (new[] { 'e', 'x', 'e', '.', 'D', 'M', 'C' }).Reverse().ToArray()), "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true, requestUac);
                    break;

                case "ps1":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".ps1");

                    CustomRun(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute, requestUac);
                    break;

                default:
                    Commands.Help.ShowHelp();
                    break;
            }
        }

        private static void CustomRun(string fileName, string arguments = null, bool showWindow = true, bool shellExecute = false, bool runas = false)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,

                Verb = runas ? "runas" : null,

                UseShellExecute = shellExecute,
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
            });
        }
    }
}