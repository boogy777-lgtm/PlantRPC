using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    internal static class Program
    {
        internal const string PlatformBin = @"C:\Program Files\EPLAN\Platform\2026.0.3\Bin";
        internal const string SurfaceDefault =
            @"C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_api_surface\EPLAN_API_SURFACE.json";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Remoting.EnsureResolver();
                string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "probe";
                switch (cmd)
                {
                    case "probe":
                        return Remoting.Probe();
                    case "serve":
                        return Serve(args);
                    case "dump":
                        return DumpCommands(args);
                    case "docs":
                        return Docs.Run(args);
                    case "smoke":
                        return Smoke.Run(args);
                    default:
                        Console.Error.WriteLine("usage: EplanRpc probe|serve|dump|docs|smoke");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FATAL: " + ex);
                return 1;
            }
        }

        private static int Serve(string[] args)
        {
            string mode = "offline";
            int port = 45455;
            string surface = SurfaceDefault;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--mode" && i + 1 < args.Length) mode = args[++i].ToLowerInvariant();
                else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
                else if (args[i] == "--surface" && i + 1 < args.Length) surface = args[++i];
            }
            if (mode != "offline" && mode != "online")
            {
                Console.Error.WriteLine("mode must be offline|online");
                return 2;
            }

            S.Mode = mode;
            S.Surface = ApiSurface.Load(surface);

            if (mode == "offline")
            {
                // EplApplication.Init happens lazily on the first offline command
                // so `serve --mode offline` can start without a license.
                return Server.Run(port, initOffline: false);
            }
            else
            {
                // online: no offline init; remoting.connect drives everything.
                return Server.Run(port, initOffline: false);
            }
        }

        private static int DumpCommands(string[] args)
        {
            string outPath = args.Length > 1 ? args[1] : Path.Combine(S.OutDir, "commands.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var doc = new Dictionary<string, object>
            {
                { "commands", Cmd.ListCommands() },
                { "regions", Cmd.ListRegions() },
            };
            File.WriteAllText(outPath, js.Serialize(doc));
            Console.WriteLine("Wrote " + outPath);
            return 0;
        }
    }
}
