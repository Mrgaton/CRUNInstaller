using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CRUNInstaller
{
    internal static class Program
    {
        [DllImport("kernel32")] private static extern bool AttachConsole(int pid);

        public static readonly string remoteRepo = Encoding.UTF8.GetString([0X68, 0X74, 0X74, 0X70, 0X73, 0X3A, 0X2F, 0X2F, 0X67, 0X69, 0X74, 0X68, 0X75, 0X62, 0X2E, 0X63, 0X6F, 0X6D, 0X2F, 0X4D, 0X72, 0X67, 0X61, 0X74, 0X6F, 0X6E, 0X2F, 0X43, 0X52, 0X55, 0X4E, 0X49, 0X6E, 0X73, 0X74, 0X61, 0X6C, 0X6C, 0X65, 0X72, 0X2F]);

        public static readonly Assembly currentAssembly = Assembly.GetExecutingAssembly();

        public static readonly Version programVersion = currentAssembly.GetName().Version;

        public static readonly string programProduct = Application.ProductName;
        public static readonly string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), programProduct.ToLower() + ".exe");

        public static readonly string trustedTokensPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), programProduct);

        public static readonly WebClient wc = new WebClient()
        {
            Headers = {
                { "Referer", "Crun Installer" },
                { "User-Agent", "Crun V" + programVersion }
            }
        };

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs ars) =>
            {
                string exceptionString = ((Exception)ars.ExceptionObject).ToString();

                if (Helper.ConsoleAtached())
                {
                    Console.WriteLine(exceptionString);
                }
                else
                {
                    MessageBox.Show(exceptionString, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                Environment.Exit(0);
            };

            if (args.Length == 0 && Helper.OnInstallPath) args = ["help"];

            if (args.Length > 0)
            {
                AttachConsole(-1);
                ArgsProcessor.ProcessArguments(args);
                return;
            }

            Process.Start(new ProcessStartInfo()
            {
                FileName = currentAssembly.Location,
                Arguments = "Install",
                Verb = "runas",
            });

            Environment.Exit(0);
        }
    }
}