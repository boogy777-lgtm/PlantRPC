using System;
using System.Collections.Generic;

namespace EplanRpc
{
    internal sealed class ApiCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return new CommandDef("api.regions", "api", "", "obj", "api.regions",
                    "region -> namespace map", CommandMode.Any, a => Regions());
                yield return new CommandDef("api.namespaces", "api", "contains=", "obj",
                    "api.namespaces contains=DataModel", "list namespaces", CommandMode.Any,
                    a => S.Surface.Namespaces(S.GetStr(a, "contains", null)));
                yield return new CommandDef("api.types", "api", "ns contains= limit=", "obj",
                    "api.types ns=Eplan.EplApi.DataModel", "types in a namespace", CommandMode.Any,
                    a => S.Surface.Types(S.GetStr(a, "ns", ""), S.GetStr(a, "contains", null),
                                         S.GetInt(a, "limit", 100)));
                yield return new CommandDef("api.members", "api", "type contains= limit=", "obj",
                    "api.members type=Eplan.EplApi.DataModel.Function", "members of a type",
                    CommandMode.Any,
                    a => S.Surface.Members(S.GetStr(a, "type", ""), S.GetStr(a, "contains", null),
                                           S.GetInt(a, "limit", 200)));
                yield return new CommandDef("api.desc", "api", "target", "obj",
                    "api.desc Eplan.EplApi.DataModel.Function", "description/signatures of a type or member",
                    CommandMode.Any, a =>
                    {
                        string target = S.GetStr(a, "target", null);
                        if (string.IsNullOrEmpty(target)) target = S.GetStr(a, "arg0", null);
                        return S.Surface.Desc(target);
                    });
                yield return new CommandDef("api.invoke", "api", "target k=v...", "obj",
                    "api.invoke DataModel.Function.Create page=@page variant=@variant:X:0",
                    "generic reflective call (static/ctor/instance)", CommandMode.Any, Invoke);
            }
        }

        private static object Invoke(Dictionary<string, object> a)
        {
            string target = S.GetStr(a, "target", null);
            if (string.IsNullOrEmpty(target)) target = S.GetStr(a, "arg0", null);
            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("api.invoke needs a target (Type.Member)");
            var args = new Dictionary<string, object>(a);
            args.Remove("target");
            args.Remove("arg0");
            return S.Mode == "online"
                ? Online.ApiInvoke(target, args)
                : Offline.ApiInvoke(target, args);
        }

        private static object Regions()
        {
            return new Dictionary<string, object>
            {
                { "system", "mode/version/backend" },
                { "project", "Eplan.EplApi.DataModel.Project + ProjectManager" },
                { "page", "Eplan.EplApi.DataModel.Page" },
                { "symbol", "Eplan.EplApi.DataModel.MasterData.SymbolLibrary/Symbol/SymbolVariant" },
                { "function", "Eplan.EplApi.DataModel.Function" },
                { "connection", "Eplan.EplApi.DataModel.Connection + DynamicConnectionLine" },
                { "graphics", "Eplan.EplApi.DataModel.Graphics.*" },
                { "generate", "Eplan.EplApi.HEServices.Generate" },
                { "export", "Eplan.EplApi.HEServices.Export" },
                { "action", "Eplan.EplApi.ApplicationFramework.ActionManager" },
                { "settings", "Eplan.EplApi.Base.Settings" },
                { "masterdata", "Eplan.EplApi.MasterData.MDPartsManagement" },
                { "remoting", "Eplan.EplApi.RemoteClient.EplanRemoteClient" },
            };
        }
    }
}
