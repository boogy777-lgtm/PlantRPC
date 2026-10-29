using System;
using System.Collections.Generic;
using System.Linq;

namespace EplanRpc
{
    internal enum CommandMode
    {
        Any,
        Offline,
        Online
    }

    /// <summary>
    /// One command definition: stable metadata plus the handler that executes it.
    /// The handler is the only executable part; metadata is not duplicated in a
    /// second list, so docs and dispatch cannot drift.
    /// </summary>
    internal sealed class CommandDef
    {
        internal readonly string Name;
        internal readonly string Region;
        internal readonly string Args;
        internal readonly string Ret;
        internal readonly string Example;
        internal readonly string Description;
        internal readonly CommandMode Mode;
        internal readonly Func<Dictionary<string, object>, object> Handler;

        internal CommandDef(string name, string region, string args, string ret,
                            string example, string description, CommandMode mode,
                            Func<Dictionary<string, object>, object> handler)
        {
            Name = name;
            Region = region;
            Args = args;
            Ret = ret;
            Example = example;
            Description = description;
            Mode = mode;
            Handler = handler;
        }
    }

    internal interface ICommandModule
    {
        IEnumerable<CommandDef> Commands { get; }
    }

    /// <summary>
    /// Thin dispatcher. It owns command identity, mode admission and metadata;
    /// domain behaviour stays in the modules. Adding a command touches exactly
    /// one module and no registry code.
    /// </summary>
    internal sealed class CommandRegistry
    {
        private readonly Dictionary<string, CommandDef> _byName =
            new Dictionary<string, CommandDef>(StringComparer.Ordinal);
        private readonly List<CommandDef> _ordered = new List<CommandDef>();

        internal void Add(ICommandModule module)
        {
            foreach (var def in module.Commands)
            {
                if (_byName.ContainsKey(def.Name))
                    throw new InvalidOperationException("duplicate command: " + def.Name);
                _byName.Add(def.Name, def);
                _ordered.Add(def);
            }
        }

        internal object Dispatch(string cmd, Dictionary<string, object> args)
        {
            CommandDef def;
            if (!_byName.TryGetValue(cmd, out def))
                throw new ArgumentException("unknown cmd: " + cmd);

            if (def.Mode == CommandMode.Offline && S.Mode != "offline")
                throw new InvalidOperationException("command '" + cmd + "' needs the offline backend. " +
                    "In online mode use api.invoke, remoting.script, or remoting.action.");
            if (def.Mode == CommandMode.Online && S.Mode != "online")
                throw new InvalidOperationException("command '" + cmd + "' needs the online backend. " +
                    "Use remoting.connect first.");
            if (def.Mode == CommandMode.Offline)
                Offline.Init();

            return def.Handler(args);
        }

        internal object ListCommands()
        {
            var list = new List<object>();
            foreach (var d in _ordered)
                list.Add(new Dictionary<string, object>
                {
                    { "name", d.Name }, { "region", d.Region }, { "args", d.Args },
                    { "ret", d.Ret }, { "example", d.Example }, { "desc", d.Description },
                });
            return list;
        }

        internal object ListRegions()
        {
            var list = new List<object>();
            foreach (var group in _ordered.GroupBy(d => d.Region).OrderBy(g => g.Key))
            {
                var cmds = new List<string>();
                foreach (var d in group) cmds.Add(d.Name);
                list.Add(new Dictionary<string, object>
                {
                    { "region", group.Key }, { "commands", cmds }
                });
            }
            return list;
        }
    }
}
