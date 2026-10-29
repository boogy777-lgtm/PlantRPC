using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class ProbePins_20261029
{
    static string Flatten(Exception ex)
    {
        StringBuilder sb = new StringBuilder();
        Exception e = ex; int d = 0;
        while (e != null && d < 8)
        {
            if (sb.Length > 0) sb.Append(" <- ");
            sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
            e = e.InnerException; d++;
        }
        return sb.ToString();
    }

    static object CurrentProject()
    {
        Type ssType = typeof(Eplan.EplApi.HEServices.SelectionSet);
        object ss = Activator.CreateInstance(ssType);
        MethodInfo getCur = ssType.GetMethod("GetCurrentProject", new Type[] { typeof(bool) });
        object project = getCur.Invoke(ss, new object[] { false });
        if (project == null) throw new Exception("no current project");
        return project;
    }

    static object FindPage(object project, string wanted)
    {
        Type finderType = typeof(Eplan.EplApi.DataModel.DMObjectsFinder);
        object finder = Activator.CreateInstance(finderType, new object[] { project });
        Type filterType = typeof(Eplan.EplApi.DataModel.PagesFilter);
        object filter = Activator.CreateInstance(filterType);
        MethodInfo getPages = finderType.GetMethod("GetPages", new Type[] { filterType });
        IEnumerable pages = (IEnumerable)getPages.Invoke(finder, new object[] { filter });
        foreach (object p in pages)
        {
            if (p == null) continue;
            PropertyInfo np = p.GetType().GetProperty("Name");
            if (Convert.ToString(np.GetValue(p, null)) == wanted) return p;
        }
        return null;
    }

    static string Pt(object pt)
    {
        if (pt == null) return "null";
        PropertyInfo px = pt.GetType().GetProperty("X");
        PropertyInfo py = pt.GetType().GetProperty("Y");
        double x = Convert.ToDouble(px.GetValue(pt, null));
        double y = Convert.ToDouble(py.GetValue(pt, null));
        return x.ToString("0.###") + ";" + y.ToString("0.###");
    }

    [Start]
    public void Run()
    {
        var res = new Dictionary<string, object>();
        try
        {
            object project = CurrentProject();
            Type pt = typeof(Eplan.EplApi.DataModel.Page);
            object page = FindPage(project, "=DO+FB/1");
            if (page == null) { res["page"] = "not found"; }
            else
            {
                res["page"] = "=DO+FB/1";
                var rows = new List<object>();
                PropertyInfo fp = page.GetType().GetProperty("Functions");
                IEnumerable funcs = (IEnumerable)fp.GetValue(page, null);
                foreach (object fn in funcs)
                {
                    if (fn == null) continue;
                    var row = new Dictionary<string, object>();
                    Type ft = fn.GetType();
                    row["name"] = Convert.ToString(ft.GetProperty("Name").GetValue(fn, null));
                    PropertyInfo lp = ft.GetProperty("Location");
                    object loc = lp == null ? null : lp.GetValue(fn, null);
                    row["at"] = Pt(loc);
                    PropertyInfo loc2 = ft.GetProperty("AreConnectionPointPositionsLocal");
                    if (loc2 != null)
                    {
                        try
                        {
                            row["local"] = Convert.ToString(loc2.GetValue(fn, null));
                            loc2.SetValue(fn, false, null);
                        }
                        catch (Exception ex) { row["localErr"] = Flatten(ex); }
                    }
                    PropertyInfo cpp = ft.GetProperty("ConnectionPointPositions");
                    if (cpp != null)
                    {
                        var cps = new List<object>();
                        IEnumerable arr = (IEnumerable)cpp.GetValue(fn, null);
                        int i = 0;
                        if (arr != null)
                            foreach (object cp in arr)
                            {
                                var c = new Dictionary<string, object>();
                                c["i"] = i++;
                                PropertyInfo xp = cp.GetType().GetProperty("X");
                                PropertyInfo yp = cp.GetType().GetProperty("Y");
                                if (xp != null && yp != null)
                                    c["abs"] = Convert.ToDouble(xp.GetValue(cp, null)).ToString("0.###") + ";" +
                                               Convert.ToDouble(yp.GetValue(cp, null)).ToString("0.###");
                                var locp = cp.GetType().GetProperty("Location");
                                if (locp != null) c["loc"] = Pt(locp.GetValue(cp, null));
                                cps.Add(c);
                            }
                        row["cp"] = cps;
                    }
                    PropertyInfo pp = ft.GetProperty("Pins");
                    if (pp != null)
                    {
                        var ps = new List<object>();
                        IEnumerable arr = (IEnumerable)pp.GetValue(fn, null);
                        int i = 0;
                        if (arr != null)
                            foreach (object pin in arr)
                            {
                                if (pin == null) { i++; continue; }
                                var c = new Dictionary<string, object>();
                                c["i"] = i++;
                                PropertyInfo idx = pin.GetType().GetProperty("Index");
                                if (idx != null) c["idx"] = Convert.ToString(idx.GetValue(pin, null));
                                PropertyInfo lp2 = pin.GetType().GetProperty("Location");
                                if (lp2 != null) c["loc"] = Pt(lp2.GetValue(pin, null));
                                ps.Add(c);
                            }
                        row["pins"] = ps;
                    }
                    var svp = ft.GetProperty("SymbolVariant");
                    if (svp != null)
                    {
                        object sv = svp.GetValue(fn, null);
                        if (sv != null)
                        {
                            var vp = sv.GetType().GetProperty("ConnectionPoints");
                            if (vp != null)
                            {
                                var vs = new List<object>();
                                IEnumerable arr = (IEnumerable)vp.GetValue(sv, null);
                                int i = 0;
                                if (arr != null)
                                    foreach (object cp in arr)
                                    {
                                        if (cp == null) { i++; continue; }
                                        var c = new Dictionary<string, object>();
                                        c["i"] = i++;
                                        PropertyInfo idx = cp.GetType().GetProperty("Index");
                                        if (idx != null) c["idx"] = Convert.ToString(idx.GetValue(cp, null));
                                        PropertyInfo lp3 = cp.GetType().GetProperty("Location");
                                        if (lp3 != null) c["loc"] = Pt(lp3.GetValue(cp, null));
                                        vs.Add(c);
                                    }
                                row["variant"] = vs;
                            }
                        }
                    }
                    rows.Add(row);
                }
                res["functions"] = rows;
            }
            res["success"] = true;
        }
        catch (Exception ex) { res["success"] = false; res["error"] = Flatten(ex); }
        File.WriteAllText(@"{{RESULT_PATH}}", Newtonsoft.Json.JsonConvert.SerializeObject(res));
    }
}
