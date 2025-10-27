using Mono.Nat;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;

namespace CRUNInstaller.Nat
{
    internal static class NatManager
    {
        private static INatDevice device = null;

        private static void Check()
        {
            if (device == null && !Init())
                throw new Exception("Could not find NAT device within 10 seconds");
        }
        public static string GetMappings()
        {
            Check();

            Console.WriteLine("=== Active Port Mappings ===\n");

            var existing = device.GetAllMappingsAsync().Result;

            return PrintMappings(existing);
        }
      
        public static string Map(string protocol, string publicPort, string privatePort, string lifeTime, string description = "CrunHelper mapping")
        {
            Check();

            var proto = ParseProtocol(protocol);

            var publPort = int.Parse(publicPort);
            var privPort = int.Parse(privatePort);

            var newMap = new Mapping(
                proto,
                publicPort: publPort,
                privatePort: privPort,
                lifetime: int.Parse(lifeTime),
                description: description
            );

            Console.WriteLine($"Adding {protocol} mapping {publicPort} > {privatePort}...");
            device.CreatePortMapAsync(newMap).Wait();

            var afterAdd = device.GetAllMappingsAsync().Result;
            var afterAddedString = PrintMappings(afterAdd);

            FirewallHelper.AddRule(privPort, proto == Protocol.Tcp);

            Console.WriteLine("=== After Adding ===\n" + afterAddedString);
            return afterAddedString;
        }
        public static bool UnMap(string? protocol = null, string? publicPort = null, string? privatePort = null, string? lifeTime = "0", string? description = null)
        {
            Check();

            var existing = device.GetAllMappingsAsync().Result;

            Protocol p = Protocol.Tcp;

            if (protocol != null)
                p = ParseProtocol(protocol);

            var parsedPublicPort = int.Parse(publicPort);
            var parsedPrivatePort = int.Parse(privatePort);

            var filtered = existing.Where(m =>
            {
                if (protocol != null && m.Protocol == p)
                    return true;

                if (publicPort != null && m.PublicPort == parsedPublicPort)
                    return true;

                if (privatePort != null && m.PrivatePort == parsedPrivatePort)
                    return true;

                if (lifeTime != null && m.Lifetime == int.Parse(lifeTime))
                    return true;

                if (description != null && string.Equals(m.Description, description,StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            });

            FirewallHelper.RemoveRule(parsedPrivatePort, p == Protocol.Tcp);

            if (!filtered.Any())
            {
                Console.WriteLine("Could not found any mapping with the provided filters");
                return false;
            }

            Console.WriteLine($"Removing the {(filtered.Count() > 1 ? "mappings" : "mapping")}...");

            foreach (var m in filtered)
            {
                device.DeletePortMapAsync(m).Wait();
            }

            var afterRemove = device.GetAllMappingsAsync().Result;

            Console.WriteLine("=== After Removal ===\n" + PrintMappings(afterRemove));
            return true;
        }
        public static bool Init()
        {
            var tcs = new TaskCompletionSource<INatDevice>();

            NatUtility.DeviceFound += (_, e) =>
            {
                Console.WriteLine($"[Found] {e.Device.GetType().Name}");

                tcs.TrySetResult(e.Device);
            };

            NatUtility.StartDiscovery();

            if (!tcs.Task.Wait(TimeSpan.FromSeconds(15)))
            {
                Console.Error.WriteLine("No NAT device found within 10 seconds.");
                return false;
            }

            device = tcs.Task.Result;

            NatUtility.StopDiscovery();

            return true;
        }
        static string PrintMappings(IEnumerable<Mapping> maps)
        {
            if (!maps.Any())
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            foreach (var m in maps)
            {
                sb.AppendLine($"Protocol: {m.Protocol}, PublicPort: {m.PublicPort}, PrivatePort: {m.PrivatePort}, Expiration: {new DateTimeOffset(m.Expiration.ToUniversalTime()).ToUnixTimeSeconds()}, Lifetime: {m.Lifetime}, Description: {m.Description}");
            }

            return sb.ToString();
        }

        private static Protocol ParseProtocol(string protocol)
        {
            switch (protocol.Trim().ToLower())
            {
                case "tcp":
                    return Protocol.Tcp;
                case "udp":
                    return Protocol.Udp;
                default:
                    throw new Exception("Invalid protocol");
            }
        }
    }
}
