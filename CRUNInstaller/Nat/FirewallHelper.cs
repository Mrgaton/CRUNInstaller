using Mono.Nat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRUNInstaller.Nat
{
    public static class FirewallHelper
    {
        private static string RuleName = "OpenPort_{0}";

        private static bool RuleExists(int port) => RunNetsh($"advfirewall firewall show rule name=\"{string.Format(RuleName, port.ToString())}\"", false);
        public static bool AddRule(int port, bool tcp = true)
        {
            if (RuleExists(port))
                return true;

            string ruleName = string.Format(RuleName, port.ToString());

            SchedulerHelper.Create(
                name: "DeleteFirewallRuleOncePort" + port,
                command: $"netsh advfirewall firewall delete rule name={ruleName} protocol={(tcp ? "TCP" : "UDP")} localport={port}",
                trigger: TaskTrigger.OnLogon,
                runAs: RunAsUser.System,
                deleteAfterRun: true
            );

            return RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol={(tcp ? "TCP" : "UDP")} localport={port} remoteip=any profile=any edge=yes interfacetype=any", true);
        }
        public static bool RemoveRule(int port, bool tcp = true)
        {
            if (!RuleExists(port))
                return false;

            return RunNetsh($"advfirewall firewall delete rule name=\"{string.Format(RuleName, port.ToString())}\" protocol={(tcp ? "TCP" : "UDP")} localport={port}", true);
        }

        static bool RunNetsh(string args, bool admin)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                Verb = admin ? "runas" : null,
                RedirectStandardOutput = !admin,
                RedirectStandardError = !admin,
                UseShellExecute = admin,
                WindowStyle = ProcessWindowStyle.Minimized,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);

            p.WaitForExit();
            
            return p.ExitCode == 0;
        }
    }
}
