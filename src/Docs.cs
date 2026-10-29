using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// Pure-C# doc generator (replaces gen_commands.py). The command table is
    /// the single source of truth: it is dumped to commands.json and rendered
    /// into the lazy skill references so the docs cannot drift from the server.
    /// </summary>
    internal static class Docs
    {
        private static readonly string[][] RegionMap =
        {
            new[] { "system", "server state + mode", "EplApplication (Eplan.EplApi.System)" },
            new[] { "project", "create/open/list projects", "ProjectManager, Project (Eplan.EplApi.DataModel)" },
            new[] { "page", "create/select pages", "Page, PagePropertyList (Eplan.EplApi.DataModel)" },
            new[] { "symbol", "enumerate symbol libraries", "SymbolLibrary, Symbol, SymbolVariant (Eplan.EplApi.DataModel.MasterData)" },
            new[] { "function", "place/tag devices", "Function (Eplan.EplApi.DataModel)" },
            new[] { "connection", "generate/read connections", "Connection, DynamicConnectionLine (Eplan.EplApi.DataModel)" },
            new[] { "graphics", "graphic primitives (NOT electrical)", "Graphics.* (Eplan.EplApi.DataModel.Graphics)" },
            new[] { "generate", "Generate.Connections", "Generate (Eplan.EplApi.HEServices)" },
            new[] { "export", "PDF/PNG export", "Export (Eplan.EplApi.HEServices)" },
            new[] { "schematic", "draw a full page from a JSON spec (self-contained offline)", "Function, DynamicConnectionLine, Generate, Export (Eplan.EplApi.DataModel/HEServices)" },
            new[] { "action", "run EPLAN actions", "ActionManager (Eplan.EplApi.ApplicationFramework)" },
            new[] { "settings", "read/write settings", "Settings (Eplan.EplApi.Base)" },
            new[] { "masterdata", "parts database", "MDPartsManagement (Eplan.EplApi.MasterData)" },
            new[] { "remoting", "online: connect/script", "EplanRemoteClient (Eplan.EplApi.RemoteClient)" },
        };

        private sealed class Recipe
        {
            internal string Name, Mode, Note;
            internal string[] Lines;
        }

        private static readonly Recipe[] Recipes =
        {
            new Recipe {
                Name = "offline: draw the 230V->24V PSU", Mode = "offline",
                Lines = new[] {
                    "project.create link=<out>\\psu.elk",
                    "page.create name=1",
                    "function.place symbol=X tag=-X1 x=40 y=180 v=3",
                    "function.place symbol=X tag=-X2 x=40 y=160 v=3",
                    "function.place symbol=X tag=-X3 x=40 y=140 v=3",
                    "function.place symbol=F1 tag=-F1 x=100 y=180 v=1",
                    "generate.connections",
                    "export.image path=<out>\\psu.png width=1600",
                    "stats",
                },
                Note = "page name is read back (may be /1, not 1). Electrical wiring between pins uses DynamicConnectionLine; the self-contained schematic.draw does this in one command.",
            },
            new Recipe {
                Name = "online: read the project open in the GUI", Mode = "online",
                Lines = new[] {
                    "remoting.connect host=127.0.0.1 port=49152",
                    "api.invoke @project.ProjectName",
                    "api.invoke @project.ProjectLinkFilePath",
                    "remoting.disconnect",
                },
                Note = "port 49152 is the usual remoting port; discover with remoting.connect (omit port) or netstat.",
            },
            new Recipe {
                Name = "explore the 100% API surface", Mode = "any",
                Lines = new[] {
                    "api.namespaces contains=DataModel",
                    "api.types ns=Eplan.EplApi.DataModel contains=Function",
                    "api.members type=Eplan.EplApi.DataModel.Function contains=Create",
                    "api.desc Eplan.EplApi.DataModel.Function.Create",
                },
                Note = "metadata needs no EPLAN runtime; api.invoke needs a live backend.",
            },
            new Recipe {
                Name = "draw a page from a JSON spec (self-contained, offline)", Mode = "offline",
                Lines = new[] {
                    "project.create link=<out>\\page.elk template=GOST_bas001",
                    "schematic.draw spec=specs\\do_feedback.json",
                },
                Note = "One server command renders placements + real DynamicConnectionLine wires on actual pins + texts + generate.connections + PNG. No C# template, no Python, no remoting.script. Wire endpoints are {placement,pin} refs or [x,y]; pins resolve from SymbolVariant.ConnectionPoints (fallback: embedded symbol_pins.json). The result carries wireCheck: every line's stored Location must equal the pin coordinate it was asked to start at (locationOk), so \"lines are connected\" is verified, not asserted. A bare template name resolves in the installed EPLAN template folder: GOST_bas001 registers GOST_symbol, IEC_bas001 ships IEC_symbol. export.path is a DIRECTORY (Export.GraphicPage names the file after the page).",
            },
        };

        internal static int Run(string[] args)
        {
            string skillDir = args.Length > 1 && !string.IsNullOrEmpty(args[1])
                ? args[1]
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                               ".agents", "skills", "eplan-api");
            string refs = Path.Combine(skillDir, "references");
            string regions = Path.Combine(refs, "regions");
            Directory.CreateDirectory(regions);

            object raw = Cmd.ListCommands();
            var commands = raw as List<object> ?? new List<object>();

            // commands.json (authoritative dump)
            Directory.CreateDirectory(S.OutDir);
            string dumpPath = Path.Combine(S.OutDir, "commands.json");
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var doc = new Dictionary<string, object>
            {
                { "commands", commands },
                { "regions", Cmd.ListRegions() },
            };
            File.WriteAllText(dumpPath, js.Serialize(doc));

            var byRegion = new SortedDictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
            foreach (var c in commands)
            {
                var d = c as Dictionary<string, object>;
                if (d == null) continue;
                string region = Str(d, "region");
                if (!byRegion.ContainsKey(region)) byRegion[region] = new List<Dictionary<string, object>>();
                byRegion[region].Add(d);
            }

            File.WriteAllText(Path.Combine(refs, "commands.md"), CommandIndex(commands));
            foreach (var kv in byRegion)
                File.WriteAllText(Path.Combine(regions, kv.Key + ".md"), RegionFile(kv.Key, kv.Value));
            File.WriteAllText(Path.Combine(refs, "api-map.md"), ApiMap());
            File.WriteAllText(Path.Combine(refs, "recipes.md"), RecipesText());

            Console.WriteLine("wrote commands.json -> " + dumpPath);
            Console.WriteLine("wrote commands.md, api-map.md, recipes.md, regions/*.md under " + refs);
            Console.WriteLine("commands: " + commands.Count + ", regions: " + byRegion.Count);
            return 0;
        }

        private static string CommandIndex(List<object> commands)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# EplanRpc commands (index)");
            sb.AppendLine();
            sb.AppendLine("Generated from the server command table. Grep here; the per-region");
            sb.AppendLine("files under `regions/` carry the detail and are loaded on demand.");
            sb.AppendLine();
            sb.AppendLine("| command | args | returns | example |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var c in commands)
            {
                var d = c as Dictionary<string, object>;
                if (d == null) continue;
                sb.Append("| `").Append(Str(d, "name")).Append("` | `")
                  .Append(Esc(Str(d, "args"))).Append("` | ")
                  .Append(Str(d, "ret")).Append(" | `")
                  .Append(Esc(Str(d, "example"))).AppendLine("` |");
            }
            return sb.ToString();
        }

        private static string RegionFile(string region, List<Dictionary<string, object>> commands)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Region: " + region);
            sb.AppendLine();
            foreach (var d in commands)
            {
                sb.AppendLine("## `" + Str(d, "name") + "`");
                sb.AppendLine();
                sb.AppendLine("- args: `" + Str(d, "args") + "`");
                sb.AppendLine("- returns: " + Str(d, "ret"));
                sb.AppendLine("- example: `" + Str(d, "example") + "`");
                sb.AppendLine("- " + Str(d, "desc"));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string ApiMap()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# API map: region -> namespace -> key types");
            sb.AppendLine();
            foreach (var row in RegionMap)
                sb.AppendLine("- **" + row[0] + "** \u2014 " + row[1] + " \u2014 `" + row[2] + "`");
            sb.AppendLine();
            sb.AppendLine("The full surface (7388 types) is behind `api.types` / `api.members` / `api.desc`.");
            sb.AppendLine("`api.desc <Type.Member>` is the lazy source for a single signature.");
            sb.AppendLine("The surface is embedded in `EplanRpc.exe`; `--surface` is a development override.");
            return sb.ToString();
        }

        private static string RecipesText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Recipes (copy a line, don't re-derive)");
            sb.AppendLine();
            foreach (var r in Recipes)
            {
                sb.AppendLine("## " + r.Name + "  (mode: " + r.Mode + ")");
                sb.AppendLine();
                foreach (var line in r.Lines)
                    sb.AppendLine("    " + line);
                sb.AppendLine();
                sb.AppendLine(r.Note);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            return s == null ? "" : s.Replace("|", "\\|");
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }
    }
}
