using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class ExportDoPage_20261028
{
    static string Flatten(Exception ex)
    {
        StringBuilder sb = new StringBuilder();
        Exception e = ex;
        int depth = 0;
        while (e != null && depth < 8)
        {
            if (sb.Length > 0) sb.Append(" <- ");
            sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
            e = e.InnerException;
            depth++;
        }
        return sb.ToString();
    }

    static Assembly[] _asms;
    static Type FindType(string fullName)
    {
        if (_asms == null) _asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly a in _asms)
        {
            try { Type t = a.GetType(fullName); if (t != null) return t; }
            catch { }
        }
        string[] names = new string[] {
            "Eplan.EplApi.DataModelNetu", "Eplan.EplApi.HEServicesNetu",
            "Eplan.EplApi.DataModelu", "Eplan.EplApi.HEServicesu",
            "Eplan.EplApi.Baseu", "Eplan.EplApi.Systemu" };
        foreach (string n in names)
        {
            try
            {
                Assembly a = Assembly.Load(n);
                Type t = a.GetType(fullName);
                if (t != null) { _asms = AppDomain.CurrentDomain.GetAssemblies(); return t; }
            }
            catch { }
        }
        throw new Exception("type not found: " + fullName);
    }

    static PropertyInfo GetReadable(Type t, string name)
    {
        BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        Type cur = t;
        while (cur != null)
        {
            PropertyInfo pi = null;
            try { pi = cur.GetProperty(name, bf, null, null, Type.EmptyTypes, null); }
            catch { pi = null; }
            if (pi == null)
            {
                try { pi = cur.GetProperty(name, bf); } catch { pi = null; }
            }
            if (pi != null && pi.CanRead) return pi;
            cur = cur.BaseType;
        }
        return null;
    }

    static string PropText(object o, string name)
    {
        try
        {
            PropertyInfo pi = GetReadable(o.GetType(), name);
            if (pi == null) return null;
            object v = pi.GetValue(o, null);
            return v == null ? null : v.ToString();
        }
        catch { return null; }
    }

    static MethodInfo MethodByShape(Type t, string name, string[] pTypeNames, bool wantStatic)
    {
        Type cur = t;
        while (cur != null)
        {
            BindingFlags bf = BindingFlags.Public | BindingFlags.DeclaredOnly |
                (wantStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo[] all = null;
            try { all = cur.GetMethods(bf); } catch { all = null; }
            if (all != null)
            {
                foreach (MethodInfo mi in all)
                {
                    if (mi.Name != name) continue;
                    ParameterInfo[] ps = mi.GetParameters();
                    if (ps.Length != pTypeNames.Length) continue;
                    bool ok = true;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (pTypeNames[i].Length > 0 && ps[i].ParameterType.Name != pTypeNames[i])
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) return mi;
                }
            }
            cur = cur.BaseType;
        }
        return null;
    }

    static object CurrentProject()
    {
        Type ssType = FindType("Eplan.EplApi.HEServices.SelectionSet");
        object ss = Activator.CreateInstance(ssType);
        MethodInfo getCur = ssType.GetMethod("GetCurrentProject", new Type[] { typeof(bool) });
        object project = getCur.Invoke(ss, new object[] { false });
        if (project == null) throw new Exception("no current project");
        return project;
    }

    static object FindPage(object project, string wanted)
    {
        Type finderType = FindType("Eplan.EplApi.DataModel.DMObjectsFinder");
        object finder = Activator.CreateInstance(finderType, new object[] { project });
        Type filterType = FindType("Eplan.EplApi.DataModel.PagesFilter");
        object filter = Activator.CreateInstance(filterType);
        MethodInfo getPages = finderType.GetMethod("GetPages", new Type[] { filterType });
        IEnumerable pages = (IEnumerable)getPages.Invoke(finder, new object[] { filter });
        foreach (object p in pages)
        {
            if (p == null) continue;
            if (PropText(p, "Name") == wanted) return p;
        }
        throw new Exception("page not found: " + wanted);
    }

    [Start]
    public void Run()
    {
        var results = new Dictionary<string, object>();
        Type lsType = null;
        object lockStep = null;
        try
        {
            lsType = FindType("Eplan.EplApi.DataModel.LockingStep");
            lockStep = Activator.CreateInstance(lsType);
            object project = CurrentProject();
            object page = FindPage(project, "=DO+FB/1");

            try
            {
                Type genType = FindType("Eplan.EplApi.HEServices.Generate");
                object gen = Activator.CreateInstance(genType);
                MethodInfo connections = MethodByShape(genType, "Connections", new string[] { "Project" }, false);
                if (connections != null) connections.Invoke(gen, new object[] { project });
                results["connectionsGenerated"] = true;
            }
            catch (Exception gex)
            {
                results["connectionsGenerated"] = false;
                results["generateError"] = Flatten(gex);
            }

            string path = @"C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_export";
            Type exportType = FindType("Eplan.EplApi.HEServices.Export");
            object export = Activator.CreateInstance(exportType);
            Type formatType = exportType.GetNestedType("Format");
            Type compressionType = exportType.GetNestedType("Compression");
            object png = Enum.Parse(formatType, "PNG", false);
            object none = Enum.Parse(compressionType, "None", false);
            MethodInfo graphic = MethodByShape(exportType, "GraphicPage",
                new string[] { "Page", "String", "Format", "Int32", "Int32", "Compression", "Boolean" }, false);
            if (graphic == null) throw new Exception("GraphicPage overload not found");
            graphic.Invoke(export, new object[] { page, path, png, 24, 1800, none, false });
            results["image"] = path;
            results["exists"] = File.Exists(path);
            if (File.Exists(path)) results["bytes"] = new FileInfo(path).Length;
            results["success"] = true;
        }
        catch (Exception ex)
        {
            results["success"] = false;
            results["error"] = Flatten(ex);
        }
        finally
        {
            if (lockStep != null)
            {
                try { lsType.GetMethod("Dispose").Invoke(lockStep, null); }
                catch { }
            }
        }
        File.WriteAllText(@"{{RESULT_PATH}}", Newtonsoft.Json.JsonConvert.SerializeObject(results));
    }
}
