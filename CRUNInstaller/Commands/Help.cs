using System;
using System.Reflection;

namespace CRUNInstaller.Commands
{
    internal class Help
    {
        public static void ShowHelp()
        {
            Console.WriteLine("CRUN v" + Program.programVersion.ToString() + " - 2023");

            ConsoleColor oldColor = Console.ForegroundColor;

            Console.WriteLine();
            Console.Write("To see examples please visit: ");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Program.remoteRepo);
            Console.WriteLine();
            Console.ForegroundColor = oldColor;
        }
    }
}