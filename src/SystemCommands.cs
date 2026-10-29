using System;
using System.Collections.Generic;

namespace EplanRpc
{
    internal sealed class SystemCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return CmdDef("ping", "system", "", "str", "ping", "health check",
                    CommandMode.Any, a => "pong");
                yield return CmdDef("state", "system", "", "obj", "state",
                    "server state: mode, project, page, online endpoint",
                    CommandMode.Any, a => S.State());
                yield return CmdDef("shutdown", "system", "", "str", "shutdown", "stop the server",
                    CommandMode.Any, a => "bye");
                yield return CmdDef("meta.commands", "system", "", "obj", "meta.commands",
                    "command dictionary (name/region/args/example)",
                    CommandMode.Any, a => Cmd.ListCommands());
                yield return CmdDef("meta.regions", "system", "", "obj", "meta.regions", "region list",
                    CommandMode.Any, a => Cmd.ListRegions());

                yield return CmdDef("system.init", "system", "", "str", "system.init",
                    "offline: EplApplication.Init", CommandMode.Any,
                    a => { if (S.Mode == "offline") Offline.Init(); return "ok"; });
                yield return CmdDef("system.version", "system", "", "str", "system.version",
                    "EPLAN version string", CommandMode.Any, a => "2026.0.3.25702");
                yield return CmdDef("system.mode", "system", "", "str", "system.mode",
                    "current mode offline|online", CommandMode.Any, a => S.Mode);
            }
        }

        private static CommandDef CmdDef(string name, string region, string args, string ret,
                                         string example, string desc, CommandMode mode,
                                         Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc, mode, handler);
        }
    }
}
