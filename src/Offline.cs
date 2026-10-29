using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;

namespace EplanRpc
{
    /// <summary>
    /// Offline backend: EplApplication.Init + direct typed calls. Typed regions
    /// are the fast, validated path; api.invoke is the generic long tail over
    /// reflection.
    /// </summary>
    internal static class Offline
    {
        private static Eplan.EplApi.System.EplApplication _app;

        internal static void Init()
        {
            if (_app != null) return;
            _app = new Eplan.EplApi.System.EplApplication { EplanBinFolder = S.VariantBin };
            _app.Init("", false, false);
            S.Pm = new ProjectManager { LockProjectByDefault = true };
        }

        internal static object WithLock(Func<object> f)
        {
            using (new LockingStep()) return f();
        }

        // ============================================================ project
        internal static object ProjectCreate(Dictionary<string, object> a)
        {
            string link = S.GetStr(a, "link", Path.Combine(S.OutDir, "PSU_230V_24V.elk"));
            string template = ResolveTemplate(S.GetStr(a, "template", null));
            CloseProject();
            S.Project = S.Pm.CreateProject(link, template);
            S.Page = null;
            return Dict("project", link, "template", template);
        }

        /// <summary>
        /// A bare template name resolves inside the installed EPLAN template
        /// folder, so `template=GOST_bas001` selects a project that registers
        /// GOST_symbol without hard-coding an absolute path at the call site.
        /// </summary>
        internal static string ResolveTemplate(string template)
        {
            if (string.IsNullOrEmpty(template)) return S.TemplatePath;
            if (template.IndexOf('\\') >= 0 || template.IndexOf('/') >= 0) return template;
            string file = template.ToLowerInvariant().EndsWith(".zw9") ? template : template + ".zw9";
            return Path.Combine(S.TemplatesDir, file);
        }

        internal static object ProjectOpen(Dictionary<string, object> a)
        {
            string link = S.GetStr(a, "link", null);
            if (string.IsNullOrEmpty(link)) throw new ArgumentException("link required");
            CloseProject();
            S.Project = S.Pm.OpenProject(link, ProjectManager.OpenMode.Exclusive);
            S.Page = null;
            return Dict("project", link);
        }

        internal static object ProjectClose()
        {
            CloseProject();
            return "closed";
        }

        private static void CloseProject()
        {
            try { if (S.Project != null) S.Project.Close(); } catch { }
            S.Project = null;
            S.Page = null;
        }

        internal static object ProjectPages()
        {
            RequireProject();
            var list = new List<object>();
            foreach (Page p in S.Project.Pages)
                list.Add(new Dictionary<string, object> { { "name", p.Name }, { "type", p.PageType.ToString() } });
            return list;
        }

        // ================================================================ page
        internal static object PageCreate(Dictionary<string, object> a)
        {
            RequireProject();
            string name = S.GetStr(a, "name", null);
            string typeName = S.GetStr(a, "type", "Circuit");
            var type = (DocumentTypeManager.DocumentType)Enum.Parse(typeof(DocumentTypeManager.DocumentType), typeName);

            if (string.IsNullOrEmpty(name))
            {
                var parts = new PagePropertyList();
                var page = new Page();
                page.Create(S.Project, type, parts);
                S.Page = page;
                return Dict("page", page.Name);
            }
            foreach (Page p in S.Project.Pages)
                if (p.Name == name) { S.Page = p; return Dict("page", name, "existing", true); }

            var parts2 = new PagePropertyList { PAGE_NAME = name };
            var page2 = new Page();
            page2.Create(S.Project, type, parts2);
            S.Page = page2;
            return Dict("page", name, "existing", false);
        }

        internal static object PageUse(Dictionary<string, object> a)
        {
            RequireProject();
            string name = S.GetStr(a, "name", null);
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name required");
            foreach (Page p in S.Project.Pages)
                if (p.Name == name) { S.Page = p; return Dict("page", name); }
            throw new ArgumentException("page not found: " + name);
        }

        internal static object PageClear()
        {
            RequirePage();
            int n = 0;
            var funcs = new List<Function>();
            foreach (Function f in S.Page.Functions) funcs.Add(f);
            foreach (Function f in funcs) { try { f.Remove(); n++; } catch { } }
            return Dict("removed", n);
        }

