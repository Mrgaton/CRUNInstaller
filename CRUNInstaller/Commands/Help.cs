using System;

namespace CRUNInstaller.Commands
{
    internal static class Help
    {
        public static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("CRUN v" + Program.programVersion.ToString() + " - 2024");

            ConsoleColor oldColor = Console.ForegroundColor;

            Console.WriteLine();
            Console.Write("To see examples please visit: ");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Program.remoteRepo);
            Console.ForegroundColor = oldColor;
        }
    }
}