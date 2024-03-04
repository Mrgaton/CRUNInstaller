using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        private static bool ParseBool(string text, bool defaultValue = default)
        {
            string trimed = text.Trim();

            if (char.ToLower(trimed[0]) == 't' || trimed == "1") return true;
            else if (char.ToLower(trimed[0]) == 'f' || trimed == "0") return false;

            return bool.TryParse(text, out bool res) ? res : defaultValue;
        }

        private static bool GetArgBool(string argName, bool defaultValue) => argsSplited.ContainsKey(argName) ? ParseBool(argsSplited[argName], defaultValue) : defaultValue;

        private static string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");

        private static string defaultPowerShellArgs = "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass";
        
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

                if (index != -1) argsSplited.Add(argument.Substring(0, index).ToLower(), argument.Substring(index + 1).Trim('\"'));
            }

            if (urlCalled)
            {
                argsSplited.TryGetValue("cname", out string name);
                argsSplited.TryGetValue("ctoken", out string token);

                string fileName = Helper.ToSafeBase64(Helper.hashAlg.ComputeHash(Encoding.UTF8.GetBytes(name + token)));

                string tarjetPath = Path.Combine(Program.trustedTokensPath, fileName);

                if (!File.Exists(tarjetPath))
                {
                    if (MessageBox.Show($"Are you sure you want to trust the page \"{name}\" for running commands on your pc?\n\n¡This message won't pop out again for commands from the same website!", Program.programProduct, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) Environment.Exit(0);
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
                        return;

                    case "uninstall":
                        Commands.Installer.Uninstall();
                        return;
                }
            }

            if (argsSplited.TryGetValue("tarjetversion", out string intendedVersion))
            {
                int result = int.MaxValue;

                foreach (string version in intendedVersion.Split(',').OrderBy(e => e))
                {
                    if (result != 0) result = Program.programVersion.CompareTo(new Version(version));
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

            bool showWindow = GetArgBool("showwindow", true);
            bool shellExecute = GetArgBool("shellexecute", true);
            bool requestUac = GetArgBool("requestuac", false);
            bool autoClose = GetArgBool("autoclose", true);

            if (argsSplited.TryGetValue("currentdir", out string currentDirPath)) SetCurrentDirectory(currentDirPath);
            else SetCurrentDirectory(defaultTempPath);

            argsSplited.TryGetValue("run", out string executePath);
            argsSplited.TryGetValue("args", out string arguments);

            argsSplited.TryGetValue("files", out string extraFilesString);

            if (extraFilesString != null)
            {
                string[] extraFiles = extraFilesString.Split('|');

                Helper.DownloadFiles(extraFiles);
            }

            switch (lowered[0])
            {
                case "run":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, Path.GetExtension(executePath.Split('/').Last().Split('?')[0]));
                    CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                    break;

                case "zip":
                    argsSplited.TryGetValue("zip", out string zipUrl);
                    Helper.DownloadZip(zipUrl);
                    CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                    break;

                case "cmd":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".bat");

                    CustomRun(string.Join("", (new[] { 'e', 'x', 'e', '.', 'D', 'M', 'C' }).Reverse().ToArray()), "/d " + (autoClose ? "/c " : "/k ") + "\"" + executePath + "\"", showWindow, true, requestUac);
                    break;

                case "ps1":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath, ".ps1");

                    CustomRun(powershellPath, defaultPowerShellArgs + (autoClose ? null : " -NoExit") + " -Command \"& \"" + executePath + "\"\"", showWindow, shellExecute, requestUac);
                    break;

                case "eps1":
                    CustomRun(powershellPath, defaultPowerShellArgs + (autoClose ? null : " -NoExit") + " -EncodedCommand " + executePath, showWindow, shellExecute, requestUac);
                    break;

                default:
                    if (Helper.IsLink(args[0]))
                    {
                        executePath = Helper.DownloadFile(args[0], '.' + lowered[0].Split('?')[0].Split('.').Last());

                        CustomRun(executePath, arguments, showWindow, shellExecute, requestUac);
                        return;
                    }

                    Commands.Help.ShowHelp();
                    break;
            }
        }

        private static void CustomRun(string fileName, string arguments = null, bool showWindow = true, bool shellExecute = false, bool runas = false)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    Verb = (runas ? "runas" : null),
                    UseShellExecute = shellExecute,
                    CreateNoWindow = !showWindow,
                    WindowStyle = (showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden)
                });
            }
            catch (Win32Exception)
            {
                if (!runas) CustomRun(fileName, arguments, showWindow, shellExecute, true);
                else throw;
            }
        }
    }
}