        // =============================================================== symbol
        internal static object SymbolList(Dictionary<string, object> a)
        {
            string libName = S.GetStr(a, "library", S.DefaultLib);
            var lib = new SymbolLibrary();
            bool opened = lib.Initialize(libName);
            int count;
            try { count = Convert.ToInt32(lib.Properties.SYMBLIB_SYMBOLCOUNT.ToString()); }
            catch { count = 4000; }
            string contains = S.GetStr(a, "contains", null);
            int limit = S.GetInt(a, "limit", 200);

            var sb = new StringBuilder();
            int matched = 0, returned = 0;
            for (int i = 1; i <= count; i++)
            {
                Symbol sym;
                try { sym = new Symbol(lib, i); } catch { continue; }
                for (int vn = 0; vn <= 7; vn++)
                {
                    try
                    {
                        var v = new SymbolVariant();
                        v.Initialize(sym, vn);
                        int cp;
                        try { cp = Convert.ToInt32(v.Properties.SYMBVAR_CONPOINTSCOUNT.ToString()); } catch { cp = -1; }
                        string nm = v.SymbolName;
                        if (contains != null && nm.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) break;
                        matched++;
                        if (returned < limit) { sb.Append(nm).Append('\t').Append(cp).Append('\t').Append(vn).AppendLine(); returned++; }
                        break;
                    }
                    catch { }
                }
            }
            return Dict("opened", opened, "matched", matched, "returned", returned, "text", sb.ToString());
        }

        internal static object SymbolCheck(Dictionary<string, object> a)
        {
            RequireProject();
            string libName = S.GetStr(a, "library", S.DefaultLib);
            string name = S.GetStr(a, "name", null);
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name required");
            int vn = S.GetInt(a, "variant", 0);
            var lib = new SymbolLibrary(S.Project, libName);
            var symbol = new Symbol(lib, name);
            var v = new SymbolVariant();
            v.Initialize(symbol, vn);
            int cp;
            try { cp = Convert.ToInt32(v.Properties.SYMBVAR_CONPOINTSCOUNT.ToString()); } catch { cp = -1; }
            return Dict("name", name, "variant", vn, "conpoints", cp, "symbolName", v.SymbolName);
        }

        // ============================================================== function
        internal static object FunctionPlace(Dictionary<string, object> a)
        {
            RequirePage();
            string libName = S.GetStr(a, "library", S.DefaultLib);
            string name = S.GetStr(a, "symbol", null);
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("symbol required");
            int vn = S.GetInt(a, "variant", S.GetInt(a, "v", 0));
            string tag = S.GetStr(a, "name", S.GetStr(a, "tag", name));
            string vis = S.GetStr(a, "visible", S.GetStr(a, "vis", tag));
            Function fn = PlaceFunction(libName, name, vn, tag, vis,
                S.GetDouble(a, "x", 0), S.GetDouble(a, "y", 0),
                S.GetArray(a, "designations"), false);
            return Dict("placed", fn.Name, "symbol", name, "handle", fn.ToStringIdentifier());
        }

        /// <summary>
        /// Single placement mechanism shared by function.place and
        /// schematic.draw: symbol lookup, Function.Create, tag/location,
        /// optional contact-image hiding and connection designations.
        /// </summary>
        internal static Function PlaceFunction(string libName, string symbolName, int variantNr,
            string tag, string visibleName, double x, double y,
            List<object> designations, bool hideContactImage)
        {
            PinBase[] ignored;
            return PlaceFunction(libName, symbolName, variantNr, tag, visibleName, x, y,
                designations, hideContactImage, out ignored);
        }

        /// <summary>
        /// Place a symbol and expose its variant connection points. The points
        /// carry Index + local Location (the authoritative pin geometry), so the
        /// schematic renderer resolves wire endpoints without a precomputed map.
        /// </summary>
        internal static Function PlaceFunction(string libName, string symbolName, int variantNr,
            string tag, string visibleName, double x, double y,
            List<object> designations, bool hideContactImage, out PinBase[] connectionPoints)
        {
            // Project-scoped open: Function.Create needs a variant bound to the
            // project's symbol library list. Offline that list comes from the
            // project template (IEC_bas001 ships IEC_symbol), so a library only
            // present in the GUI project (e.g. GOST_symbol) is not placeable here.
            var lib = new SymbolLibrary(S.Project, libName);
            var symbol = new Symbol(lib, symbolName);
            var variant = new SymbolVariant();
            variant.Initialize(symbol, variantNr);
            connectionPoints = variant.ConnectionPoints;

            var fn = new Function();
            fn.Create(S.Page, variant);
            fn.Name = tag;
            fn.VisibleName = visibleName ?? tag;
            fn.Location = new PointD(x, y);

            if (hideContactImage)
                fn.ContactImageType =
                    Eplan.EplApi.DataModel.Graphics.ContactImage.Enums.ContactImageType.None;

            for (int i = 0; i < designations.Count; i++)
                try { fn.Properties.FUNC_CONNECTIONDESIGNATION[i + 1] = Convert.ToString(designations[i]); } catch { }

            return fn;
        }

