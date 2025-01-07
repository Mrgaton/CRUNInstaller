using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CRUNInstaller.HttpServer
{
    internal class Server
    {
        public static HttpListener listener;

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

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse res = ctx.Response;

                Task.Factory.StartNew(() => HandleRequest(req, res));

            }
        }
        private static HMACMD5 hash = new HMACMD5(Encoding.UTF8.GetBytes("eeee"));
        private  string EncodePath(string path) {
            var splited = path.Split('/');

            var result = string.Join("/", splited.Select(e => e.Length > 0 ? BitConverter.ToString(Encoding.UTF8.GetBytes(e)) : ""));

            Console.WriteLine("from " + path + " to " + result);
            return result;
            }
        private static async void HandleRequest(HttpListenerRequest req, HttpListenerResponse res)
        {
            try
            {
                if (!req.IsLocal)
                {
                    res.Close();
                    return;
                }

#if DEBUG
                Console.WriteLine("Request");
                Console.WriteLine(req.Url.ToString());
                Console.WriteLine(req.Url.AbsolutePath);
                Console.WriteLine(req.Url.LocalPath);
                Console.WriteLine(req.HttpMethod);
                Console.WriteLine(req.UserHostName);
                Console.WriteLine(req.UserAgent);
                Console.WriteLine();
#endif

                res.AddHeader("Access-Control-Allow-Origin", "*");
                res.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

                if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/close")) runServer = false;

                NameValueCollection query = new NameValueCollection();

                foreach (string key in req.QueryString.AllKeys) query[key] = Environment.ExpandEnvironmentVariables(req.QueryString[key]);

                //var query = req.QueryString.AllKeys.ToDictionary(k => k, k => Environment.ExpandEnvironmentVariables(req.QueryString[k]));

                string path = query["path"];

                switch (req.Url.LocalPath.ToLowerInvariant())
                {
                    case ("/gcd"):
                        res.Return(Directory.GetCurrentDirectory());
                        break;

                    case ("/scd"):
                        Directory.SetCurrentDirectory(path);
                        res.Return("OK");
                        break;

                    case ("/read"):
                        bool base64 = bool.TryParse(query["base64"], out bool result) && result;

                        if (base64) res.Return(Convert.ToBase64String(File.ReadAllBytes(path)));
                        else res.Return(File.ReadAllText(path));
                        break;

                    case "/list":
                        res.Return(string.Join("\n", Directory.EnumerateDirectories(path).Select(dir => dir + '\\').Concat(Directory.EnumerateFiles(path))));
                        break;

                    case "/exist":
                        res.Return((File.Exists(path) || Directory.Exists(path)).ToString());
                        break;

                    case "/write":
                        try
                        {
                            req.InputStream.CopyTo(File.OpenWrite(path));

                            res.Return("true");
                        }
                        catch (Exception ex)
                        {
                            res.Return(ex.ToString());
                        }
                        break;

                    case "/attributes":
                        res.Return(((int)File.GetAttributes(path)).ToString());
                        break;

                    case "/delete":
                        if (!Directory.Exists(path) && !File.Exists(path))
                        {
                            res.StatusCode = 404;
                            res.Return("Path does not exist");
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
                        res.Return(string.Join("\n", Process.GetProcesses().Select(p => p.ProcessName + ':' + p.Id)));
                        break;

                    case "/pkill":
                        string processName = query["name"];

                        List<Process> tarjetProcesses = new List<Process>();

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
                        else if (int.TryParse(query["id"], out int pid) && pid > 0) tarjetProcesses.Add(Process.GetProcessById(pid));

                        foreach (var proc in tarjetProcesses) proc.Kill();

                        res.Return((tarjetProcesses.Count > 0).ToString());
                        break;

                    case "/run":
                        bool hide = ArgsProcessor.ParseBool(query["hide"]);
                        bool async = ArgsProcessor.ParseBool(query["async"]);

                        ProcessStartInfo info = new ProcessStartInfo()
                        {
                            FileName = query["file"],
                            Arguments = query["args"],

                            CreateNoWindow = hide,
                            UseShellExecute = async && ArgsProcessor.ParseBool(query["shell"], false),
                            RedirectStandardOutput = !async,

                            Verb = ArgsProcessor.ParseBool(query["admin"]) ? "runas" : "",
                            WindowStyle = hide ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                        };

                        Process p = Process.Start(info);

                        if (async)
                        {
                            res.Return("OK");
                        }
                        else
                        {
                            StringBuilder output = new StringBuilder();

                            while (!p.StandardOutput.EndOfStream)
                            {
                                output.AppendLine(p.StandardOutput.ReadLine());
                            }

                            p.WaitForExit();

                            res.Return(output.ToString());
                        }
                        break;

                    case "/extract":
                        new ZipArchive(await Program.client.GetStreamAsync(query["url"])).ExtractToDirectory(path);

                        res.Return("DONE");
                        break;

                    case "/health":
                        HealthCheck = DateTime.Now;

                        res.Return("OK");
                        break;

                    default:
                        res.Return("Not found /" + req.Url.PathAndQuery.ToString());
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