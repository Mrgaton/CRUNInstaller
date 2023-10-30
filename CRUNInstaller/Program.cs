using CRUNInstaller.Commands;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class Program
    {
        public static WebClient wc = new WebClient();

        public static readonly Assembly currentAssembly = Assembly.GetExecutingAssembly();
        public static readonly Version programVersion = currentAssembly.GetName().Version;

        public static string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "crun.exe");

        //[DllImport("kernel32.dll")] private static extern bool AllocConsole();

        [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

        public static bool ConsoleAtached()
        {
            try
            {
                if (Console.CursorLeft > int.MinValue) return true;
            }
            catch { }

            return false;
        }

        [STAThread]
        protected static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs ars) =>
            {
                if (ConsoleAtached())
                {
                    Console.WriteLine(((Exception)ars.ExceptionObject).ToString());
                }
                else
                {
                    MessageBox.Show(((Exception)ars.ExceptionObject).ToString(), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                Environment.Exit(0);
            };

            if (args.Length == 0 && Helper.OnInstallPath) args = new string[] { "help" };

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
                Installer.Install();

                MessageBox.Show($"CRUN v{programVersion} installed successfully", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static bool PathsEquals(string path1, string path2) => StringComparer.InvariantCultureIgnoreCase.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2));
    }
}