        internal static object FunctionRemove(Dictionary<string, object> a)
        {
            RequirePage();
            string name = S.GetStr(a, "name", null);
            string handle = S.GetStr(a, "handle", null);
            int n = 0;
            foreach (Function f in S.Page.Functions)
            {
                bool hit = (!string.IsNullOrEmpty(name) && f.Name == name) ||
                           (!string.IsNullOrEmpty(handle) && f.ToStringIdentifier() == handle);
                if (!hit) continue;
                try { f.Remove(); n++; } catch { }
            }
            return Dict("removed", n);
        }

        internal static object FunctionTag(Dictionary<string, object> a)
        {
            RequirePage();
            string name = S.GetStr(a, "name", null);
            string tag = S.GetStr(a, "tag", null);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(tag)) throw new ArgumentException("name and tag required");
            int n = 0;
            foreach (Function f in S.Page.Functions)
                if (f.Name == name) { f.Name = tag; n++; }
            return Dict("tagged", n, "tag", tag);
        }

        internal static object FunctionDesignation(Dictionary<string, object> a)
        {
            RequirePage();
            string name = S.GetStr(a, "name", null);
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name required");
            var desig = S.GetArray(a, "designations");
            int n = 0;
            foreach (Function f in S.Page.Functions)
                if (f.Name == name)
                {
                    for (int i = 0; i < desig.Count; i++)
                        try { f.Properties.FUNC_CONNECTIONDESIGNATION[i + 1] = Convert.ToString(desig[i]); } catch { }
                    n++;
                }
            return Dict("set", n);
        }

        internal static object FunctionPins()
        {
            RequirePage();
            var list = new List<object>();
            foreach (Function f in S.Page.Functions)
            {
                var pts = new List<object>();
                try
                {
                    var cpps = f.ConnectionPointPositions;
                    if (cpps != null)
                        foreach (ConnectionPointPosition p in cpps)
                            pts.Add(new Dictionary<string, object> { { "i", p.Index }, { "n", p.Name }, { "x", p.Position.X }, { "y", p.Position.Y } });
                }
                catch { }
                list.Add(new Dictionary<string, object> { { "name", f.Name }, { "handle", f.ToStringIdentifier() }, { "points", pts } });
            }
            return list;
        }

        // ============================================================ connection
        internal static object GenerateConnections()
        {
            RequireProject();
            new Generate().Connections(S.Project);
            return "generated";
        }

        internal static object ConnectionRead()
        {
            RequirePage();
            var list = new List<object>();
            foreach (Function f in S.Page.Functions)
            {
                var conns = new List<object>();
                try
                {
                    foreach (Connection c in f.Connections)
                        conns.Add(c.ToStringIdentifier());
                }
                catch { }
                list.Add(new Dictionary<string, object> { { "name", f.Name }, { "connections", conns } });
            }
            return list;
        }

        // ============================================================== graphics
        internal static object LineDraw(Dictionary<string, object> a)
        {
            RequirePage();
            var line = new Eplan.EplApi.DataModel.Graphics.Line();
            line.Create(S.Page,
                new PointD(S.GetDouble(a, "x1", 0), S.GetDouble(a, "y1", 0)),
                new PointD(S.GetDouble(a, "x2", 0), S.GetDouble(a, "y2", 0)));
            return "ok";
        }

        internal static object RectDraw(Dictionary<string, object> a)
        {
            RequirePage();
            var r = new Eplan.EplApi.DataModel.Graphics.Rectangle();
            r.Create(S.Page);
            r.SetArea(new PointD(S.GetDouble(a, "x1", 0), S.GetDouble(a, "y1", 0)),
                      new PointD(S.GetDouble(a, "x2", 0), S.GetDouble(a, "y2", 0)));
            return "ok";
        }

        internal static object TextDraw(Dictionary<string, object> a)
        {
            RequirePage();
            var t = new Eplan.EplApi.DataModel.Graphics.Text();
            t.Create(S.Page, S.GetStr(a, "text", ""), S.GetDouble(a, "height", 3.5));
            t.Location = new PointD(S.GetDouble(a, "x", 0), S.GetDouble(a, "y", 0));
            return "ok";
        }

