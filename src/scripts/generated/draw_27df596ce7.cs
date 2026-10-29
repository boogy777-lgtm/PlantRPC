using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class DrawFromSpec_20261028
{
    const string LIB = "GOST_symbol";
    const string PLANT = "DO";
    const string LOCATION = "FB";
    const int COUNTER = 1;
    const bool REPLACE_PAGE = true;

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

    static PropertyInfo GetWritable(Type t, string name)
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
            if (pi != null && pi.CanWrite) return pi;
            cur = cur.BaseType;
        }
        return null;
    }

    static PropertyInfo GetIndexed(Type t, string name)
    {
        BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        Type cur = t;
        while (cur != null)
        {
            PropertyInfo pi = null;
            try { pi = cur.GetProperty(name, bf, null, null, new Type[] { typeof(int) }, null); }
            catch { pi = null; }
            if (pi != null) return pi;
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

    static MethodInfo RequireMethod(Type t, string name, string[] pTypeNames, bool wantStatic)
    {
        MethodInfo mi = MethodByShape(t, name, pTypeNames, wantStatic);
        if (mi == null)
            throw new Exception("No " + (wantStatic ? "static " : "instance ") + name +
                "(" + string.Join(", ", pTypeNames) + ") on " + t.FullName);
        return mi;
    }

    static object Call(MethodInfo mi, object target, object[] args)
    {
        try { return mi.Invoke(target, args); }
        catch (TargetInvocationException tie)
        {
            throw new Exception(mi.DeclaringType.Name + "." + mi.Name + ": " + Flatten(tie.InnerException));
        }
    }

    static object MakePoint(Type ptType, double x, double y)
    {
        return Activator.CreateInstance(ptType, new object[] { x, y });
    }

    static object MakeValue(Type targetType, object raw)
    {
        MethodInfo conv = null;
        foreach (MethodInfo mi in targetType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (mi.Name != "op_Implicit" && mi.Name != "op_Explicit") continue;
            if (mi.ReturnType != targetType) continue;
            ParameterInfo[] ps = mi.GetParameters();
            if (ps.Length != 1) continue;
            if (ps[0].ParameterType != raw.GetType()) continue;
            conv = mi;
            break;
        }
        if (conv == null) throw new Exception("no conversion to " + targetType.FullName);
        return conv.Invoke(null, new object[] { raw });
    }

    static void SetProp(object propList, string name, object raw)
    {
        PropertyInfo pi = GetWritable(propList.GetType(), name);
        if (pi == null) throw new Exception("no writable " + name);
        pi.SetValue(propList, MakeValue(pi.PropertyType, raw), null);
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
        return null;
    }

    static object CreatePage(object project)
    {
        string wanted = "=" + PLANT + "+" + LOCATION + "/" + COUNTER;
        object existing = FindPage(project, wanted);
        if (existing != null)
        {
            if (!REPLACE_PAGE) return existing;
            MethodInfo remove = existing.GetType().GetMethod("Remove", Type.EmptyTypes);
            if (remove == null) throw new Exception("existing page has no Remove()");
            remove.Invoke(existing, null);
        }

        Type pageType = FindType("Eplan.EplApi.DataModel.Page");
        Type pplType = FindType("Eplan.EplApi.DataModel.PagePropertyList");
        Type dtmType = FindType("Eplan.EplApi.DataModel.DocumentTypeManager");
        Type docEnum = dtmType.GetNestedType("DocumentType");
        if (docEnum == null) throw new Exception("DocumentType enum not found");
        object docType = Enum.Parse(docEnum, "Circuit", false);

        object ppl = Activator.CreateInstance(pplType);
        SetProp(ppl, "DESIGNATION_PLANT", PLANT);
        SetProp(ppl, "DESIGNATION_LOCATION", LOCATION);
        SetProp(ppl, "PAGE_COUNTER", COUNTER);

        object page = Activator.CreateInstance(pageType);
        MethodInfo create = RequireMethod(pageType, "Create",
            new string[] { project.GetType().Name, docEnum.Name, pplType.Name }, false);
        Call(create, page, new object[] { project, docType, ppl });
        string name = PropText(page, "Name");
        if (string.IsNullOrEmpty(name)) throw new Exception("page created without name");
        return page;
    }

    static object Place(object project, object page, string symbolName, int variantNr,
                        string tag, double x, double y, string[] designations, bool hideContactImage)
    {
        Type libType = FindType("Eplan.EplApi.DataModel.MasterData.SymbolLibrary");
        Type symType = FindType("Eplan.EplApi.DataModel.MasterData.Symbol");
        Type varType = FindType("Eplan.EplApi.DataModel.MasterData.SymbolVariant");
        Type funcType = FindType("Eplan.EplApi.DataModel.Function");

        ConstructorInfo libCtor = libType.GetConstructor(new Type[] { project.GetType(), typeof(string) });
        object lib = libCtor.Invoke(new object[] { project, LIB });
        ConstructorInfo symCtor = symType.GetConstructor(new Type[] { libType, typeof(string) });
        object sym = symCtor.Invoke(new object[] { lib, symbolName });
        if (sym == null || PropText(sym, "IsValid") != "True")
            throw new Exception("symbol not valid: " + symbolName);

        ConstructorInfo varCtor = varType.GetConstructor(new Type[] { symType, typeof(int) });
        object variant = varCtor.Invoke(new object[] { sym, variantNr });

        MethodInfo create = RequireMethod(funcType, "Create",
            new string[] { "Page", "SymbolVariant" }, false);
        object fn = Activator.CreateInstance(funcType);
        Call(create, fn, new object[] { page, variant });

        PropertyInfo locProp = GetWritable(fn.GetType(), "Location");
        if (locProp == null) throw new Exception("Function has no writable Location");
        Type ptType = locProp.PropertyType;
        locProp.SetValue(fn, MakePoint(ptType, x, y), null);

        PropertyInfo nameProp = GetWritable(fn.GetType(), "Name");
        if (nameProp != null) nameProp.SetValue(fn, tag, null);
        PropertyInfo visProp = GetWritable(fn.GetType(), "VisibleName");
        if (visProp != null) visProp.SetValue(fn, tag, null);

        if (hideContactImage)
        {
            PropertyInfo ciType = GetWritable(fn.GetType(), "ContactImageType");
            if (ciType != null)
                ciType.SetValue(fn, Enum.Parse(ciType.PropertyType, "None", true), null);
        }

        if (designations != null && designations.Length > 0)
        {
            try
            {
                PropertyInfo propsProp = GetReadable(fn.GetType(), "Properties");
                object props = propsProp.GetValue(fn, null);
                PropertyInfo cdIdx = GetIndexed(props.GetType(), "FUNC_CONNECTIONDESIGNATION");
                if (cdIdx != null)
                {
                    for (int i = 0; i < designations.Length; i++)
                    {
                        object pv = cdIdx.GetValue(props, new object[] { i + 1 });
                        MethodInfo setter = pv.GetType().GetMethod("Set", new Type[] { typeof(string) });
                        if (setter != null) setter.Invoke(pv, new object[] { designations[i] });
                    }
                }
            }
            catch { }
        }

        return fn;
    }

    static void Wire(object page, double x1, double y1, double x2, double y2)
    {
        Type dclType = FindType("Eplan.EplApi.DataModel.DynamicConnectionLine");
        object dcl = Activator.CreateInstance(dclType);
        MethodInfo create = RequireMethod(dclType, "Create", new string[] { "Page" }, false);
        Call(create, dcl, new object[] { page });

        MethodInfo setG = RequireMethod(dclType, "SetGraphics",
            new string[] { "PointD", "PointD" }, false);
        Type ptType = setG.GetParameters()[0].ParameterType;

        PropertyInfo locProp = GetWritable(dclType, "Location");
        locProp.SetValue(dcl, MakePoint(ptType, x1, y1), null);
        Call(setG, dcl, new object[] {
            MakePoint(ptType, 0.0, 0.0),
            MakePoint(ptType, x2 - x1, y2 - y1) });
    }

    static void Text(object page, string text, double x, double y, double height)
    {
        Type tType = FindType("Eplan.EplApi.DataModel.Graphics.Text");
        object t = Activator.CreateInstance(tType);
        MethodInfo create = RequireMethod(tType, "Create",
            new string[] { "Page", "String", "Double" }, false);
        Call(create, t, new object[] { page, text, height });
        PropertyInfo loc = GetWritable(tType, "Location");
        Type ptType = loc.PropertyType;
        loc.SetValue(t, MakePoint(ptType, x, y), null);
    }

    static string ExportPage(object page, string path, int width)
    {
        Type exportType = FindType("Eplan.EplApi.HEServices.Export");
        object export = Activator.CreateInstance(exportType);
        Type formatType = exportType.GetNestedType("Format");
        Type compressionType = exportType.GetNestedType("Compression");
        object png = Enum.Parse(formatType, "PNG", false);
        object none = Enum.Parse(compressionType, "None", false);
        MethodInfo graphic = MethodByShape(exportType, "GraphicPage",
            new string[] { "Page", "String", "Format", "Int32", "Int32", "Compression", "Boolean" }, false);
        if (graphic == null) throw new Exception("GraphicPage overload not found");
        graphic.Invoke(export, new object[] { page, path, png, 24, width, none, false });
        return path;
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
            object page = CreatePage(project);
            results["page"] = PropText(page, "Name");
            results["success"] = true;

            object fn_0 = Place(project, page, "X", 0, "-X1", 90, 160, new string[] { "1", "2" }, false);
            object fn_1 = Place(project, page, "K", 0, "-K1", 90, 146, new string[] { "A1", "A2" }, true);
            object fn_2 = Place(project, page, "X", 0, "-X2", 90, 132, new string[] { "1", "2" }, false);
            object fn_3 = Place(project, page, "X", 0, "-X3", 170, 160, new string[] { "1", "2" }, false);
            object fn_4 = Place(project, page, "O", 0, "-K1", 170, 146, new string[] { "21", "22" }, false);
            object fn_5 = Place(project, page, "X", 0, "-X4", 170, 132, new string[] { "1", "2" }, false);
            object fn_6 = Place(project, page, "X", 0, "-X5", 250, 160, new string[] { "1", "2" }, false);
            object fn_7 = Place(project, page, "S", 0, "-K1", 250, 146, new string[] { "13", "14" }, false);
            object fn_8 = Place(project, page, "Y1", 0, "-Y1", 250, 132, new string[] { "1", "2" }, false);
            object fn_9 = Place(project, page, "X", 0, "-X6", 250, 118, new string[] { "1", "2" }, false);
            Wire(page, 90, 156, 90, 150);
            Wire(page, 90, 142, 90, 136);
            Wire(page, 170, 156, 170, 150);
            Wire(page, 170, 142, 170, 136);
            Wire(page, 250, 156, 250, 150);
            Wire(page, 250, 142, 250, 136);
            Wire(page, 250, 128, 250, 122);
            Text(page, "DO 24 V DC / 220 V AC", 40, 185, 3.5);
            Text(page, "DO +24 V (ПЛК)", 35, 168, 2.5);
            Text(page, "K1 катушка 24 V DC", 35, 154, 2.5);
            Text(page, "0 V", 35, 132, 2.5);
            Text(page, "Обратная связь", 115, 168, 2.5);
            Text(page, "+24 V", 115, 160, 2.5);
            Text(page, "K1 NC 21-22", 115, 154, 2.5);
            Text(page, "DI вход", 115, 132, 2.5);
            Text(page, "Нагрузка 220 V AC", 195, 168, 2.5);
            Text(page, "L 220 V", 195, 160, 2.5);
            Text(page, "K1 NO 13-14", 195, 154, 2.5);
            Text(page, "Y1", 195, 132, 2.5);
            Text(page, "N 220 V", 195, 118, 2.5);
            Text(page, "При отпущенном K1 NC замкнут: DI=1. При включении DO катушка K1 срабатывает, NC размыкается: DI=0.", 40, 105, 2.5);
            Text(page, "K1 NO 13-14 включает нагрузку 220 V AC. Катушка K1 питается от DO 24 V DC.", 40, 98, 2.5);
            results["placements"] = 10;
            results["wires"] = 7;
            results["texts"] = 15;
            results["exportPath"] = "C:\\Users\\HALLIBURTON\\Desktop\\EPLAN_RESEARCH\\artifacts\\wY_rpc\\do_fb_spec_export";
            ExportPage(page, "C:\\Users\\HALLIBURTON\\Desktop\\EPLAN_RESEARCH\\artifacts\\wY_rpc\\do_fb_spec_export", 1800);

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
