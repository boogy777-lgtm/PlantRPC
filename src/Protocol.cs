using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// The compact command language: <c>function.place symbol=X tag=-X1 x=40 y=180</c>.
    /// Values are named (<c>key=value</c>); a value containing spaces or commas is
    /// double-quoted. The JSON equivalent
    /// <c>{"cmd":"function.place","args":{"symbol":"X",...}}</c> is accepted
    /// unchanged. Object tokens start with @: <c>@page</c>, <c>@project</c>,
    /// <c>@handle:28/17/64/0</c>, <c>@variant:X:0</c>.
    /// </summary>
    internal static class Protocol
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        internal sealed class Request
        {
            public string Cmd;
            public Dictionary<string, object> Args = new Dictionary<string, object>();
            public string Raw;
        }

        internal static Request Parse(string line)
        {
            line = line.Trim();
            if (line.Length == 0) throw new ArgumentException("empty request");
            if (line[0] == '{') return FromJson(line);

            // Compact: first token is the command, the rest are key=value pairs.
            var toks = Tokenize(line);
            var req = new Request { Raw = line };
            if (toks.Count == 0) throw new ArgumentException("empty request");
            req.Cmd = toks[0];

            int i = 1;
            while (i < toks.Count)
            {
                string tok = toks[i];
                int eq = tok.IndexOf('=');
                if (eq < 0)
                {
                    // Bare value -> positional key "argN".
                    req.Args["arg" + (i - 1)] = tok;
                    i++;
                    continue;
                }
                string key = tok.Substring(0, eq).Trim();
                string val = tok.Substring(eq + 1).Trim();
                if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                    val = Unquote(val);
                if (key.Length == 0) throw new ArgumentException("empty key in token: " + tok);
                req.Args[key] = val;
                i++;
            }
            return req;
        }

        private static Request FromJson(string line)
        {
            var obj = Json.Deserialize<Dictionary<string, object>>(line);
            var req = new Request { Raw = line };
            req.Cmd = GetStr(obj, "cmd");
            if (req.Cmd == null) throw new ArgumentException("json request has no 'cmd'");
            if (obj.ContainsKey("args"))
            {
                var a = obj["args"] as Dictionary<string, object>;
                if (a != null) req.Args = a;
                else if (obj["args"] is System.Collections.IEnumerable)
                    foreach (var o in (System.Collections.IEnumerable)obj["args"])
                        if (o != null) req.Args["arg" + req.Args.Count] = o;
            }
            return req;
        }

        // ---- compact tokenizer: quotes preserve spaces/commas -----------------
        private static List<string> Tokenize(string s)
        {
            var toks = new List<string>();
            var cur = new StringBuilder();
            bool inq = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inq)
                {
                    if (c == '"') { inq = false; cur.Append(c); }
                    else cur.Append(c);
                    continue;
                }
                if (c == '"') { inq = true; cur.Append(c); continue; }
                if (c == ' ' || c == '\t')
                {
                    if (cur.Length > 0) { toks.Add(cur.ToString()); cur.Length = 0; }
                    continue;
                }
                cur.Append(c);
            }
            if (inq) throw new ArgumentException("unterminated quote");
            if (cur.Length > 0) toks.Add(cur.ToString());
            return toks;
        }

        private static string Unquote(string v)
        {
            // v begins and ends with a quote, kept by the tokenizer.
            return v.Substring(1, v.Length - 2);
        }

        private static string GetStr(Dictionary<string, object> d, string k)
        {
            if (d != null && d.ContainsKey(k) && d[k] != null) return Convert.ToString(d[k]);
            return null;
        }

        internal static string Serialize(Dictionary<string, object> resp)
        {
            return Json.Serialize(resp);
        }
    }
}
