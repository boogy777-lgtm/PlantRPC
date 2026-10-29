using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// Online backend: gRPC remoting into a running EPLAN GUI. Commands are
    /// executed by generating a reflection C# script (the same technique the
    /// pythonnet MCP bridge uses), running it via ExecuteScript, and reading the
    /// JSON result file the script writes. remoting.script is the full escape
    /// hatch: any reflection script can be run verbatim.
    /// </summary>
    internal static class Online
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static string _scriptDir = Path.Combine(S.OutDir, "scripts");
        private static string _resultDir = Path.Combine(S.OutDir, "results");

        internal static object Connect(Dictionary<string, object> a)
        {
            Remoting.Boot();
            string host = S.GetStr(a, "host", "localhost");
            string port = S.GetStr(a, "port", null);
            int timeout = S.GetInt(a, "timeout", 15);

            // Auto-discover port from the advertised servers when not given.
            if (string.IsNullOrEmpty(port))
            {
                object probe = Refl.New("Eplan.EplApi.RemoteClient.EplanRemoteClient", null);
                try
                {
                    object list = Refl.OutList(probe, "GetActiveEplanServersOnLocalMachine");
                    if (list != null)
                        foreach (object srv in (System.Collections.IEnumerable)list)
                            port = Convert.ToString(Refl.GetProp(srv, "ServerPort"));
                }
                catch { }
                try { Refl.Call(probe, "Dispose"); } catch { }
                if (string.IsNullOrEmpty(port)) port = "49152";
            }

            object client = Refl.New("Eplan.EplApi.RemoteClient.EplanRemoteClient", null);
            Refl.Call(client, "Connect", host, port, TimeSpan.FromSeconds(timeout));
            bool ping = (bool)Refl.Call(client, "Ping");
            if (!ping) { try { Refl.Call(client, "Dispose"); } catch { } throw new Exception("Connect ok but Ping() returned false"); }

            S.RemoteClient = client;
            S.RemoteHost = host;
            S.RemotePort = port;
            S.Mode = "online";
            return S.State();
        }

        internal static object Ping()
        {
            RequireClient();
            return Dict("alive", (bool)Refl.Call(S.RemoteClient, "Ping"));
        }

        internal static object Disconnect()
        {
            if (S.RemoteClient != null)
            {
                try { Refl.Call(S.RemoteClient, "Disconnect"); } catch { }
                try { Refl.Call(S.RemoteClient, "Dispose"); } catch { }
                S.RemoteClient = null;
            }
            return "disconnected";
        }

        internal static object Action(Dictionary<string, object> a)
        {
            RequireClient();
            string action = S.GetStr(a, "action", null);
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("action required");
            bool ok = (bool)Refl.Call(S.RemoteClient, "ExecuteAction", action);
            return Dict("ok", ok, "action", action);
        }

        /// <summary>
        /// Run a C# reflection script in the GUI. Accepts either `file` (path to
        /// a .cs) or `code` (the full class source). The script must write its
        /// JSON result to the {{RESULT_PATH}} placeholder — Script() substitutes
        /// it before execution.
        /// </summary>
        internal static object Script(Dictionary<string, object> a)
        {
            RequireClient();
            Directory.CreateDirectory(_scriptDir);
            Directory.CreateDirectory(_resultDir);
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string scriptPath = Path.Combine(_scriptDir, "script_" + id + ".cs");
            string resultPath = Path.Combine(_resultDir, "result_" + id + ".json");

            string code = S.GetStr(a, "code", null);
            string file = S.GetStr(a, "file", null);
            if (!string.IsNullOrEmpty(file)) code = File.ReadAllText(file);
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("code or file required");
            code = code.Replace("{{RESULT_PATH}}", resultPath.Replace("\\", "\\\\"));

            File.WriteAllText(scriptPath, code, new UTF8Encoding(false));
            var ctx = Refl.New("Eplan.EplApi.Remoting.CallingContext", null);
            Refl.Call(ctx, "Set", "ScriptFile", scriptPath);
            bool ok = (bool)Refl.Call(S.RemoteClient, "ExecuteAction", "ExecuteScript", ctx);

            int waited = 0, timeout = S.GetInt(a, "timeout", 30);
            while (!File.Exists(resultPath) && waited < timeout * 10)
            {
                System.Threading.Thread.Sleep(100);
                waited++;
            }
            if (!File.Exists(resultPath))
                throw new Exception("script did not write a result file (likely failed to compile). ok=" + ok +
                    " ctxMsg=" + Refl.GetProp(ctx, "Message"));

            string json = File.ReadAllText(resultPath);
            try { File.Delete(scriptPath); File.Delete(resultPath); } catch { }
            var result = Json.Deserialize<Dictionary<string, object>>(json);
            return result ?? new Dictionary<string, object>();
        }

        // ===================================================== generic api.invoke
        /// <summary>
        /// Generic api.invoke for the ONLINE backend: generates a reflection
        /// script that resolves the type in the GUI process and invokes a static
        /// method/property/ctor, or an instance member on the current project /
        /// a named page.
        /// </summary>
        internal static object ApiInvoke(string target, Dictionary<string, object> a)
        {
            RequireClient();

            string instance = null;   // null | "project" | "page"
            string typeName = null, member;

            if (target.StartsWith("@project.")) { instance = "project"; member = target.Substring("@project.".Length); }
            else if (target.StartsWith("@page.")) { instance = "page"; member = target.Substring("@page.".Length); }
            else
            {
                int dot = target.LastIndexOf('.');
                if (dot <= 0) throw new ArgumentException("target must be Namespace.Type.Member");
                typeName = target.Substring(0, dot);
                member = target.Substring(dot + 1);
            }
            if (string.IsNullOrEmpty(member)) throw new ArgumentException("empty member in target");

            var body = new StringBuilder();
            if (instance == null)
            {
                body.AppendLine("Type t = FindType(\"" + CS(typeName) + "\");");
                if (member == "new" || member == "New")
                {
                    body.AppendLine("object[] args = BuildArgs(t, new string[] {" + ArgNames(a) + "}, new string[] {" + ArgValues(a) + "}, new string[] {" + ArgTypes(a) + "});");
                    body.AppendLine("object o = Activator.CreateInstance(t, args);");
                    body.AppendLine("results[\"result\"] = SafeText(o);");
                }
                else
                {
                    body.AppendLine("PropertyInfo sp = StaticProp(t, \"" + CS(member) + "\");");
                    body.AppendLine("if (sp != null) { if (HasValue) { sp.SetValue(null, Conv(VAL, sp.PropertyType), null); results[\"result\"]=\"set\"; } else { results[\"result\"] = SafeText(sp.GetValue(null, null)); } }");
                    body.AppendLine("else { object r = CallStatic(t, \"" + CS(member) + "\", new string[] {" + ArgNames(a) + "}, new string[] {" + ArgValues(a) + "}, new string[] {" + ArgTypes(a) + "}); results[\"result\"] = SafeText(r); }");
                }
            }
            else
            {
                string instCode = instance == "project"
                    ? "object inst = SelectionSet_CurrentProject();"
                    : "object inst = FindPage(SelectionSet_CurrentProject(), \"" + CS(S.GetStr(a, "page", "")) + "\");";
                body.AppendLine(instCode);
                body.AppendLine("PropertyInfo ip = Prop(inst.GetType(), \"" + CS(member) + "\");");
                body.AppendLine("if (ip != null) { if (HasValue) { ip.SetValue(inst, Conv(VAL, ip.PropertyType), null); results[\"result\"]=\"set\"; } else { results[\"result\"] = SafeText(ip.GetValue(inst, null)); } }");
                body.AppendLine("else { object r = CallInst(inst, \"" + CS(member) + "\", new string[] {" + ArgNames(a) + "}, new string[] {" + ArgValues(a) + "}, new string[] {" + ArgTypes(a) + "}); results[\"result\"] = SafeText(r); }");
            }

            var script = BuildScript("ApiInvoke", body.ToString(), a);
            return Script(Dict("code", script));
        }

        private static string ArgNames(Dictionary<string, object> a)
        {
            var l = new List<string>();
            foreach (var k in a.Keys)
                if (k != "page" && k != "value") l.Add("\"" + CS(k) + "\"");
            return string.Join(",", l.ToArray());
        }

        private static string ArgValues(Dictionary<string, object> a)
        {
            var l = new List<string>();
            foreach (var kv in a)
                if (kv.Key != "page" && kv.Key != "value") l.Add("\"" + CS(Convert.ToString(kv.Value)) + "\"");
            return string.Join(",", l.ToArray());
        }

        private static string ArgTypes(Dictionary<string, object> a)
        {
            var l = new List<string>();
            foreach (var k in a.Keys)
                if (k != "page" && k != "value") l.Add("\"\"");
            return string.Join(",", l.ToArray());
        }

        private static string BuildScript(string cls, string body, Dictionary<string, object> a)
        {
            string hasValue = a.ContainsKey("value") ? "true" : "false";
            string val = a.ContainsKey("value") ? "\"" + CS(Convert.ToString(a["value"])) + "\"" : "\"\"";

            return
@"using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class " + cls + @"_" + Guid.NewGuid().ToString("N").Substring(0, 6) + @"
{
    static string Flatten(Exception ex)
    {
        var sb = new StringBuilder(); Exception e = ex; int d = 0;
        while (e != null && d < 6) { if (sb.Length > 0) sb.Append("" <- ""); sb.Append(e.GetType().Name).Append("": "").Append(e.Message); e = e.InnerException; d++; }
        return sb.ToString();
    }
    static Assembly[] _asms;
    static Type FindType(string fullName)
    {
        if (_asms == null) _asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly a in _asms) { try { Type t = a.GetType(fullName); if (t != null) return t; } catch { } }
        string[] names = new string[] { ""Eplan.EplApi.DataModelNetu"", ""Eplan.EplApi.HEServicesNetu"", ""Eplan.EplApi.DataModelu"", ""Eplan.EplApi.HEServicesu"", ""Eplan.EplApi.Baseu"", ""Eplan.EplApi.Systemu"", ""Eplan.EplApi.MasterDatau"", ""Eplan.EplApi.EServicesu"" };
        foreach (string n in names) { try { Assembly a = Assembly.Load(n); Type t = a.GetType(fullName); if (t != null) { _asms = AppDomain.CurrentDomain.GetAssemblies(); return t; } } catch { } }
        throw new Exception(""type not found: "" + fullName);
    }
    static PropertyInfo Prop(Type t, string name)
    {
        Type cur = t;
        while (cur != null)
        {
            PropertyInfo pi = null;
            try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); } catch { }
            if (pi == null) try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance); } catch { }
            if (pi != null) return pi;
            cur = cur.BaseType;
        }
        return null;
    }
    static PropertyInfo StaticProp(Type t, string name)
    {
        Type cur = t;
        while (cur != null)
        {
            PropertyInfo pi = null;
            try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); } catch { }
            if (pi == null) try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Static); } catch { }
            if (pi != null) return pi;
            cur = cur.BaseType;
        }
        return null;
    }
    static object Conv(string raw, Type target)
    {
        if (target == typeof(string)) return raw;
        if (target == typeof(int)) return int.Parse(raw);
        if (target == typeof(long)) return long.Parse(raw);
        if (target == typeof(double)) return double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(float)) return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(bool)) return bool.Parse(raw);
        if (target.IsEnum) return Enum.Parse(target, raw, true);
        if (target.Name == ""PointD"" || target.Name == ""PointF"")
        {
            var c = target.GetConstructor(new Type[] { typeof(double), typeof(double) });
            string[] xy = raw.Split(',');
            if (c != null) return c.Invoke(new object[] { double.Parse(xy[0].Trim(), System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) });
        }
        return raw;
    }
    static object[] BuildArgs(Type t, string[] names, string[] values, string[] types)
    {
        var ctors = t.GetConstructors();
        foreach (ConstructorInfo ci in ctors)
        {
            var ps = ci.GetParameters();
            if (ps.Length != values.Length) continue;
            var args = new object[ps.Length]; bool ok = true;
            for (int i = 0; i < ps.Length; i++) { try { args[i] = Conv(values[i], ps[i].ParameterType); } catch { ok = false; break; } }
            if (ok) return args;
        }
        throw new Exception(""no matching ctor"" );
    }
    static object CallStatic(Type t, string name, string[] names, string[] values, string[] types)
    {
        foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (mi.Name != name) continue;
            var ps = mi.GetParameters();
            if (ps.Length != values.Length) continue;
            var args = new object[ps.Length]; bool ok = true;
            for (int i = 0; i < ps.Length; i++) { try { args[i] = Conv(values[i], ps[i].ParameterType); } catch { ok = false; break; } }
            if (ok) { try { return mi.Invoke(null, args); } catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); } }
        }
        throw new Exception(""no matching static method "" + name);
    }
    static object CallInst(object inst, string name, string[] names, string[] values, string[] types)
    {
        Type t = inst.GetType();
        foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (mi.Name != name) continue;
            var ps = mi.GetParameters();
            if (ps.Length != values.Length) continue;
            var args = new object[ps.Length]; bool ok = true;
            for (int i = 0; i < ps.Length; i++) { try { args[i] = Conv(values[i], ps[i].ParameterType); } catch { ok = false; break; } }
            if (ok) { try { return mi.Invoke(inst, args); } catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); } }
        }
        throw new Exception(""no matching instance method "" + name);
    }
    static string SafeText(object v)
    {
        if (v == null) return null;
        try { return v.ToString(); } catch { return null; }
    }
    static string Handle(object o)
    {
        try { var mi = o.GetType().GetMethod(""ToStringIdentifier""); object v = mi.Invoke(o, null); return v == null ? null : v.ToString(); }
        catch { return null; }
    }
    static object SelectionSet_CurrentProject()
    {
        Type ssType = FindType(""Eplan.EplApi.HEServices.SelectionSet"");
        object ss = Activator.CreateInstance(ssType);
        MethodInfo getCur = ssType.GetMethod(""GetCurrentProject"", new Type[] { typeof(bool) });
        object project = getCur.Invoke(ss, new object[] { false });
        if (project == null) throw new Exception(""no project open in EPLAN"");
        return project;
    }
    static object FindPage(object project, string wanted)
    {
        Type finderType = FindType(""Eplan.EplApi.DataModel.DMObjectsFinder"");
        object finder = Activator.CreateInstance(finderType, new object[] { project });
        Type filterType = FindType(""Eplan.EplApi.DataModel.PagesFilter"");
        object filter = Activator.CreateInstance(filterType);
        MethodInfo getPages = finderType.GetMethod(""GetPages"", new Type[] { filterType });
        IEnumerable pages = (IEnumerable)getPages.Invoke(finder, new object[] { filter });
        foreach (object p in pages) { if (p == null) continue; PropertyInfo pi = Prop(p.GetType(), ""Name""); if (pi != null && Convert.ToString(pi.GetValue(p, null)) == wanted) return p; }
        throw new Exception(""page not found: "" + wanted);
    }
    [Start]
    public void Run()
    {
        var results = new Dictionary<string, object>();
        bool HasValue = " + hasValue + @";
        string VAL = " + val + @";
        try
        {
            Type lsType = FindType(""Eplan.EplApi.DataModel.LockingStep"");
            object lockStep = Activator.CreateInstance(lsType);
            try
            {
" + body + @"
                results[""success""] = true;
            }
            finally { try { lsType.GetMethod(""Dispose"").Invoke(lockStep, null); } catch { } }
        }
        catch (Exception ex)
        {
            results[""success""] = false;
            results[""error""] = Flatten(ex);
        }
        File.WriteAllText(@""{{RESULT_PATH}}"", Newtonsoft.Json.JsonConvert.SerializeObject(results));
    }
}";
        }

        private static string CS(string s)
        {
            if (s == null) return "";
            var b = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '\\') b.Append("\\\\");
                else if (c == '"') b.Append("\\\"");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') b.Append("\\r");
                else if (c == '\t') b.Append("\\t");
                else b.Append(c);
            }
            return b.ToString();
        }

        private static void RequireClient()
        {
            if (S.RemoteClient == null)
                throw new InvalidOperationException("not connected (remoting.connect first)");
        }

        private static Dictionary<string, object> Dict(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[Convert.ToString(kv[i])] = kv[i + 1];
            return d;
        }
    }
}