        // ================================================================ export
        internal static object ExportPdf(Dictionary<string, object> a)
        {
            RequirePage();
            string pdf = S.GetStr(a, "path", Path.Combine(S.OutDir, "page.pdf"));
            var pages = new ArrayList { S.Page };
            new Export().PdfPages(pages, "", pdf, Export.DegreeOfColor.Color, false, "ru_RU", false);
            return Dict("pdf", pdf);
        }

        internal static object ExportImage(Dictionary<string, object> a)
        {
            RequirePage();
            string img = S.GetStr(a, "path", Path.Combine(S.OutDir, "page.png"));
            int width = S.GetInt(a, "width", 1600);
            string dir = ResolvePngDir(img);
            new Export().GraphicPage(S.Page, dir, Export.Format.PNG, 24, width, Export.Compression.None, false);
            return Dict("image", dir);
        }

        // ============================================================== settings
        internal static object SettingsGet(Dictionary<string, object> a)
        {
            string path = S.GetStr(a, "path", null);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path required");
            var settings = new Settings();
            string v = settings.GetStringSetting(path, S.GetInt(a, "index", 0));
            return Dict("value", v);
        }

        internal static object SettingsSet(Dictionary<string, object> a)
        {
            string path = S.GetStr(a, "path", null);
            string value = S.GetStr(a, "value", null);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path required");
            var settings = new Settings();
            settings.SetStringSetting(path, value, S.GetInt(a, "index", 0));
            return "ok";
        }

        // ================================================================= stats
        internal static object Stats()
        {
            RequirePage();
            int fn = 0, conn = 0;
            foreach (Function f in S.Page.Functions)
            {
                fn++;
                try { conn += f.Connections.Length; } catch { }
            }
            return Dict("functions", fn, "connections", conn, "page", S.Page.Name);
        }

        // ============================================================== schematic
        /// <summary>
        /// schematic.draw &lt;spec&gt; — self-contained JSON drawing. Mirrors the
        /// former client generator (draw_template.cs + eplan_schematic.py) but
        /// runs natively on the typed object model: no template, no
        /// remoting.script, no reflection. Wire endpoints are placement location
        /// plus embedded symbol_pins.json offsets (ConnectionPointPositions on a
        /// placed function return zeros, so the symbol geometry is authoritative).
        /// </summary>
        internal static object SchematicDraw(Dictionary<string, object> a)
        {
            string specPath = S.GetStr(a, "spec", null);
            string inline = S.GetStr(a, "json", null);
            if (string.IsNullOrEmpty(specPath) && string.IsNullOrEmpty(inline))
                throw new ArgumentException("schematic.draw needs spec=<path> or json=<inline json>");

            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string text;
            if (!string.IsNullOrEmpty(specPath))
            {
                if (!File.Exists(specPath)) throw new FileNotFoundException("spec not found: " + specPath);
                text = File.ReadAllText(specPath);
            }
            else text = inline;
            var spec = js.Deserialize<Dictionary<string, object>>(text);

            RequireProject();

            // ---- page
            var pageSpec = AsDict(Obj(spec, "page"));
            string library = Str(Obj(spec, "library")) ?? Str(Obj(pageSpec, "library")) ?? "GOST_symbol";
            string plant = Str(Obj(pageSpec, "plant")) ?? "";
            string location = Str(Obj(pageSpec, "location")) ?? "";
            int counter = pageSpec == null ? 1 : ToInt(Obj(pageSpec, "counter"), 1);
            bool replace = pageSpec != null && ToBool(Obj(pageSpec, "replace"), false);
            SchematicPage(plant, location, counter, replace);

            // ---- placements
            var placed = new Dictionary<string, PlacedInfo>();
            int placementCount = 0;
            foreach (var o in AsList(Obj(spec, "placements")))
            {
                var p = AsDict(o);
                if (p == null) continue;
                string symbol = Str(Obj(p, "symbol"));
                if (string.IsNullOrEmpty(symbol)) throw new ArgumentException("placement has no 'symbol'");
                int variant = ToInt(Obj(p, "variant"), 0);
                string tag = Str(Obj(p, "tag")) ?? ("-" + symbol);
                double x = ToDouble(Obj(p, "x"), 0);
                double y = ToDouble(Obj(p, "y"), 0);
                var designations = AsList(Obj(p, "designations"));
                bool hide = ToBool(Obj(p, "hideContactImage"), false);
                var pins = new List<string>();
                foreach (var pn in AsList(Obj(p, "pins"))) pins.Add(Convert.ToString(pn));

                PinBase[] offsets;
                PlaceFunction(library, symbol, variant, tag, tag, x, y, designations, hide, out offsets);
                string id = Str(Obj(p, "id"));
                if (id != null) placed[id] = new PlacedInfo { Symbol = symbol, X = x, Y = y, Pins = pins, Offsets = offsets };
                placementCount++;
            }

