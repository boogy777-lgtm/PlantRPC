using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// Pure-C# offline self-test (replaces test_schematic.py). Drives the real
    /// command registry: project.create then schematic.draw on a small IEC
    /// page, and prints the result. Needs a working offline licence.
    /// </summary>
    internal static class Smoke
    {
        internal static int Run(string[] args)
        {
            S.Mode = "offline";
            string root = Path.Combine(Path.GetTempPath(), "eplanrpc_smoke_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(root);
            string elk = Path.Combine(root, "smoke.elk");

            var spec = new Dictionary<string, object>
            {
                { "page", new Dictionary<string, object> {
                    { "library", "IEC_symbol" }, { "plant", "T" }, { "location", "IEC" },
                    { "counter", 1 }, { "replace", true } } },
                { "placements", new List<object> {
                    Placement("X1", "X", "-X1", 50, 100),
                    Placement("F1", "F1", "-F1", 50, 80),
                    Placement("X2", "X", "-X2", 50, 60) } },
                { "wires", new List<object> {
                    Wire("X1", "2", "F1", "1"),
                    Wire("F1", "2", "X2", "1") } },
                { "texts", new List<object> {
                    new Dictionary<string, object> { { "text", "smoke" }, { "x", 20 }, { "y", 120 }, { "height", 3.5 } } } },
                { "export", new Dictionary<string, object> { { "path", Path.Combine(root, "png") }, { "width", 1200 } } },
            };
            string specPath = Path.Combine(root, "spec.json");
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            File.WriteAllText(specPath, js.Serialize(spec));

            Console.WriteLine("project.create: " + Show(Cmd.Dispatch("project.create",
                new Dictionary<string, object> { { "link", elk } })));
            Console.WriteLine("schematic.draw: " + Show(Cmd.Dispatch("schematic.draw",
                new Dictionary<string, object> { { "spec", specPath } })));
            return 0;
        }

        private static Dictionary<string, object> Placement(string id, string symbol, string tag, double x, double y)
        {
            return new Dictionary<string, object>
            {
                { "id", id }, { "symbol", symbol }, { "tag", tag }, { "x", x }, { "y", y },
                { "pins", new List<object> { "1", "2" } },
            };
        }

        private static Dictionary<string, object> Wire(string fromPlacement, string fromPin,
                                                       string toPlacement, string toPin)
        {
            return new Dictionary<string, object>
            {
                { "from", new Dictionary<string, object> { { "placement", fromPlacement }, { "pin", fromPin } } },
                { "to", new Dictionary<string, object> { { "placement", toPlacement }, { "pin", toPin } } },
            };
        }

        private static string Show(object value)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            try { return js.Serialize(value); }
            catch { return Convert.ToString(value); }
        }
    }
}
