using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace EplanRpc
{
    /// <summary>
    /// Symbol pin geometry: symbol name -> ordered [x,y] offsets of its
    /// connection points. Derived from SymbolVariant connection-point geometry
    /// (GOST_symbol); shipped as an embedded resource so the offline server can
    /// resolve wire endpoints without the external symbol_pins.json file.
    /// </summary>
    internal static class PinMap
    {
        private static Dictionary<string, double[][]> _map;

        internal static double[] Get(string symbol, int index)
        {
            Load();
            double[][] pins;
            if (!_map.TryGetValue(symbol, out pins) || pins == null)
                return null;
            if (index < 0 || index >= pins.Length)
                return null;
            return pins[index];
        }

        private static void Load()
        {
            if (_map != null) return;
            string text;
            string disk = Path.Combine(
                Path.GetDirectoryName(typeof(PinMap).Assembly.Location) ?? ".", "symbol_pins.json");
            if (File.Exists(disk))
            {
                text = File.ReadAllText(disk);
            }
            else
            {
                var stream = typeof(PinMap).Assembly.GetManifestResourceStream("symbol_pins.json");
                if (stream == null)
                    throw new FileNotFoundException("symbol_pins.json not found on disk or embedded");
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();
            }

            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = js.Deserialize<Dictionary<string, object>>(text);
            _map = new Dictionary<string, double[][]>();
            foreach (var kv in root)
            {
                var list = new List<double[]>();
                if (kv.Value is System.Collections.IEnumerable)
                {
                    foreach (var item in (System.Collections.IEnumerable)kv.Value)
                    {
                        var xy = new double[2];
                        if (item is System.Collections.IEnumerable)
                        {
                            int n = 0;
                            foreach (var v in (System.Collections.IEnumerable)item)
                            {
                                if (n < 2)
                                    try { xy[n] = Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); }
                                    catch { }
                                n++;
                            }
                        }
                        list.Add(xy);
                    }
                }
                _map[kv.Key] = list.ToArray();
            }
        }
    }
}