            // ---- wires (real DynamicConnectionLine on actual pin coordinates)
            int wireCount = 0;
            var lineCheck = new List<object>();
            foreach (var o in AsList(Obj(spec, "wires")))
            {
                var w = AsDict(o);
                if (w == null) continue;
                double x1, y1, x2, y2;
                ResolveEndpoint(Obj(w, "from"), placed, "wire.from", out x1, out y1);
                ResolveEndpoint(Obj(w, "to"), placed, "wire.to", out x2, out y2);
                var dcl = DrawConnectionLine(x1, y1, x2, y2);
                lineCheck.Add(CheckLine(dcl, x1, y1, x2, y2));
                wireCount++;
            }

            // ---- texts
            int textCount = 0;
            foreach (var o in AsList(Obj(spec, "texts")))
            {
                var t = AsDict(o);
                if (t == null) continue;
                TextDraw(t);
                textCount++;
            }

            // ---- connections + export
            var result = Dict("page", S.Page.Name, "placements", placementCount,
                              "wires", wireCount, "texts", textCount);
            int lineOk = 0;
            foreach (Dictionary<string, object> c in lineCheck)
                if (c.ContainsKey("locationOk") && (bool)c["locationOk"]) lineOk++;
            result["wireCheck"] = Dict("count", lineCheck.Count, "locationOk", lineOk, "items", lineCheck);
            if (!spec.ContainsKey("generateConnections") || ToBool(Obj(spec, "generateConnections"), true))
            {
                try { GenerateConnections(); result["connectionsGenerated"] = true; }
                catch (Exception ex) { result["connectionsGenerated"] = false; result["connectionError"] = ex.Message; }
            }
            else result["connectionsGenerated"] = false;

            object export = Obj(spec, "export");
            if (export != null)
            {
                string path = null; int width = 1800;
                var ed = AsDict(export);
                if (ed != null) { path = Str(Obj(ed, "path")); width = ToInt(Obj(ed, "width"), 1800); }
                else path = Convert.ToString(export);
                string dir = ResolvePngDir(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    new Export().GraphicPage(S.Page, dir, Export.Format.PNG, 24, width, Export.Compression.None, false);
                    result["exportPath"] = dir;
                }
            }

            int fn = 0, conn = 0;
            foreach (Function f in S.Page.Functions) { fn++; try { conn += f.Connections.Length; } catch { } }
            result["functions"] = fn;
            result["connections"] = conn;
            return result;
        }

        private sealed class PlacedInfo
        {
            internal string Symbol;
            internal double X, Y;
            internal List<string> Pins = new List<string>();
            internal PinBase[] Offsets;
        }

        private static void SchematicPage(string plant, string location, int counter, bool replace)
        {
            RequireProject();
            string wanted = "=" + plant + "+" + location + "/" + counter;
            Page existing = null;
            foreach (Page p in S.Project.Pages)
                if (p.Name == wanted) { existing = p; break; }
            if (existing != null)
            {
                if (replace) existing.Remove();
                else { S.Page = existing; return; }
            }
            var parts = new PagePropertyList
            {
                DESIGNATION_PLANT = plant,
                DESIGNATION_LOCATION = location,
                PAGE_COUNTER = counter,
            };
            var page = new Page();
            page.Create(S.Project, DocumentTypeManager.DocumentType.Circuit, parts);
            S.Page = page;
        }

