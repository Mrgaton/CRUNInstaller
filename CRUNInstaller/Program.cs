using CRUNInstaller.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal class Program
    {
        public static readonly WebClient wc = new WebClient();

        public static readonly string remoteRepo = Encoding.UTF8.GetString(new byte[] { 0X68, 0X74, 0X74, 0X70, 0X73, 0X3A, 0X2F, 0X2F, 0X67, 0X69, 0X74, 0X68, 0X75, 0X62, 0X2E, 0X63, 0X6F, 0X6D, 0X2F, 0X4D, 0X72, 0X67, 0X61, 0X74, 0X6F, 0X6E, 0X2F, 0X43, 0X52, 0X55, 0X4E, 0X49, 0X6E, 0X73, 0X74, 0X61, 0X6C, 0X6C, 0X65, 0X72, 0X2F });

        public static readonly Assembly currentAssembly = Assembly.GetExecutingAssembly();
        public static readonly Version programVersion = currentAssembly.GetName().Version;

        public static readonly string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "crun.exe");

        [DllImport("kernel32")] private static extern bool AttachConsole(int pid);

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

            if (args.Length == 0 && Helper.OnInstallPath) args = new[] { "help" };

            if (args.Length > 0)
            {
                if (args[0].ToLower().StartsWith("crun://",StringComparison.InvariantCultureIgnoreCase)) args = args[0].Split('/').Skip(2).Select(arg => Uri.UnescapeDataString(arg)).ToArray();

                AttachConsole(-1);

                ArgsProcessor.ProcessArguments(args);
                return;
            }

            if (args.Length == 0)
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = currentAssembly.Location,
                    Arguments = "Install",
                    Verb = "runas",
                });

                Environment.Exit(0);
            }
        }

        public static bool PathsEquals(string path1, string path2) => StringComparer.InvariantCultureIgnoreCase.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2));
    }
}