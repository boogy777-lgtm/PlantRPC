using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class ListDoPlacements_20261028
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

    static Dictionary<string, object> Pt(object pt)
    {
        var d = new Dictionary<string, object>();
        if (pt == null) return d;
        try
        {
            PropertyInfo px = GetReadable(pt.GetType(), "X");
            PropertyInfo py = GetReadable(pt.GetType(), "Y");
            if (px != null) d["x"] = Math.Round(Convert.ToDouble(px.GetValue(pt, null)), 4);
            if (py != null) d["y"] = Math.Round(Convert.ToDouble(py.GetValue(pt, null)), 4);
        }
        catch { }
        return d;
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
            PropertyInfo allProp = GetReadable(page.GetType(), "AllPlacements");
            IEnumerable all = (IEnumerable)allProp.GetValue(page, null);
            var list = new List<Dictionary<string, object>>();
            foreach (object pl in all)
            {
                if (pl == null) continue;
                var d = new Dictionary<string, object>();
                d["type"] = pl.GetType().Name;
                d["name"] = PropText(pl, "Name");
                PropertyInfo loc = GetReadable(pl.GetType(), "Location");
                if (loc != null) d["location"] = Pt(loc.GetValue(pl, null));
                try
                {
                    MethodInfo bbox = pl.GetType().GetMethod("GetBoundingBox", Type.EmptyTypes);
                    if (bbox != null)
                    {
                        object bb = bbox.Invoke(pl, null);
                        if (bb is IEnumerable)
                        {
                            var pts = new List<object>();
                            foreach (object p in (IEnumerable)bb)
                                if (p != null) pts.Add(Pt(p));
                            d["bbox"] = pts;
                        }
                    }
                }
                catch { }
                list.Add(d);
            }
            results["placements"] = list;
            results["count"] = list.Count;
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
