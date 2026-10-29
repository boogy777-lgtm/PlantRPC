using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class QuerySymbols_20261028
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

            Type libType = FindType("Eplan.EplApi.DataModel.MasterData.SymbolLibrary");
            Type symType = FindType("Eplan.EplApi.DataModel.MasterData.Symbol");
            Type varType = FindType("Eplan.EplApi.DataModel.MasterData.SymbolVariant");
            ConstructorInfo libCtor = libType.GetConstructor(new Type[] { project.GetType(), typeof(string) });
            object lib = libCtor.Invoke(new object[] { project, "GOST_symbol" });
            ConstructorInfo symCtor = symType.GetConstructor(new Type[] { libType, typeof(int) });
            ConstructorInfo varCtor = varType.GetConstructor(new Type[] { symType, typeof(int) });

            var syms = new List<Dictionary<string, object>>();
            for (int i = 0; i < 2000 && syms.Count < 220; i++)
            {
                object sym = null;
                try { sym = symCtor.Invoke(new object[] { lib, i }); }
                catch { continue; }
                if (sym == null) continue;
                string valid = PropText(sym, "IsValid");
                if (valid != "True") continue;
                string name = PropText(sym, "Name");
                if (name == null) continue;
                string type = PropText(sym, "Type");

                object variant = null;
                try { variant = varCtor.Invoke(new object[] { sym, 0 }); }
                catch { }
                var pins = new List<Dictionary<string, object>>();
                object cps = null;
                if (variant != null) cps = Prop(variant.GetType(), "ConnectionPoints") == null ? null : Prop(variant.GetType(), "ConnectionPoints").GetValue(variant, null);
                if (cps is IEnumerable)
                {
                    int pi = 0;
                    foreach (object cp in (IEnumerable)cps)
                    {
                        if (cp == null) { pi++; continue; }
                        var pd = new Dictionary<string, object>();
                        pd["i"] = pi;
                        pd["n"] = PropText(cp, "Designation");
                        pd["d"] = PropText(cp, "Direction");
                        object loc = Prop(cp.GetType(), "Location") == null ? null : Prop(cp.GetType(), "Location").GetValue(cp, null);
                        if (loc != null)
                        {
                            object x = Prop(loc.GetType(), "X").GetValue(loc, null);
                            object y = Prop(loc.GetType(), "Y").GetValue(loc, null);
                            pd["x"] = x == null ? null : x.ToString();
                            pd["y"] = y == null ? null : y.ToString();
                        }
                        pins.Add(pd);
                        pi++;
                    }
                }
                var d = new Dictionary<string, object>();
                d["index"] = i;
                d["name"] = name;
                d["type"] = type;
                d["pins"] = pins;
                syms.Add(d);
            }
            results["symbols"] = syms;
            results["count"] = syms.Count;
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
