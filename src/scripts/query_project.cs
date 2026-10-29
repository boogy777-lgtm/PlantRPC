using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class QueryProject_20261028
{
    static string Flatten(Exception ex)
    {
        StringBuilder sb = new StringBuilder();
        Exception e = ex;
        int depth = 0;
        while (e != null && depth < 6)
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

    static PropertyInfo Prop(Type t, string name)
    {
        Type cur = t;
        while (cur != null)
        {
            PropertyInfo pi = null;
            try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); }
            catch { }
            if (pi == null) try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance); } catch { }
            if (pi != null) return pi;
            cur = cur.BaseType;
        }
        return null;
    }

    static string PropText(object o, string name)
    {
        try
        {
            PropertyInfo pi = Prop(o.GetType(), name);
            if (pi == null) return null;
            object v = pi.GetValue(o, null);
            return v == null ? null : v.ToString();
        }
        catch { return null; }
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
            results["project"] = PropText(project, "ProjectName");
            results["link"] = PropText(project, "ProjectLinkFilePath");

            Type finderType = FindType("Eplan.EplApi.DataModel.DMObjectsFinder");
            object finder = Activator.CreateInstance(finderType, new object[] { project });

            Type pagesFilter = FindType("Eplan.EplApi.DataModel.PagesFilter");
            object pf = Activator.CreateInstance(pagesFilter);
            MethodInfo getPages = finderType.GetMethod("GetPages", new Type[] { pagesFilter });
            IEnumerable pages = (IEnumerable)getPages.Invoke(finder, new object[] { pf });
            var pageList = new List<Dictionary<string, object>>();
            foreach (object p in pages)
            {
                if (p == null) continue;
                var d = new Dictionary<string, object>();
                d["name"] = PropText(p, "Name");
                d["type"] = PropText(p, "PageType");
                pageList.Add(d);
            }
            results["pages"] = pageList;
            results["pageCount"] = pageList.Count;

            var libs = new List<string>();
            object sl = null;
            try { sl = Prop(project.GetType(), "SymbolLibraries").GetValue(project, null); }
            catch { }
            if (sl is IEnumerable)
                foreach (object one in (IEnumerable)sl)
                {
                    if (one == null) continue;
                    string n = PropText(one, "Name");
                    libs.Add(n == null ? one.ToString() : n);
                }
            results["symbolLibraries"] = libs;

            Type fnFilter = FindType("Eplan.EplApi.DataModel.FunctionsFilter");
            object ff = Activator.CreateInstance(fnFilter);
            MethodInfo getFunctions = finderType.GetMethod("GetFunctions", new Type[] { fnFilter });
            IEnumerable fns = (IEnumerable)getFunctions.Invoke(finder, new object[] { ff });
            var fnList = new List<string>();
            foreach (object f in fns)
            {
                if (f == null) continue;
                if (fnList.Count >= 80) break;
                fnList.Add(PropText(f, "Name"));
            }
            results["functions"] = fnList;
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
