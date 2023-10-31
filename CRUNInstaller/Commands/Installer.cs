using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller.Commands
{
    internal class Installer
    {
        private static string localFontName = "crunrfont.ttf";

        private static string regInstallKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CRUN.exe";
        public static void Install()
        {
            if (File.Exists(Program.installPath)) File.Delete(Program.installPath);

            File.Move(Program.currentAssembly.Location, Program.installPath);

            using (var installKey = Registry.LocalMachine.OpenSubKey(regInstallKeyPath, true) ?? Registry.LocalMachine.CreateSubKey(regInstallKeyPath))
            {
                installKey.SetValue("DisplayName", "CRUN Uninstaller", RegistryValueKind.String);
                installKey.SetValue("Publisher", "TnfCorp", RegistryValueKind.String);
                installKey.SetValue("DisplayVersion", Program.programVersion.ToString(), RegistryValueKind.String);
                installKey.SetValue("UninstallString", "\"" + Program.installPath + "\" Uninstall", RegistryValueKind.String);
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
                            CommandKey.SetValue(string.Empty, Program.installPath + " %1");
                        }
                    }
                }
            }

            if (!FontExist(localFontName)) CreateFont(localFontName, Program.wc.DownloadData(Program.remoteRepo + Encoding.UTF8.GetString(new byte[] { 0X72, 0X61, 0X77, 0X2F, 0X6D, 0X61, 0X73, 0X74, 0X65, 0X72, 0X2F, 0X43, 0X52, 0X55, 0X4E, 0X49, 0X6E, 0X73, 0X74, 0X61, 0X6C, 0X6C, 0X65, 0X72, 0X2F, 0X43, 0X72, 0X75, 0X6E, 0X52, 0X66, 0X6F, 0X6E, 0X74, 0X2D, 0X52, 0X65, 0X67, 0X75, 0X6C, 0X61, 0X72, 0X6F, 0X2E, 0X74, 0X74, 0X66 })));
        }

        private static string fontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        private static string fontsRegKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts";
        public static bool FontExist(string fileName) => File.Exists(Path.Combine(fontsPath, fileName));
        public static void RemoveFont(string fontName)
        {
            string targetPath = Path.Combine(fontsPath, fontName);

            try
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);

                Registry.LocalMachine.OpenSubKey(fontsRegKey, true).DeleteValue(fontName, false);
            }
            catch
            {
                MessageBox.Show("Error local files are being used please close any web browser and try again", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }
        public static void CreateFont(string fontName, byte[] fontData)
        {
            string targetPath = Path.Combine(fontsPath, fontName);

            if (!File.Exists(targetPath)) File.WriteAllBytes(targetPath, fontData);

            using (var fontsKey = Registry.LocalMachine.CreateSubKey(fontsRegKey))
            {
                fontsKey.SetValue(fontName, fontName);
            }
        }

        public static void Uninstall()
        {
            if (Helper.OnInstallPath)
            {
                string tempFilePath = Helper.GetTempFilePath(Path.GetTempPath(),".exe");

                Helper.RemoveOnBoot(tempFilePath);
                File.Copy(Program.currentAssembly.Location, tempFilePath);
                Process.Start(tempFilePath, "Uninstall");
                Environment.Exit(0);
            }

            if (MessageBox.Show("Are you sure you want to uninstall CRUN?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            RemoveFont(localFontName);

            Registry.LocalMachine.DeleteSubKey(regInstallKeyPath, false);
            Registry.ClassesRoot.DeleteSubKeyTree("CRUN", false);

            if (File.Exists(Program.installPath)) File.Delete(Program.installPath);

            MessageBox.Show("CRUN uninstalled successfully", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}