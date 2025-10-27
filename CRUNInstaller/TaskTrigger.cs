using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CRUNInstaller.Nat
{
    public enum TaskTrigger
    {
        OnStart,
        OnLogon,
        Daily,
        Weekly,
        Monthly,
        OnIdle
    }

    public enum RunAsUser
    {
        System,
        CurrentUser,
        Administrators
    }

    public static class EnumParser
    {
        public static T Parse<T>(string value, bool ignoreCase = true) where T : struct, Enum
        {
            return Enum.TryParse(value, ignoreCase, out T result) ? result : throw new ArgumentException($"Invalid {typeof(T).Name}: '{value}'");
        }
    }

    public static class SchedulerHelper
    {
        public static (bool Success, string Output) Create(string name, string command, TaskTrigger trigger, RunAsUser runAs = RunAsUser.System, bool deleteAfterRun = false, string startTime = null)
        {
            if (command.Contains("\""))
                throw new Exception("Command cant have quotes :c");

            var arguments = new List<string>
            {
                "/Create",
                "/F",
                $"/TN \"{name}\"",
                $"/TR \"{command}\""
            };

            arguments.Add(trigger switch
            {
                TaskTrigger.OnStart => "/SC ONSTART",
                TaskTrigger.OnLogon => "/SC ONLOGON",
                TaskTrigger.OnIdle => "/SC ONIDLE",
                TaskTrigger.Daily => $"/SC DAILY{(startTime != null ? " /ST " + startTime : string.Empty)}",
                TaskTrigger.Weekly => $"/SC WEEKLY{(startTime != null ? " /ST " + startTime : string.Empty)}",
                TaskTrigger.Monthly => $"/SC MONTHLY{(startTime != null ? " /ST " + startTime : string.Empty)}",
                _ => throw new ArgumentOutOfRangeException(nameof(trigger))
            });

            arguments.Add(runAs switch
            {
                RunAsUser.System => "/RU SYSTEM",
                RunAsUser.Administrators => "/RU \"BUILTIN\\Administrators\"",
                _ => string.Empty
            });

            if (deleteAfterRun)
            {
                arguments.Add("/Z");
            }

            return RunProcess("schtasks", string.Join(" ", arguments));
        }

        public static (bool Success, string Output) Delete(string name) => RunProcess("schtasks", $"/Delete /TN \"{name}\" /F");

        public static bool Exists(string name) => RunProcess("schtasks", $"/Query /TN \"{name}\"").Success;

        public static (bool Success, string Output) RunNow(string name) => RunProcess("schtasks", $"/Run /TN \"{name}\"");

        public static (bool Success, string Output) Enable(string name) => RunProcess("schtasks", $"/Change /TN \"{name}\" /ENABLE");

        public static (bool Success, string Output) Disable(string name) => RunProcess("schtasks", $"/Change /TN \"{name}\" /DISABLE");

        private static (bool Success, string Output) RunProcess(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using var process = new Process { StartInfo = psi };
                var outputBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) => outputBuilder.AppendLine(e.Data);
                process.ErrorDataReceived += (s, e) => outputBuilder.AppendLine(e.Data);

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                var output = outputBuilder.ToString().Trim();

                return (process.ExitCode == 0, output);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}