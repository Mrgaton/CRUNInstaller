using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 && StringComparer.InvariantCultureIgnoreCase.Equals(Path.GetFullPath(currentAssembly.Location), Path.GetFullPath(installPath))) args = new string[] { "help" };

            if (args.Length > 0)

            if(args.Length == 0)
            {
                Install();

                MessageBox.Show("CRUN installed successfully", Application.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }

        private static Assembly currentAssembly = Assembly.GetExecutingAssembly();
        private static string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "crun.exe");
        private static string regInstallKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CRUN.exe";
        public static void Install()
        {
            if (File.Exists(installPath))File.Delete(installPath);

            File.Move(currentAssembly.Location, installPath);

            RegistryKey key = Registry.LocalMachine.OpenSubKey(regInstallKeyPath, true) ?? Registry.LocalMachine.CreateSubKey(regInstallKeyPath);

            if (key != null)
            {
                key.SetValue("DisplayName", "CRUN Uninstaller", RegistryValueKind.String);
                key.SetValue("Publisher", "TnfCorp", RegistryValueKind.String);
                key.SetValue("DisplayVersion", Assembly.GetExecutingAssembly().GetName().Version.ToString(), RegistryValueKind.String);
                key.SetValue("UninstallString", "Cmd.exe", RegistryValueKind.String);
                key.Close();
            }

            using (var ProtocolKey = Registry.ClassesRoot.CreateSubKey("CRUN"))
            {
                ProtocolKey.SetValue(string.Empty, "URL: CRUN Protocol");
                ProtocolKey.SetValue("URL Protocol", string.Empty);

                using (var ProtocolShellKey = ProtocolKey.CreateSubKey("shell"))
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
        }

    }
}