        private static void ResolveEndpoint(object ep, Dictionary<string, PlacedInfo> placed,
            string where, out double x, out double y)
        {
            x = y = 0;
            var d = AsDict(ep);
            if (d != null && d.ContainsKey("placement"))
            {
                string id = Str(Obj(d, "placement"));
                PlacedInfo p;
                if (id == null || !placed.TryGetValue(id, out p))
                    throw new ArgumentException(where + " references unknown placement '" + id + "'");
                object pinRaw = Obj(d, "pin");
                int idx;
                if (pinRaw == null || pinRaw is string)
                {
                    string pinName = pinRaw == null ? null : (string)pinRaw;
                    idx = pinName == null ? 0 : p.Pins.IndexOf(pinName);
                }
                else idx = ToInt(pinRaw, 0);
                if (idx < 0)
                    throw new ArgumentException(where + ": placement '" + id + "' has no pin '" + pinRaw + "'");
                if (p.Offsets != null && idx < p.Offsets.Length && p.Offsets[idx] != null)
                {
                    x = p.X + p.Offsets[idx].Location.X;
                    y = p.Y + p.Offsets[idx].Location.Y;
                    return;
                }
                double[] off = PinMap.Get(p.Symbol, idx);
                if (off == null)
                    throw new ArgumentException(where + ": no pin offset for symbol '" + p.Symbol + "' index " + idx);
                x = p.X + off[0];
                y = p.Y + off[1];
                return;
            }
            var arr = AsList(ep);
            if (arr.Count >= 2) { x = ToDouble(arr[0], 0); y = ToDouble(arr[1], 0); return; }
            throw new ArgumentException(where + " must be {placement,pin} or [x,y]");
        }

        private static DynamicConnectionLine DrawConnectionLine(double x1, double y1, double x2, double y2)
        {
            RequirePage();
            var dcl = new DynamicConnectionLine();
            dcl.Create(S.Page);
            dcl.Location = new PointD(x1, y1);
            dcl.SetGraphics(new PointD(0, 0), new PointD(x2 - x1, y2 - y1));
            return dcl;
        }

