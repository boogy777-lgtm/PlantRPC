using System;
using System.Collections.Generic;
using System.IO;

namespace EplanRpc
{
    /// <summary>
    /// Shared server state. Offline holds typed Project/Page objects; online
    /// holds the gRPC remoting client. project/page are the server-side state
    /// the compact protocol refers to with @project / @page.
    /// </summary>
    internal static class S
    {
        internal const string VariantBin = @"C:\Program Files\EPLAN\Electric P8\2026.0.3\Bin";
        internal const string TemplatesDir =
            @"C:\ProgramData\EPLAN\O_Data\Electric P8 Data\2026.0.3\Templates\EPLAN";
        internal const string TemplatePath = TemplatesDir + @"\IEC_bas001.zw9";
        // GOST_bas001.zw9 registers GOST_symbol, the library the schematic
        // renderer defaults to; IEC_bas001 carries only IEC_symbol.
        internal const string GostTemplate = "GOST_bas001";
        internal const string DefaultLib = "IEC_symbol";

        internal static string Mode = "offline";
        internal static ApiSurface Surface;

        // offline
        internal static Eplan.EplApi.DataModel.ProjectManager Pm;
        internal static Eplan.EplApi.DataModel.Project Project;
        internal static Eplan.EplApi.DataModel.Page Page;

        // online
        internal static object RemoteClient;
        internal static string RemoteHost;
        internal static string RemotePort;

        internal static string OutDir
        {
            get { return Path.Combine(Path.GetDirectoryName(typeof(S).Assembly.Location) ?? ".", "out"); }
        }

        internal static string GetStr(Dictionary<string, object> a, string k, string def)
        {
            if (a != null && a.ContainsKey(k) && a[k] != null) return Convert.ToString(a[k]);
            return def;
        }

        internal static double GetDouble(Dictionary<string, object> a, string k, double def)
        {
            if (a != null && a.ContainsKey(k) && a[k] != null) return Convert.ToDouble(a[k]);
            return def;
        }

        internal static int GetInt(Dictionary<string, object> a, string k, int def)
        {
            if (a != null && a.ContainsKey(k) && a[k] != null) return Convert.ToInt32(a[k]);
            return def;
        }

        internal static bool GetBool(Dictionary<string, object> a, string k, bool def)
        {
            if (a != null && a.ContainsKey(k) && a[k] != null)
            {
                object v = a[k];
                if (v is bool) return (bool)v;
                return Convert.ToString(v).ToLowerInvariant() == "true";
            }
            return def;
        }

        internal static List<object> GetArray(Dictionary<string, object> a, string k)
        {
            if (a != null && a.ContainsKey(k) && a[k] is object[])
                return new List<object>((object[])a[k]);
            return new List<object>();
        }

        internal static Dictionary<string, object> State()
        {
            var d = new Dictionary<string, object>
            {
                { "mode", Mode },
                { "online", Mode == "online" ? (RemoteClient != null) : false },
                { "onlineHost", RemoteHost },
                { "onlinePort", RemotePort },
            };
            if (Mode == "offline")
            {
                d["projectOpen"] = Project != null;
                d["project"] = Project == null ? null : Project.ProjectLinkFilePath;
                d["page"] = Page == null ? null : Page.Name;
            }
            return d;
        }
    }
}
