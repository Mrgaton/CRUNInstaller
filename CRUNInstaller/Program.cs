using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class Program
    {
        [DllImport("kernel32.dll")] private static extern bool AllocConsole();

        [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

        [STAThread]
        protected static void Main(string[] args)
        {
            if (args.Length == 0 && StringComparer.InvariantCultureIgnoreCase.Equals(Path.GetFullPath(currentAssembly.Location), Path.GetFullPath(installPath))) args = new string[] { "help" };

            if (args.Length > 0)
            {
                if (args[0].ToLower().StartsWith("crun://")) args = args[0].Split('/').Skip(2).Select(arg => Uri.UnescapeDataString(arg)).ToArray();

                //MessageBox.Show(string.Join("\\", args));

                AttachConsole(-1);

                ArgsProcessor.ProcessArguments(args);
                return;
            }

            if (args.Length == 0)
            {
                Install();

                MessageBox.Show($"CRUN v{programVersion} installed successfully", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static readonly Assembly currentAssembly = Assembly.GetExecutingAssembly();
        public static readonly Version programVersion = currentAssembly.GetName().Version;

        private static string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "crun.exe");

        private static string regInstallKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CRUN.exe";
        private static string chromePoliceKeyPath = "SOFTWARE\\Policies\\Google\\Chrome";

        public static void Install()
        {
            if (File.Exists(installPath)) File.Delete(installPath);
            File.Move(currentAssembly.Location, installPath);

            using (var installKey = Registry.LocalMachine.OpenSubKey(regInstallKeyPath, true) ?? Registry.LocalMachine.CreateSubKey(regInstallKeyPath))
            {
                installKey.SetValue("DisplayName", "CRUN Uninstaller", RegistryValueKind.String);
                installKey.SetValue("Publisher", "TnfCorp", RegistryValueKind.String);
                installKey.SetValue("DisplayVersion", programVersion.ToString(), RegistryValueKind.String);
                installKey.SetValue("UninstallString", "\"" + installPath + "\" Uninstall", RegistryValueKind.String);
                installKey.Close();
            }

            using (var protocolKey = Registry.ClassesRoot.CreateSubKey("CRUN"))
            {
                protocolKey.SetValue(string.Empty, "URL: CRUN Protocol");
                protocolKey.SetValue("URL Protocol", string.Empty);

                using (var ProtocolShellKey = protocolKey.CreateSubKey("shell"))
                {
                    using (var OpenKey = ProtocolShellKey.CreateSubKey("open"))
                    {
                        using (var CommandKey = OpenKey.CreateSubKey("command"))
                        {
                            CommandKey.SetValue(string.Empty, installPath + " %1");
                        }
                    }
                }
            }

            using (var chromePoliceKey = Registry.LocalMachine.OpenSubKey(chromePoliceKeyPath, true) ?? Registry.LocalMachine.CreateSubKey(chromePoliceKeyPath))
            {
                chromePoliceKey.SetValue("ExternalProtocolDialogShowAlwaysOpenCheckbox", 1);

                using (var whitelistKey = (Registry.LocalMachine.OpenSubKey(chromePoliceKeyPath + "\\URLWhitelist", true) ?? Registry.LocalMachine.CreateSubKey(chromePoliceKeyPath + "\\URLWhitelist")))
                {
                    List<long> alreadyAddedOnes = new List<long>();

                    whitelistKey.GetValueNames().ToList().ForEach(name =>
                    {
                        try
                        {
                            alreadyAddedOnes.Add(long.Parse(name));
                        }
                        catch { }
                    });

                    long num = 0;

                    while (alreadyAddedOnes.Contains(num)) num++;

                    whitelistKey.SetValue(num.ToString(), "crun://*");
                }
            }
        }
    }
}