        /// <summary>
        /// Export.GraphicPage treats its target as a directory and names the file
        /// after the page. Accepting a "*.png" argument as a file silently created
        /// a directory with that name, so resolve every caller to a directory once.
        /// </summary>
        private static string ResolvePngDir(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string dir = path.ToLowerInvariant().EndsWith(".png") ? Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Read back the geometry a DynamicConnectionLine actually stored and
        /// compare it with the endpoints we asked for. This is the invariant the
        /// "lines are connected" claim rests on: the stored line must start/end on
        /// the pin coordinates, not merely exist.
        /// </summary>
        private static Dictionary<string, object> CheckLine(
            DynamicConnectionLine dcl, double ex1, double ey1, double ex2, double ey2)
        {
            var rec = new Dictionary<string, object>
            {
                { "expect", new double[] { ex1, ey1, ex2, ey2 } },
            };
            try
            {
                PointD loc = dcl.Location;
                rec["location"] = new double[] { loc.X, loc.Y };
                Placement g = dcl.GetGraphics();
                if (g != null)
                {
                    PointD[] bb = g.GetBoundingBox();
                    if (bb != null && bb.Length > 0)
                        rec["bbox"] = new double[] { bb[0].X, bb[0].Y, bb[bb.Length - 1].X, bb[bb.Length - 1].Y };
                }
                rec["locationOk"] = Math.Abs(loc.X - ex1) < 1e-6 && Math.Abs(loc.Y - ey1) < 1e-6;
            }
            catch (Exception ex) { rec["error"] = ex.Message; }
            return rec;
        }

        private static object Obj(Dictionary<string, object> d, string k)
        {
            if (d == null) return null;
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }

        private static string Str(object o)
        {
            return o == null ? null : Convert.ToString(o);
        }

        private static Dictionary<string, object> AsDict(object o)
        {
            return o as Dictionary<string, object>;
        }

        private static List<object> AsList(object o)
        {
            var list = new List<object>();
            if (o is System.Collections.IEnumerable)
                foreach (var x in (System.Collections.IEnumerable)o)
                    list.Add(x);
            return list;
        }

        private static double ToDouble(object o, double def)
        {
            if (o == null) return def;
            try { return Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return def; }
        }

        private static int ToInt(object o, int def)
        {
            if (o == null) return def;
            try { return Convert.ToInt32(o); } catch { return def; }
        }

        private static bool ToBool(object o, bool def)
        {
            if (o == null) return def;
            if (o is bool) return (bool)o;
            string s = Convert.ToString(o).Trim();
            if (s.Length == 0) return def;
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }

        // ===================================================== generic api.invoke
        /// <summary>
        /// api.invoke &lt;Type.Member&gt; [k=v ...] — the generic long tail.
        /// Supports: static method, static property get/set, constructor
        /// (Type.new), and instance method/property on @page / @project.
        /// </summary>
        internal static object ApiInvoke(string target, Dictionary<string, object> a)
        {
            if (string.IsNullOrEmpty(target)) throw new ArgumentException("api.invoke needs a Type.Member target");

            // @page / @project instance call.
            if (target.StartsWith("@page.") || target.StartsWith("@project."))
            {
                object inst = target.StartsWith("@page.") ? RequirePageObj() : RequireProjectObj();
                string member = target.Substring(target.IndexOf('.') + 1);
                return InvokeMember(inst, member, a, null);
            }

            int dot = target.LastIndexOf('.');
            if (dot <= 0) throw new ArgumentException("target must be Namespace.Type.Member");
            string typeName = target.Substring(0, dot);
            string mem = target.Substring(dot + 1);
            Type t = ResolveType(typeName);

            if (mem == "new" || mem == "New")
                return InvokeCtor(t, a);

            // static property get/set?
            var sp = PropStatic(t, mem);
            if (sp != null && sp.GetGetMethod() != null && sp.GetIndexParameters().Length == 0)
            {
                if (a.ContainsKey("value"))
                {
                    sp.SetValue(null, Conv(Convert.ToString(a["value"]), sp.PropertyType), null);
                    return "set";
                }
                object v = sp.GetValue(null, null);
                return v == null ? null : SafeText(v);
            }

            // static method
            return InvokeStatic(t, mem, a);
        }

        private static object InvokeMember(object inst, string member, Dictionary<string, object> a, Type hint)
        {
            Type t = inst.GetType();
            var pi = Prop(t, member);
            if (pi != null && pi.GetIndexParameters().Length == 0)
            {
                if (a.ContainsKey("value"))
                {
                    pi.SetValue(inst, Conv(Convert.ToString(a["value"]), pi.PropertyType), null);
                    return "set";
                }
                object v = pi.GetValue(inst, null);
                return v == null ? null : SafeText(v);
            }
            return CallMethod(inst, member, a, false);
        }

        private static object InvokeCtor(Type t, Dictionary<string, object> a)
        {
            object[] args;
            ConstructorInfo ci = PickCtor(t, a, out args);
            if (ci == null) throw new Exception("No matching ctor for " + t.FullName + " with args " + Keys(a));
            try { return ci.Invoke(args); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        private static object InvokeStatic(Type t, string mem, Dictionary<string, object> a)
        {
            return CallMethod(null, mem, a, true, t);
        }

        private static object CallMethod(object inst, string name, Dictionary<string, object> a, bool wantStatic, Type decl = null)
        {
            Type t = decl ?? inst.GetType();
            var args = new object[0];
            MethodInfo mi = PickMethod(t, name, a, wantStatic, out args);
            if (mi == null)
                throw new Exception("No matching " + (wantStatic ? "static " : "") + "method " + t.FullName + "." + name +
                    "(" + string.Join(",", a.Keys.ToArray()) + "). Use api.members " + t.FullName + " to see signatures.");
            try { return mi.Invoke(inst, args); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        private static MethodInfo PickMethod(Type t, string name, Dictionary<string, object> a, bool wantStatic, out object[] args)
        {
            args = new object[0];
            MethodInfo fallback = null;
            foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | (wantStatic ? BindingFlags.Static : BindingFlags.Instance)))
            {
                if (mi.Name != name) continue;
                var ps = mi.GetParameters();
                if (ps.Length != a.Count) continue;
                var cargs = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    string raw = GetArgFor(a, ps[i].Name, i);
                    if (raw == null) { ok = false; break; }
                    try { cargs[i] = Conv(raw, ps[i].ParameterType); }
                    catch { ok = false; break; }
                }
                if (!ok) continue;
                if (IsByName(a, ps)) { args = cargs; return mi; }
                fallback = mi;
            }
            // Fallback: positional if a name-based match failed.
            if (fallback != null)
            {
                var ps = fallback.GetParameters();
                var cargs = new object[ps.Length];
                var keys = new List<string>(a.Keys);
                for (int i = 0; i < ps.Length; i++)
                    cargs[i] = Conv(Convert.ToString(a[keys[i]]), ps[i].ParameterType);
                args = cargs;
            }
            return fallback;
        }

        private static ConstructorInfo PickCtor(Type t, Dictionary<string, object> a, out object[] args)
        {
            args = new object[0];
            foreach (ConstructorInfo ci in t.GetConstructors())
            {
                var ps = ci.GetParameters();
                if (ps.Length != a.Count) continue;
                var cargs = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    string raw = GetArgFor(a, ps[i].Name, i);
                    if (raw == null) { ok = false; break; }
                    try { cargs[i] = Conv(raw, ps[i].ParameterType); }
                    catch { ok = false; break; }
                }
                if (ok) { args = cargs; return ci; }
            }
            return null;
        }

        private static bool IsByName(Dictionary<string, object> a, ParameterInfo[] ps)
        {
            foreach (var p in ps)
                if (!a.ContainsKey(p.Name)) return false;
            return true;
        }

        private static string GetArgFor(Dictionary<string, object> a, string paramName, int i)
        {
            if (a.ContainsKey(paramName)) return Convert.ToString(a[paramName]);
            if (a.ContainsKey("arg" + i)) return Convert.ToString(a["arg" + i]);
            // case-insensitive name
            foreach (var k in a.Keys)
                if (string.Equals(k, paramName, StringComparison.OrdinalIgnoreCase))
                    return Convert.ToString(a[k]);
            return null;
        }

        internal static object Conv(string raw, Type target)
        {
            if (target == typeof(string)) return raw;
            if (target == typeof(int)) return int.Parse(raw);
            if (target == typeof(long)) return long.Parse(raw);
            if (target == typeof(double)) return double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(float)) return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(bool)) return bool.Parse(raw);
            if (target.IsEnum) return Enum.Parse(target, raw, true);
            if (target.Name == "PointD" || target.Name == "PointF")
            {
                var c = target.GetConstructor(new[] { typeof(double), typeof(double) });
                string[] xy = raw.Split(',');
                double x = double.Parse(xy[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                double y = double.Parse(xy[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                if (c != null) return c.Invoke(new object[] { x, y });
            }
            if (target == typeof(object)) return raw;
            throw new Exception("no conversion for string -> " + target.FullName);
        }

        private static Type ResolveType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName); } catch { }
                if (t != null) return t;
            }
            // Try the 14 known managed assemblies.
            foreach (string a in new[] { "Eplan.EplApi.DataModelu", "Eplan.EplApi.HEServicesu", "Eplan.EplApi.Baseu", "Eplan.EplApi.Systemu", "Eplan.EplApi.MasterDatau", "Eplan.EplApi.ApplicationFrameworku", "Eplan.EplApi.EServicesu" })
            {
                try
                {
                    Assembly asm = Assembly.LoadFrom(Path.Combine(Program.PlatformBin, a + ".dll"));
                    Type t = asm.GetType(fullName);
                    if (t != null) return t;
                }
                catch { }
            }
            throw new Exception("type not found: " + fullName);
        }

        private static PropertyInfo Prop(Type t, string name)
        {
            Type cur = t;
            while (cur != null)
            {
                PropertyInfo pi = null;
                try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); }
                catch { }
                if (pi == null)
                    try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance); } catch { }
                if (pi != null) return pi;
                cur = cur.BaseType;
            }
            return null;
        }

        private static PropertyInfo PropStatic(Type t, string name)
        {
            Type cur = t;
            while (cur != null)
            {
                PropertyInfo pi = null;
                try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); }
                catch { }
                if (pi == null)
                    try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Static); } catch { }
                if (pi != null) return pi;
                cur = cur.BaseType;
            }
            return null;
        }

        private static object SafeText(object v)
        {
            try { return v == null ? null : v.ToString(); }
            catch { return null; }
        }

        private static string Keys(Dictionary<string, object> a)
        {
            return string.Join(",", a.Keys.ToArray());
        }

        private static string Flatten(Exception e)
        {
            var parts = new List<string>();
            Exception x = e;
            int d = 0;
            while (x != null && d < 6) { parts.Add(x.GetType().Name + ": " + x.Message); x = x.InnerException; d++; }
            return string.Join(" <- ", parts.ToArray());
        }

        private static void RequireProject()
        {
            if (S.Project == null) throw new InvalidOperationException("no project open (project.open/create first)");
        }

        private static void RequirePage()
        {
            RequireProject();
            if (S.Page == null) throw new InvalidOperationException("no page selected (page.create/use first)");
        }

        private static object RequirePageObj()
        {
            RequirePage();
            return S.Page;
        }

        private static object RequireProjectObj()
        {
            RequireProject();
            return S.Project;
        }

        private static Dictionary<string, object> Dict(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2)
                d[Convert.ToString(kv[i])] = kv[i + 1];
            return d;
        }
    }
}
