using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EplanRpc
{
    /// <summary>
    /// Loads the EPLAN Remoting assemblies at RUNTIME (no compile-time
    /// reference) and talks to a running EPLAN GUI over gRPC Remoting, the same
    /// way the pythonnet MCP bridge does. Runtime-load is load-bearing: a
    /// compile-time reference drags stale System.Memory/System.Buffers/
    /// System.Runtime.CompilerServices.Unsafe copies into the app directory and
    /// a binding-redirect config, and then gRPC answers every call with
    /// "Failed to deserialize response message" while the identical client
    /// loaded through pythonnet works. Loading the real assemblies out of
    /// Platform\Bin with a single AssemblyResolve probe avoids both.
    /// </summary>
    internal static class Remoting
    {
        internal static int Probe()
        {
            Boot();

            object client = Refl.New("Eplan.EplApi.RemoteClient.EplanRemoteClient", null);

            // Enumerate the remoting servers EPLAN advertises on this machine.
            var servers = new List<object>();
            try
            {
                object list = Refl.OutList(client, "GetActiveEplanServersOnLocalMachine");
                if (list != null)
                    foreach (object s in (IEnumerable)list) servers.Add(s);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  server enumeration failed: " + ex.Message);
            }

            Console.WriteLine("active servers: " + servers.Count);
            var ports = new List<string>();
            foreach (object s in servers)
            {
                Console.WriteLine("  pid=" + Refl.GetProp(s, "EplanProcessID") +
                    " name=" + Refl.GetProp(s, "ServerName") +
                    " port=" + Refl.GetProp(s, "ServerPort") +
                    " ver=" + Refl.GetProp(s, "EplanVersion"));
                string p = Convert.ToString(Refl.GetProp(s, "ServerPort"));
                if (!string.IsNullOrEmpty(p) && !ports.Contains(p)) ports.Add(p);
            }
            if (ports.Count == 0) ports.Add("49152");

            var hosts = new List<string> { "127.0.0.1", "localhost" };
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !hosts.Contains(ip.ToString()))
                        hosts.Add(ip.ToString());
            }
            catch { }

            var timeout = TimeSpan.FromSeconds(15);
            foreach (string port in ports)
            {
                foreach (string host in hosts)
                {
                    object c = null;
                    try
                    {
                        c = Refl.New("Eplan.EplApi.RemoteClient.EplanRemoteClient", null);
                        Console.WriteLine("try " + host + ":" + port + " ...");
                        Refl.Call(c, "Connect", host, port, timeout);
                        bool ping = (bool)Refl.Call(c, "Ping");
                        Console.WriteLine("  ping=" + ping);
                        if (ping)
                        {
                            Console.WriteLine("CONNECTED " + host + ":" + port);
                            try { Refl.Call(c, "Disconnect"); } catch { }
                            try { Refl.Call(c, "Dispose"); } catch { }
                            return 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("  fail: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        if (c != null)
                        {
                            try { Refl.Call(c, "Dispose"); } catch { }
                        }
                    }
                }
            }

            Console.Error.WriteLine("No reachable EPLAN remoting endpoint.");
            return 3;
        }

        internal static bool Booted;
        private static bool _resolverBooted;

        /// <summary>
        /// Directories probed for EPLAN/gRPC/System.* dependencies, in order.
        /// System.Memory / System.Buffers / System.Runtime.CompilerServices.Unsafe /
        /// Newtonsoft.Json live in IdentityClient, NOT in Platform\Bin — so probing
        /// only Platform\Bin reproduces "Could not load System.Memory 4.0.1.1".
        /// </summary>
        internal static readonly string[] ProbeDirs =
        {
            Program.PlatformBin,
            @"C:\Program Files\EPLAN\Common\IdentityClient",
        };

        /// <summary>
        /// Register the assembly resolver + native DLL directory. Needed for BOTH
        /// modes: the offline surface is compiled against Eplan.EplApi.*u.dll with
        /// Private=false, so those (and their native deps) resolve through here.
        /// Must run before any code path that JITs a type referencing Eplan
        /// assemblies — even a null field of type DataModel.Project forces the load.
        /// </summary>
        internal static void EnsureResolver()
        {
            if (_resolverBooted) return;
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string name = new AssemblyName(e.Name).Name;
                foreach (string dir in ProbeDirs)
                {
                    string path = Path.Combine(dir, name + ".dll");
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            SetDllDirectory(Program.PlatformBin);
            _resolverBooted = true;
        }

        internal static void Boot()
        {
            EnsureResolver();
            if (Booted) return;
            foreach (string a in new[] { "Eplan.EplApi.Starteru", "Eplan.EplApi.RemoteClientu", "Eplan.EplApi.Remotingu" })
                Assembly.LoadFrom(Path.Combine(Program.PlatformBin, a + ".dll"));
            Booted = true;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }

    /// <summary>
    /// Tiny reflection helpers shared by the remoting client, the generic
    /// api.invoke surface and the online backend. Every reflective call goes
    /// through Call() so a TargetInvocationException is flattened into the real
    /// inner error instead of "Exception has been thrown by the target of an
    /// invocation".
    /// </summary>
    internal static class Refl
    {
        internal static Type Find(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(fullName); } catch { }
                if (t != null) return t;
            }
            throw new Exception("Type not found in loaded assemblies: " + fullName);
        }

        internal static object New(string fullName, object[] args)
        {
            Type t = Find(fullName);
            if (args == null || args.Length == 0) return Activator.CreateInstance(t);
            var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType();
            ConstructorInfo c = t.GetConstructor(types);
            if (c == null)
                throw new MissingMethodException("No ctor " + fullName + "(" + Join(types) + ")");
            try { return c.Invoke(args); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        internal static object Call(object target, string method, params object[] args)
        {
            Type t = target.GetType();
            MethodInfo mi = Match(t, method, args);
            if (mi == null)
                throw new MissingMethodException("No method " + t.FullName + "." + method + "(" +
                    Join(ArgTypes(args)) + ")");
            try { return mi.Invoke(target, args); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        internal static object GetProp(object target, string name)
        {
            var pi = Prop(target.GetType(), name);
            if (pi == null) return null;
            try { return pi.GetValue(target, null); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        internal static void SetProp(object target, string name, object value)
        {
            var pi = Prop(target.GetType(), name);
            if (pi == null) throw new Exception("No property " + name);
            try { pi.SetValue(target, value, null); }
            catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
        }

        /// <summary>
        /// EPLAN's GetActiveEplanServersOnLocalMachine takes an OUT List&lt;T&gt;.
        /// Build the correct closed List type, invoke with the out slot, and
        /// return the populated instance.
        /// </summary>
        internal static object OutList(object target, string method)
        {
            Type t = target.GetType();
            foreach (MethodInfo mi in t.GetMethods())
            {
                if (mi.Name != method) continue;
                var ps = mi.GetParameters();
                if (ps.Length != 1 || !ps[0].ParameterType.IsByRef) continue;
                Type listType = ps[0].ParameterType.GetElementType();
                object list = Activator.CreateInstance(listType);
                var slot = new object[] { list };
                try { mi.Invoke(target, slot); }
                catch (TargetInvocationException tie) { throw new Exception(Flatten(tie.InnerException)); }
                return slot[0];
            }
            throw new MissingMethodException("No out-param method " + method);
        }

        private static MethodInfo Match(Type t, string name, object[] args)
        {
            Type[] at = ArgTypes(args);
            MethodInfo best = null;
            foreach (MethodInfo mi in t.GetMethods())
            {
                if (mi.Name != name) continue;
                var ps = mi.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt.IsByRef) pt = pt.GetElementType();
                    if (!pt.IsAssignableFrom(at[i])) { ok = false; break; }
                }
                if (ok) { best = mi; break; }
            }
            return best;
        }

        private static PropertyInfo Prop(Type t, string name)
        {
            // Type.EmptyTypes pins the non-indexed declaration; a bare
            // GetProperty(name) throws AmbiguousMatchException on EPLAN types
            // that declare a plain and an indexed form of the same member.
            Type cur = t;
            while (cur != null)
            {
                PropertyInfo pi = null;
                try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly, null, null, Type.EmptyTypes, null); }
                catch { }
                if (pi == null)
                    try { pi = cur.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static); } catch { }
                if (pi != null) return pi;
                cur = cur.BaseType;
            }
            return null;
        }

        private static Type[] ArgTypes(object[] args)
        {
            if (args == null) return new Type[0];
            var t = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) t[i] = args[i].GetType();
            return t;
        }

        private static string Join(Type[] t)
        {
            var s = new List<string>();
            foreach (var x in t) s.Add(x.Name);
            return string.Join(", ", s.ToArray());
        }

        private static string Flatten(Exception e)
        {
            var parts = new List<string>();
            Exception x = e;
            int d = 0;
            while (x != null && d < 6)
            {
                parts.Add(x.GetType().Name + ": " + x.Message);
                x = x.InnerException;
                d++;
            }
            return string.Join(" <- ", parts.ToArray());
        }
    }
}
