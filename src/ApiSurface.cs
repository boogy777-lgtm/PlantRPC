using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// Serves the EPLAN public API surface (types / members / namespaces) from
    /// the pre-generated reflection dump (EPLAN_API_SURFACE.json). Metadata is
    /// identical in offline and online mode and needs no EPLAN runtime: only
    /// api.invoke and the typed regions touch a live backend.
    /// </summary>
    internal sealed class ApiSurface
    {
        private sealed class TypeRec
        {
            public string name;
            public string kind;
            public string @base;
            public List<string> members = new List<string>();
        }

        private Dictionary<string, List<TypeRec>> _ns = new Dictionary<string, List<TypeRec>>();
        private List<string> _namespaces = new List<string>();

        internal static ApiSurface Load(string path)
        {
            var surf = new ApiSurface();
            string text;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                text = File.ReadAllText(path);
            }
            else
            {
                var stream = typeof(ApiSurface).Assembly
                    .GetManifestResourceStream("EPLAN_API_SURFACE.json");
                if (stream == null)
                    throw new FileNotFoundException(
                        "API surface not found on disk or embedded: " + path);
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();
            }
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = js.Deserialize<Dictionary<string, object>>(text);

            object nsObj;
            if (root.TryGetValue("namespaces", out nsObj))
            {
                var nsMap = nsObj as Dictionary<string, object>;
                if (nsMap != null)
                {
                    foreach (var kv in nsMap)
                    {
                        var list = new List<TypeRec>();
                        // JavaScriptSerializer yields JSON arrays as ArrayList, not object[].
                        if (kv.Value is System.Collections.IEnumerable)
                        {
                            foreach (var item in (System.Collections.IEnumerable)kv.Value)
                            {
                                var d = item as Dictionary<string, object>;
                                if (d == null) continue;
                                var rec = new TypeRec();
                                rec.name = Str(d, "name");
                                rec.kind = Str(d, "kind");
                                rec.@base = Str(d, "base");
                                if (d.ContainsKey("members") && d["members"] is System.Collections.IEnumerable)
                                    foreach (var m in (System.Collections.IEnumerable)d["members"])
                                        if (m != null) rec.members.Add(Convert.ToString(m));
                                // Dedupe: the dump lists a type once per assembly.
                                if (!list.Any(x => x.name == rec.name)) list.Add(rec);
                            }
                        }
                        if (list.Count > 0)
                        {
                            surf._ns[kv.Key] = list;
                            surf._namespaces.Add(kv.Key);
                        }
                    }
                }
            }
            surf._namespaces.Sort(StringComparer.OrdinalIgnoreCase);
            return surf;
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object o;
            return d.TryGetValue(k, out o) && o != null ? Convert.ToString(o) : "";
        }

        internal object Namespaces(string contains)
        {
            var r = new List<string>();
            foreach (var n in _namespaces)
                if (Matches(n, contains)) r.Add(n);
            return new Dictionary<string, object> { { "count", r.Count }, { "namespaces", r } };
        }

        internal object Types(string ns, string contains, int limit)
        {
            List<TypeRec> list;
            if (!_ns.TryGetValue(ns, out list))
                throw new ArgumentException("unknown namespace: " + ns + ". Use api.regions or api.namespaces.");
            var r = new List<object>();
            int matched = 0;
            foreach (var t in list)
            {
                if (!Matches(t.name, contains)) continue;
                matched++;
                if (r.Count < limit)
                    r.Add(new Dictionary<string, object> { { "name", t.name }, { "kind", t.kind }, { "base", t.@base } });
            }
            return new Dictionary<string, object> { { "namespace", ns }, { "matched", matched }, { "returned", r.Count }, { "types", r } };
        }

        internal object Members(string typeName, string contains, int limit)
        {
            TypeRec rec = FindType(typeName);
            if (rec == null) throw new ArgumentException("unknown type: " + typeName);
            var r = new List<object>();
            int matched = 0;
            foreach (var m in rec.members)
            {
                if (!Matches(m, contains)) continue;
                matched++;
                if (r.Count < limit)
                {
                    string kind = m.StartsWith("ctor") ? "ctor"
                        : m.StartsWith("method") ? "method"
                        : m.StartsWith("prop") ? "prop"
                        : m.StartsWith("field") ? "field"
                        : m.StartsWith("const") ? "const"
                        : m.StartsWith("event") ? "event" : "?";
                    r.Add(new Dictionary<string, object> { { "kind", kind }, { "sig", m } });
                }
            }
            return new Dictionary<string, object> { { "type", rec.name }, { "kind", rec.kind }, { "base", rec.@base }, { "matched", matched }, { "returned", r.Count }, { "members", r } };
        }

        internal object Desc(string target)
        {
            TypeRec rec = FindType(target);
            if (rec != null)
                return new Dictionary<string, object> { { "type", rec.name }, { "kind", rec.kind }, { "base", rec.@base }, { "members", rec.members } };

            // target may be a member: try Type.Member and Type.Member(...).
            int dot = target.LastIndexOf('.');
            if (dot > 0)
            {
                string typeName = target.Substring(0, dot);
                string member = target.Substring(dot + 1);
                TypeRec t = FindType(typeName);
                if (t != null)
                {
                    var hits = t.members.Where(m =>
                    {
                        int sp = m.IndexOf(' ');
                        return sp >= 0 && m.Substring(sp + 1).StartsWith(member, StringComparison.Ordinal);
                    }).ToList();
                    if (hits.Count > 0)
                        return new Dictionary<string, object> { { "type", t.name }, { "member", member }, { "overloads", hits } };
                }
            }
            throw new ArgumentException("unknown type or member: " + target);
        }

        private TypeRec FindType(string typeName)
        {
            foreach (var list in _ns.Values)
                foreach (var t in list)
                    if (t.name == typeName) return t;
            return null;
        }

        private static bool Matches(string value, string contains)
        {
            if (string.IsNullOrEmpty(contains)) return true;
            if (value == null) return false;
            return value.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
