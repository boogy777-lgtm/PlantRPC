using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EplanRpc
{
    /// <summary>
    /// Long-lived TCP server. One JSON object (or compact command) per line in,
    /// one JSON line per request out. Compact and JSON are both parsed by
    /// Protocol; the response is always JSON: {"ok":true,"result":...} or
    /// {"ok":false,"error":...}.
    /// </summary>
    internal static class Server
    {
        internal static int Run(int port, bool initOffline)
        {
            if (initOffline)
                Offline.Init();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(16);
            Console.WriteLine("EplanRpc listening on 127.0.0.1:" + port + " mode=" + S.Mode);
            Console.Out.Flush();

            bool stop = false;
            while (!stop)
            {
                using (var client = listener.AcceptTcpClient())
                {
                    client.NoDelay = true;
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            string resp = Handle(line, ref stop);
                            writer.WriteLine(resp);
                            if (stop) break;
                        }
                    }
                }
            }
            listener.Stop();
            return 0;
        }

        private static string Handle(string line, ref bool stop)
        {
            var resp = new Dictionary<string, object>();
            string cmd = "?";
            try
            {
                var req = Protocol.Parse(line);
                cmd = req.Cmd;
                object result = Cmd.Dispatch(cmd, req.Args);
                if (cmd == "shutdown") stop = true;
                resp["ok"] = true;
                resp["result"] = result;
            }
            catch (Exception ex)
            {
                resp["ok"] = false;
                resp["error"] = ex.GetType().Name + ": " + ex.Message;
            }
            resp["cmd"] = cmd;
            return Protocol.Serialize(resp);
        }
    }
}
