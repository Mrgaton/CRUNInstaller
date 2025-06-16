using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller.Commands
{
    internal static class Installer
    {
        private static string localFontName = Program.programProduct + "Font.ttf";

        private static string regInstallKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + Program.programProduct;

        private static string policesKeyPath = "Software\\Policies\\";

        private static string autoLaunchProtocolsKeyValue = "AutoLaunchProtocolsFromOrigins";

        private static string[] regBrowsersAllowListPath = [
            policesKeyPath + "Google\\Chrome",
            policesKeyPath + "Microsoft\\Edge",
            policesKeyPath + "BraveSoftware\\Brave",
            policesKeyPath + "Mozilla\\Firefox"
        ];

        public static void Install()
        {
            if (File.Exists(Program.installPath)) File.Delete(Program.installPath);

            File.Move(Program.currentAssembly.Location, Program.installPath);

            using (var installKey = Registry.LocalMachine.OpenSubKey(regInstallKeyPath, true) ?? Registry.LocalMachine.CreateSubKey(regInstallKeyPath))
            {
                installKey.SetValue("DisplayName", Program.programProduct + " Uninstaller", RegistryValueKind.String);
                installKey.SetValue("Publisher", "TnfCorp", RegistryValueKind.String);
                installKey.SetValue("DisplayVersion", Program.programVersion.ToString(), RegistryValueKind.String);
                installKey.SetValue("UninstallString", "\"" + Program.installPath + "\" Uninstall", RegistryValueKind.String);
            }

            string autolaunchProtocolPayload = "{\"protocol\":\"" + Program.programProduct.ToLower() + "\",\"allowed_origins\":[\"https://gato.ovh\"]}";

            foreach (var regAllowListPath in regBrowsersAllowListPath)
            {
                using (var allowListKey = Registry.LocalMachine.OpenSubKey(regAllowListPath, true) ?? Registry.LocalMachine.CreateSubKey(regAllowListPath))
                {
                    string[] values = allowListKey.GetValueNames();

                    if (!values.Contains(autoLaunchProtocolsKeyValue))
                    {
                        allowListKey.SetValue(autoLaunchProtocolsKeyValue, $"[{autolaunchProtocolPayload}]", RegistryValueKind.String);
                    }
                    else
                    {
                        string json = ((string)allowListKey.GetValue(autoLaunchProtocolsKeyValue)).Trim();

                        if (!json.Split(',').Any(array => array.Contains($"\"{Program.programProduct.ToLower()}\"")))
                        {
                            json = (!json.StartsWith("[") && !json.EndsWith("]") ? $"[{autolaunchProtocolPayload}]" : json.Remove(json.Length - 1, 1) + "," + autolaunchProtocolPayload + "]");

                            allowListKey.SetValue(autoLaunchProtocolsKeyValue, json, RegistryValueKind.String);
                        }
                    }

                    /*if (!values.Any(value => ((string)allowListKey.GetValue(value)).Contains("crun")))
                    {
                        List<ulong> alreadyAdded = new List<ulong>();

                        foreach (string value in values)
                        {
                            if (ulong.TryParse(value, out ulong num)) alreadyAdded.Add(num);
                        }

                        ulong definedNum = 1;

                        while (alreadyAdded.Contains(definedNum)) definedNum++;

                        allowListKey.SetValue(definedNum.ToString(), "crun://*", RegistryValueKind.String);
                    }*/
                }
            }

            using (var protocolKey = Registry.ClassesRoot.CreateSubKey(Program.programProduct))
            {
                protocolKey.SetValue(string.Empty, $"URL: {Program.programProduct} Protocol");
                protocolKey.SetValue("URL Protocol", string.Empty);

                using (var ProtocolShellKey = protocolKey.CreateSubKey("shell"))
                {
                    using (var OpenKey = ProtocolShellKey.CreateSubKey("open"))
                    {
                        using (var CommandKey = OpenKey.CreateSubKey("command"))
                        {
                            CommandKey.SetValue(string.Empty, '\"' + Program.installPath + "\" %1");
                        }
                    }
                }
            }

            if (!FontExist(localFontName)) CreateFont(localFontName, Program.client.GetByteArrayAsync(Program.remoteRepo + Encoding.UTF8.GetString([0X72, 0X61, 0X77, 0X2F, 0X6D, 0X61, 0X73, 0X74, 0X65, 0X72, 0X2F, 0X43, 0X52, 0X55, 0X4E, 0X49, 0X6E, 0X73, 0X74, 0X61, 0X6C, 0X6C, 0X65, 0X72, 0X2F, 0X43, 0X72, 0X75, 0X6E, 0X52, 0X66, 0X6F, 0X6E, 0X74, 0X2D, 0X52, 0X65, 0X67, 0X75, 0X6C, 0X61, 0X72, 0X6F, 0X2E, 0X74, 0X74, 0X66])).Result);

            MessageBox.Show($"CRUN v{Program.programVersion} installed successfully", Application.ProductName + " Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string tempFilePath = Helper.GetTempFilePath(Path.GetTempPath(), ".exe");

                Helper.RemoveOnBoot(tempFilePath);
                File.Copy(Program.currentAssembly.Location, tempFilePath);
                Process.Start(tempFilePath, "Uninstall");
                Environment.Exit(0);
            }

            if (MessageBox.Show("Are you sure you want to uninstall CRUN? :C", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            RemoveFont(localFontName);

            Registry.LocalMachine.DeleteSubKey(regInstallKeyPath, false);
            Registry.ClassesRoot.DeleteSubKeyTree(Program.programProduct, false);

            try
            {
                if (File.Exists(Program.installPath)) File.Delete(Program.installPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error uninstalling the program make sure that the app isnt opened\n\n" + ex.ToString(), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }

            if (Directory.Exists(Program.trustedTokensPath)) Directory.Delete(Program.trustedTokensPath, true);

            MessageBox.Show(Program.programProduct + " uninstalled successfully", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}