using CRUNInstaller.Nat;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace CRUNInstaller.HttpServer
{
    internal static class Server
    {
        public static HttpListener listener;

        public static string token;

        private static readonly ushort[] defaultPorts = [51213, 61213];

        public static void Start(ushort[] ports = null, bool safe = true)
        {
            ServicePointManager.DefaultConnectionLimit = 1000;

            listener = new HttpListener();

            foreach (var port in ports ?? defaultPorts)
            {
                listener.Prefixes.Add($"http://{(!safe ? "*" : "127.0.0.1")}:" + port + '/');
            }

            listener.Start();

            Task.Factory.StartNew(() =>
            {

                while (true)
                {
                    Thread.Sleep(20 * 1000);

                    if (HealthCheck < DateTime.Now.AddMinutes(-2))
                    {
                        Environment.Exit(0);
                    }
                }
            });

            Console.WriteLine("Listening for connections");

            // Handle requests
            Task listenTask = HandleIncomingConnections();

            listenTask.GetAwaiter().GetResult();

            listener.Close();
        }

        private static DateTime HealthCheck = DateTime.Now;
        private static bool runServer = true;
        public static async Task HandleIncomingConnections()
        {
            while (runServer)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();

                if (!runServer) break;

                try
                {
                    HttpListenerRequest req = ctx.Request;
                    HttpListenerResponse res = ctx.Response;

                    if (!string.Equals(req.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase) && req.Headers.GetValues("authorization")?.FirstOrDefault() != token)
                    {
                        res.Close();
                        continue;
                    }

                    Task.Factory.StartNew(() => HandleRequest(req, res));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
        }
        /*private static HMACMD5 hash = new HMACMD5(Encoding.UTF8.GetBytes("eeee"));
        private static string EncodePath(string path)
        {
            var splited = path.Split('/');

            var result = string.Join("/", splited.Select(e => e.Length > 0 ? BitConverter.ToString(Encoding.UTF8.GetBytes(e)) : ""));

            Console.WriteLine("from " + path + " to " + result);
            return result;
        }*/

        private const string PathNotFound = "Path not found";
        private static async void HandleRequest(HttpListenerRequest req, HttpListenerResponse res)
        {
            try
            {
                if (!req.IsLocal)
                {
                    res.StatusCode = 418;
                    res.Close();
                    return;
                }

#if DEBUG
                Console.WriteLine("Request: " + req.HttpMethod + " " + req.Url.ToString());
#endif

                res.AddHeader("Access-Control-Allow-Origin", "*");
                res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                res.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (string.Equals(req.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))    
                {
                    res.StatusCode = 200;
                    res.Close();
                    return;
                }
                else if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/close")) runServer = false;

                NameValueCollection query = new NameValueCollection();

                foreach (string key in req.QueryString.AllKeys) query[key] = HttpUtility.UrlDecode( Environment.ExpandEnvironmentVariables(req.QueryString[key]));

                //var query = req.QueryString.AllKeys.ToDictionary(k => k, k => Environment.ExpandEnvironmentVariables(req.QueryString[k]));

                string path = query["path"];
                string uriLowered = req.Url.LocalPath.ToLowerInvariant();

                switch (uriLowered)
                {
                    case "/gcd":
                        res.Return(Directory.GetCurrentDirectory());
                        break;

                    case "/scd":
                        Directory.SetCurrentDirectory(path);
                        res.Return();
                        break;

                    case "/read":
                        bool base64 = bool.TryParse(query["base64"], out bool result) && result;

                        if (base64) 
                            res.Return(Convert.ToBase64String(File.ReadAllBytes(path)));
                        else 
                            res.Return(File.ReadAllText(path));
                        break;

                    case "/list":
                        var pattern = query["pattern"];

                        if (!string.IsNullOrEmpty(pattern))
                        {
                            res.Return(string.Join("\n", Directory.EnumerateDirectories(path, pattern).Select(dir => dir + '\\').Concat(Directory.EnumerateFiles(path, pattern))));

                        }
                        else
                        {
                            res.Return(string.Join("\n", Directory.EnumerateDirectories(path).Select(dir => dir + '\\').Concat(Directory.EnumerateFiles(path))));
                        }
                        break;

                    case "/exist":
                        res.Return((File.Exists(path) || Directory.Exists(path)).ToString());
                        break;

                    case "/move":
                        if (path[path.Length - 1] == '\\')
                        {
                            Directory.Move(path, query["new"]);
                        }
                        else
                        {
                            File.Move(path, query["new"]);
                        }
                        
                        res.Return();
                        break;

                    case "/write":
                        req.InputStream.CopyTo(File.OpenWrite(path));
                        res.Return();
                        break;

                    case "/download":
                        (await Program.client.GetStreamAsync(query["url"])).CopyTo(File.OpenWrite(path));
                        res.Return();
                        break;

                    case "/attributes":
                        if (!Directory.Exists(path) && !File.Exists(path))
                        {
                            res.StatusCode = 401;
                            res.Return(PathNotFound);
                        }
                        else if (path[path.Length - 1] == '\\')
                        {
                            res.Return(new DirectoryInfo(path).Attributes.ToString());
                        }
                        else
                        {
                            res.Return(File.GetAttributes(path).ToString());
                        }

                        break;

                    case "/delete":
                        if (!Directory.Exists(path) && !File.Exists(path))
                        {
                            res.StatusCode = 401;
                            res.Return(PathNotFound);
                        }
                        else if (path[path.Length - 1] == '\\')
                        {
                            Directory.Delete(path, bool.TryParse(query["recursive"], out bool recursive) && recursive);
                        }
                        else
                        {
                            File.Delete(path);
                        }

                        res.Return(true.ToString());
                        break;

                    case "/plist":
                        res.Return(string.Join("\n", Process.GetProcesses().Select(p => p.ProcessName + '|' + p.Id)));
                        break;

                    case "/pkill":
                        string processName = query["name"];

                        List<Process> tarjetProcesses = [];

                        if (processName != null)
                        {
                            foreach (var procName in processName.Split('|'))
                            {
                                foreach (var proc in Process.GetProcessesByName(procName.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ? procName.Substring(0, procName.Length - 4) : procName))
                                {
                                    tarjetProcesses.Add(proc);
                                }
                            }
                        }
                        else if (int.TryParse(query["pid"], out int pid) && pid > 0) tarjetProcesses.Add(Process.GetProcessById(pid));

                        foreach (var proc in tarjetProcesses) proc.Kill();
                        res.Return(tarjetProcesses.Count.ToString());
                        break;

                    case "/run":
                        bool hide = ArgsProcessor.ParseBool(query["hide"]);
                        bool async = ArgsProcessor.ParseBool(query["async"]);

                        if (Helper.IsLink(path)) path = Helper.DownloadFile(path);

                        ProcessStartInfo info = new ProcessStartInfo()
                        {
                            FileName = path,
                            Arguments = query["args"],

                            CreateNoWindow = hide,
                            UseShellExecute = async && ArgsProcessor.ParseBool(query["shell"], false),
                            RedirectStandardOutput = !async,

                            Verb = ArgsProcessor.ParseBool(query["uac"]) ? "runas" : "",
                            WindowStyle = hide ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                        };

                        Process p = Process.Start(info);

                        if (async)
                        {
                            res.Return();
                        }
                        else
                        {
                            using (var sw = new StreamWriter(res.OutputStream))
                            {
                                while (!p.StandardOutput.EndOfStream)
                                {

                                    sw.WriteLine(p.StandardOutput.ReadLine());
                                }
                            }

                            p.WaitForExit();
                            res.OutputStream.Close();
                        }
                        break;

                    case "/unzip":
                        new ZipArchive(await Program.client.GetStreamAsync(query["url"])).ExtractToDirectory(path);

                        res.Return();
                        break;

                    case "/health":
                        HealthCheck = DateTime.Now;

                        res.Return();
                        break;

                    case "/service/start":
                        new ServiceController(path).Start(query["args"].Split('|'));
                        res.Return();
                        break;
                    case "/service/stop":
                        new ServiceController(path).Stop();
                        res.Return();
                        break;
                    case "/service/restart":
                        var controller = new ServiceController(path);
                        controller.Stop();
                        controller.Start(query["args"].Split('|'));
                        res.Return();
                        break;
                  /*  case "/service/type":
                        res.Return(new ServiceController(path).ServiceType.ToString());
                        break;
                    case "/service/status":
                        res.Return(new ServiceController(path).Status.ToString());
                        break;
                    case "/service/startType":
                        res.Return(new ServiceController(path).StartType.ToString());
                        break;
                    case "/service/handle":
                        res.Return(new ServiceController(path).ServiceHandle.ToString());
                        break;*/
                    case "/service/info":
                        var service = new ServiceController(path);

                        res.Return(service.ServiceType + '|' + service.Status.ToString() + '|' + service.StartType + '|' + service.ServiceHandle);
                        break;
                    case "/service/list":
                        var content = ServiceController.GetServices().Select(s =>
                        s.DisplayName + '|' + s.ServiceType + '|' + s.StartType + '|' + s.Status);

                        res.Return(string.Join("\n", content));
                        break;

                    case "/env":
                        var sb = new StringBuilder();
                        foreach(DictionaryEntry env in Environment.GetEnvironmentVariables())
                        {
                            sb.AppendLine(env.Key + "=" + env.Value);
                        }
                        res.Return(sb.ToString());
                        break;

                    case "/registry/get":
                        {
                            var subKeyPath = path.Contains("\\") ? path.Substring(path.IndexOf('\\') + 1) : string.Empty;
                            var hive = Helper.ParseHive(path.Split('\\')[0]);

                            using (var regKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKeyPath))
                            {
                                var val = regKey?.GetValue(query["key"]);

                                if (val is byte[])
                                {
                                    res.Return(BitConverter.ToString((byte[])val));
                                }
                                else
                                {
                                    res.Return(val?.ToString() ?? string.Empty);
                                }
                            }
                            break;
                        }

                    case "/registry/set":
                        {
                            var subKeyPath = path.Contains("\\") ? path.Substring(path.IndexOf('\\') + 1) : string.Empty;
                            var hive = Helper.ParseHive(path.Split('\\')[0]);
                            using (var regKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default).CreateSubKey(subKeyPath))
                            {
                                var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), query["kind"], true);
                                regKey.SetValue(query["key"], query["value"], kind);
                                res.Return();
                            }
                            break;
                        }

                    case "/registry/delete":
                        {
                            var subKeyPath = path.Contains("\\") ? path.Substring(path.IndexOf('\\') + 1) : string.Empty;
                            var hive = Helper.ParseHive(path.Split('\\')[0]);
                            using (var regKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKeyPath, writable: true))
                            {
                                regKey?.DeleteValue(query["key"], throwOnMissingValue: false);
                                res.Return();
                            }
                            break;
                        }

                    case "/registry/list":
                        {
                            var subKeyPath = path.Contains("\\") ? path.Substring(path.IndexOf('\\') + 1) : string.Empty;
                            var hive = Helper.ParseHive(path.Split('\\')[0]);
                            using (var regKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKeyPath, writable: false))
                            {
                                res.Return(string.Join("\n", regKey.GetValueNames().Select(v => v + '=' + regKey.GetValue(v)).Concat(regKey.GetSubKeyNames().Select(skn => skn + '\\'))));
                            }
                            break;
                        }

                    case "/management/query":
                        var oquery = path.StartsWith("win32_", StringComparison.OrdinalIgnoreCase) ? path : "Win32_" + path;
                        string formattedResult;

                        using (var searcher = new ManagementObjectSearcher("select * from " + oquery))
                        using (var results = searcher.Get())
                        using (var wmiObject = results.Cast<ManagementObject>().FirstOrDefault())
                        {
                            if (wmiObject == null)
                            {
                                formattedResult = $"No WMI object found for query: 'select * from {oquery}'";
                            }
                            else
                            {
                                var formattedProperties = wmiObject.Properties
                                    .Cast<PropertyData>()
                                    .Select(prop => {
                                        string key = prop.Name;
                                        object value = prop.Value;
                                        string valueString;

                                        if (value == null)
                                        {
                                            valueString = "null";
                                        }
                                        else if (prop.IsArray && value is byte[] byteArray)
                                        {
                                            valueString = string.Concat(byteArray.Select(b => b.ToString("X2")));
                                        }
                                        else if (prop.IsArray && value is Array arr)
                                        {
                                            var elements = arr.Cast<object>()
                                                              .Select(el => el?.ToString() ?? "null");
                                            valueString = $"[{string.Join(", ", elements)}]";
                                        }
                                        else
                                        {
                                            valueString = value.ToString();
                                        }

                                        return $"{key}={valueString}";
                                    });

                                // Join all the formatted "key=value" strings with newlines
                                formattedResult = string.Join(Environment.NewLine, formattedProperties);
                            }
                        } // wmiObject (if not null), results, and searcher are automatically disposed here

                        res.Return(formattedResult);
                        break;

                    case "/dllinvoke":
                       var returned = DynamicInvoker.InvokeSingle(query["dll"], query["method"], query["params"], System.Runtime.InteropServices.CallingConvention.Winapi, DynamicInvoker.TypeMap[query["returnType"]]);

                        res.Return(returned);
                        break;

                    case "/nat/list":
                        {
                            res.Return(NatManager.GetMappings());
                            break;
                        }
                    case "/nat/map":
                        {
                            var port = query["port"];
                            var publicPort = query["publicPort"];
                            var privatePort = query["privatePort"];

                            res.Return(NatManager.Map(
                                query["protocol"],
                                publicPort ?? privatePort ?? port,
                                privatePort ?? publicPort ?? port,
                                query["lifetime"],
                                query["description"]));
                            break;
                        }

                    case "/nat/unmap":
                        {
                            var port = query["port"];
                            var publicPort = query["publicPort"];
                            var privatePort = query["privatePort"];

                            res.Return(NatManager.UnMap(
                                query["protocol"],
                                publicPort ?? privatePort ?? port,
                                privatePort ?? publicPort ?? port,
                                query["lifetime"],
                                query["description"]));
                            break;
                        }

                    default:
                        res.Return("Not found " + req.Url.PathAndQuery.ToString());
                        break;
                }

            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                res.Return(ex.ToString());
            }

            try
            {
                res.OutputStream.Close();
            }
            catch { }
        }

    }
}