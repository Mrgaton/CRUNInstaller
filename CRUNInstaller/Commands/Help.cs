using System;

namespace CRUNInstaller.Commands
{
    internal class Help
    {
        public static void ShowHelp()
        {
            Console.WriteLine("CRUN v" + Program.programVersion.ToString() + " - 2023");

            Console.WriteLine();
            Console.WriteLine("Ussage: crun run [ShowWindow] [UseShellExecute] [FileName] [Arguments]");
            Console.WriteLine("Ussage: crun cmd [ShowWindow] [CloseOnEnd] [Command\\Batch URI]");
            Console.WriteLine("Ussage: crun ps1 [ShowWindow] [UseShellExecute] [Command\\Powershell Script URI]");
            Console.WriteLine();
        }
    }
}