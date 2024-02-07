using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal static class ArgsProcessor
    {
        private static Dictionary<string, string> argsSplited = new Dictionary<string, string>();

        private static string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        private static string defaultPowershellArgs = "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass";
        private static bool ParseBool(string text, bool defaultValue = default)
        {
            string trimed = text.Trim().ToLower();

            if (trimed == "true" || trimed == "1") return true;
            else if (trimed == "false" || trimed == "0") return false;

            return defaultValue;
        }

        private static bool GetArgBool(string argName, bool defaultValue) => argsSplited.ContainsKey(argName) ? ParseBool(argsSplited[argName], defaultValue) : defaultValue;

        private static string defaultTempPath = Path.Combine(Path.GetTempPath(), Program.programProduct);

        private static void SetCurrentDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            Directory.SetCurrentDirectory(dirPath);
        }

        public static void ProcessArguments(string[] args)
        {
            bool urlCalled = args[0].StartsWith(Program.programProduct + "://", StringComparison.InvariantCultureIgnoreCase);

            if (urlCalled) args = args[0].Split('/').Skip(2).Select(Uri.UnescapeDataString).ToArray();

            args = args.Select(Environment.ExpandEnvironmentVariables).ToArray();

            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            foreach (string argument in args)
            {
                int index = argument.IndexOf('=');

                if (index != -1) argsSplited.Add(argument.Substring(0, index), argument.Substring(index + 1).Trim('\"'));
            }

            if (urlCalled)
            {
                argsSplited.TryGetValue("cname", out string name);
                argsSplited.TryGetValue("ctoken", out string token);

                string fileName = Helper.ToSafeBase64(Helper.hashAlg.ComputeHash(Encoding.UTF8.GetBytes(name + token)));

                string tarjetPath = Path.Combine(Program.trustedTokensPath, fileName);

                if (!File.Exists(tarjetPath))
                {
                    if (MessageBox.Show($"Are you sure you want to trust the page \"{name}\" for running commands on your pc?\n\n¡This message won't pop out again for commands from the same website!", Program.programProduct, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) Environment.Exit(0);
                    if (!Directory.Exists(Program.trustedTokensPath)) Directory.CreateDirectory(Program.trustedTokensPath);
                    File.Create(tarjetPath).Close();
                    //Helper.RemoveOnBoot(tarjetPath);
                }
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

            if (argsSplited.TryGetValue("tarjetVersion", out string intendedVersion))
            {
                int result = int.MaxValue;

                foreach (string version in intendedVersion.Split(',').OrderBy(e => e))
                {
                    var tempResult = Program.programVersion.CompareTo(new Version(version));

                    if (result != 0) result = tempResult;
                }

                if (result != 0)
                {
                    if (result < 0)
                    {
                        MessageBox.Show("Error el programa esta desactualizado y no soporta este tipo de commandos por favor actualizalo en la pagina official\n\n\"" + Program.remoteRepo + "\"", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (result > 0)
                    {
                        MessageBox.Show("El controlador está intentando ejecutar comandos que ya no son soportados", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    return;
                }
            }

            bool showWindow = GetArgBool("showWindow", true);
            bool shellExecute = GetArgBool("shellExecute", true);
            bool requestUac = GetArgBool("requestUac", false);

            if (argsSplited.TryGetValue("currentDir", out string currentDirPath)) SetCurrentDirectory(currentDirPath);
            else SetCurrentDirectory(defaultTempPath);

            argsSplited.TryGetValue("run", out string executePath);
            argsSplited.TryGetValue("args", out string arguments);

            bool autoClose = GetArgBool("autoClose", true);

            switch (lowered[0])
            {
                case "run":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, Path.GetExtension(executePath.Split('/').Last().Split('?')[0]));

                    CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                    break;

                case "cmd":

                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun(string.Join("", (new[] { 'e', 'x', 'e', '.', 'D', 'M', 'C' }).Reverse().ToArray()), "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true, requestUac);
                    break;

                case "ps1":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".ps1");
                    CustomRun(powershellPath, defaultPowershellArgs + (autoClose ? null : " -NoExit") + " -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute, requestUac);
                    break;

                case "eps1":
                    CustomRun(powershellPath, defaultPowershellArgs + (autoClose ? null : " -NoExit") + " -EncodedCommand " + executePath, showWindow, shellExecute, requestUac);
                    break;

                default:
                    if (Helper.IsLink(executePath))
                    {
                        executePath = Helper.DownloadFile(executePath, "." + lowered[0]);
                        CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                        return;
                    }

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
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
            });
        }
    }
}