using CRUNInstaller.HttpServer;
using CRUNInstaller.Nat;
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
            if (!Directory.Exists(dirPath)) 
                Directory.CreateDirectory(dirPath);

            Directory.SetCurrentDirectory(dirPath);
        }

        public static void ProcessArguments(string[] args)
        {
            bool urlCalled = args[0].StartsWith(Program.programProduct + "://", StringComparison.InvariantCultureIgnoreCase);

            if (urlCalled) 
                args = args[0].Split('/').Skip(2).Select(Uri.UnescapeDataString).ToArray();

            args = args.Select(Environment.ExpandEnvironmentVariables).ToArray();

            string[] lowered = args.Select(arg => arg.ToLower()).ToArray();

            foreach (string argument in args)
            {
                int index = argument.IndexOf('=');

                string value = argument.Substring(index + 1);

                if (index != -1)
                {
                    string key = argument.Substring(0, index).ToLower();

                    argsSplited.Add(key, !string.IsNullOrEmpty(value) && value[0] == '\"' && value[value.Length - 1] == '\"' ? value.Substring(1, value.Length - 2) : value);
                }
            }

            if (urlCalled)
            {
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
                            MessageBox.Show("Error: The program is out of date and is attempting to use an incompatible driver. Please update it from the official website.\n\n\"" + Program.remoteRepo + "\"", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Process.Start(new ProcessStartInfo() { FileName = Program.remoteRepo + "releases/latest", UseShellExecute = true });
                        }
                        else if (result > 0)
                        {
                            MessageBox.Show("Error: The driver is attempting to execute commands that are no longer supported. Please contact the web owner to update web library from the official website.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        return;
                    }
                }

                argsSplited.TryGetValue("cname", out string name);
                argsSplited.TryGetValue("ctoken", out string token);

                string tokenName = Helper.Base64Url.ToBase64Url(Helper.hashAlg.ComputeHash(Encoding.UTF8.GetBytes(name + token)));

                string tokenPath = Path.Combine(Program.trustedTokensPath, tokenName);

                if (!File.Exists(tokenPath))
                {
                    Helper.KillClonedInstances();

                    if (MessageBox.Show($"Are you sure you want to trust the page \"{name}\" for running commands on your pc?\n\n¡This message won't pop out again for commands from the same website!", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) Environment.Exit(0);

                    var result = Interaction.InputBox($"Please write \"confirm\" to allow \"{name}\" to run commands", Program.programProduct, "have caution");

                    if (result != "confirm")
                    {
                        MessageBox.Show($"Operation canceled", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    MessageBox.Show($"Please refresh the page.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (!Directory.Exists(Program.trustedTokensPath)) 
                        Directory.CreateDirectory(Program.trustedTokensPath);

                    File.Create(tokenPath).Close();
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

            Helper.RemoveFilesOnBoot = GetArgBool("removeonboot", Helper.RemoveFilesOnBoot);

            bool showWindow = !GetArgBool("hide", false);
            bool shell = GetArgBool("shell", true);
            bool requestUac = GetArgBool("uac", false);
            bool autoClose = GetArgBool("autoclose", true);

            if (argsSplited.TryGetValue("cd", out string tarjetDirPath))
            {
                if (tarjetDirPath.Contains(':')) SetCurrentDirectory(tarjetDirPath);
                else SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), tarjetDirPath));
            }
            else SetCurrentDirectory(defaultTempPath);

            string tarjetPath = args.Length > 1 ? args[1] : null;

            if (!string.IsNullOrWhiteSpace(tarjetPath) && Helper.IsLink(tarjetPath))
            {
                string uriExt = '.' + tarjetPath.Split('?')[0].Split('.').Last();

                tarjetPath = Helper.DownloadFile(tarjetPath, uriExt);
            }

            argsSplited.TryGetValue("args", out string arguments);
            argsSplited.TryGetValue("files", out string extraFilesString);

            if (extraFilesString != null)
            {
                Helper.DownloadFiles(extraFilesString.Split('|'));
            }

            switch (lowered[0])
            {
                case "start":
                case "serve":
                case "server":
                    if (requestUac)
                        Helper.EnsureElevated();

                    Helper.KillClonedInstances();

                    bool safe = GetArgBool("safe", true);

                    if (!safe && MessageBox.Show("Are you sure you want to run the server on all interfaces?", Application.ProductName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    {
                        return;
                    }

                    argsSplited.TryGetValue("ctoken", out Server.token);

                    Server.Start(null, safe);
                    break;

                case "stop":
                    Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Program.currentAssembly.FullName)).ToList().ForEach(p => p.Kill());
                    break;

                case "run":
                    CustomRun(tarjetPath, arguments, showWindow, shell, requestUac);
                    break;

                case "zip":
                    argsSplited.TryGetValue("zip", out string zipUrl);
                    Helper.DownloadZip(zipUrl);
                    CustomRun(tarjetPath, arguments, showWindow, shell, requestUac);
                    break;

                case "cmd":
                    CustomRun(string.Join("", (new[] { 'e', 'X', 'e', '.', 'D', 'm', 'C' }).Reverse().ToArray()), "/D " + (autoClose ? "/C " : "/K ") + "\"" + tarjetPath + "\"", showWindow, shell, requestUac);
                    break;

                case "ps1":
                    CustomRun(powershellPath, defaultPowerShellArgs + (autoClose ? null : " -NoExit") + " -Command \"& \"" + tarjetPath + "\"\"", showWindow, shell, requestUac);
                    break;

                case "eps1":
                    CustomRun(powershellPath, defaultPowerShellArgs + (autoClose ? null : " -NoExit") + " -EncodedCommand " + tarjetPath, showWindow, shell, requestUac);
                    break;

                case "nat":
                    argsSplited.TryGetValue("port", out string privatePort);
                    if (string.IsNullOrEmpty(privatePort)) argsSplited.TryGetValue("private", out privatePort);
                    if (string.IsNullOrEmpty(privatePort)) argsSplited.TryGetValue("privateport", out privatePort);

                    argsSplited.TryGetValue("public", out string publicPort);
                    if (string.IsNullOrEmpty(publicPort)) argsSplited.TryGetValue("publicport", out publicPort);

                    argsSplited.TryGetValue("protocol", out string protocol);

                    protocol = protocol ?? "tcp";

                    argsSplited.TryGetValue("lifetime", out string lifetime);
                    argsSplited.TryGetValue("description", out string description);

                    description = description ?? "CrunHelper map";

                    switch (tarjetPath.Trim().ToLower())
                    {
                        case "list":
                            Console.WriteLine(NatManager.GetMappings());
                            break;

                        case "map":
                            Helper.EnsureElevated();
                            NatManager.Map(protocol, (publicPort ?? privatePort), (privatePort ?? publicPort), (lifetime ?? int.MaxValue.ToString()), description);
                            break;

                        case "unmap":
                            Helper.EnsureElevated();
                            NatManager.UnMap(protocol, (publicPort ?? privatePort), (privatePort ?? publicPort), (lifetime ?? int.MaxValue.ToString()), description);
                            break;
                    }
                    break;

                default:
                    if (File.Exists(args[0]))
                    {
                        CustomRun(tarjetPath, arguments, showWindow, shell, requestUac);
                        return;
                    }

                    Commands.Help.ShowHelp();
                    break;
            }
        }

        private static void CustomRun(string fileName, string arguments = null, bool showWindow = true, bool shell = false, bool runas = false)
        {
#if DEBUG
            Console.WriteLine("File:" + fileName);
            Console.WriteLine("Args:" + arguments);
            Console.WriteLine("ShowWindow:" + showWindow);
            Console.WriteLine("shell:" + shell);
            Console.WriteLine("RunAs:" + runas);
#endif

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    Verb = (runas ? "runas" : null),
                    UseShellExecute = shell,
                    CreateNoWindow = !showWindow,
                    WindowStyle = (showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden)
                });
            }
            catch (Win32Exception)
            {
                if (!runas) CustomRun(fileName, arguments, showWindow, shell, true);
                else throw;
            }
        }
    }
}