using CRUNInstaller.HttpServer;
using Microsoft.VisualBasic;
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

        public static bool ParseBool(string text, bool defaultValue = default)
        {
            if (string.IsNullOrEmpty(text)) return defaultValue;

            char fc = char.ToLower(text.Trim()[0]);

            if (fc == 't' || fc == '1') return true;
            if (fc == 'f' || fc == '0') return false;

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

                string value = argument.Substring(index + 1);

                if (index != -1) argsSplited.Add(argument.Substring(0, index).ToLower(), (value[0] == '\"' && value[value.Length - 1] == '\"' ? value.Substring(1, value.Length - 2) : value));
            }

            if (urlCalled)
            {
                argsSplited.TryGetValue("cname", out string name);
                argsSplited.TryGetValue("ctoken", out string token);

                string fileName = Helper.Base64Url.ToBase64Url(Helper.hashAlg.ComputeHash(Encoding.UTF8.GetBytes(name + token)));

                string tarjetPath = Path.Combine(Program.trustedTokensPath, fileName);

                if (!File.Exists(tarjetPath))
                {
                    Helper.KillClonedInstances();

                    if (MessageBox.Show($"Are you sure you want to trust the page \"{name}\" for running commands on your pc?\n\n¡This message won't pop out again for commands from the same website!", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) Environment.Exit(0);

                    var result = Interaction.InputBox($"Please write \"confirm\" to allow \"{name}\" to run commands", Program.programProduct, "have caution");

                    if (result != "confirm")
                    {
                        MessageBox.Show($"Operation canceled", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

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

            Helper.RemoveFilesOnBoot = GetArgBool("removeOnBoot", Helper.RemoveFilesOnBoot);

            bool showWindow = GetArgBool("showwindow", true);
            bool shellExecute = GetArgBool("shellexecute", true);
            bool requestUac = GetArgBool("uac", false);
            bool autoClose = GetArgBool("autoclose", true);

            if (argsSplited.TryGetValue("cd", out string tarjetDirPath))
            {
                if (tarjetDirPath.Contains(':')) SetCurrentDirectory(tarjetDirPath);
                else SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), tarjetDirPath));
            }
            else SetCurrentDirectory(defaultTempPath);

            argsSplited.TryGetValue("run", out string executePath);
            argsSplited.TryGetValue("args", out string arguments);

            argsSplited.TryGetValue("files", out string extraFilesString);

            if (executePath == null && args.Length > 1) executePath = args[1];

            if (extraFilesString != null)
            {
                string[] extraFiles = extraFilesString.Split('|');

                Helper.DownloadFiles(extraFiles);
            }

            switch (lowered[0])
            {
                case "start":
                case "serve":
                case "server":
                    Helper.KillClonedInstances();

                    bool safe = !GetArgBool("unsafe", false);

                    if (!safe && MessageBox.Show("Are you sure you want to run the server on all interfaces?", Application.ProductName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    {
                        return;
                    }

                    Server.Start(null, safe);
                    break;

                case "stop":
                    Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Program.currentAssembly.FullName)).ToList().ForEach(p => p.Kill());
                    break;

                case "run":
                    if (Helper.IsLink(executePath)) executePath = Helper.DownloadFile(executePath);
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
#if DEBUG
            Console.WriteLine("File:" + fileName);
            Console.WriteLine("Args:" + arguments);
            Console.WriteLine("ShowWindow:" + showWindow);
            Console.WriteLine("ShellExecute:" + shellExecute);
            Console.WriteLine("RunAs:" + runas);
#endif

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