using System;
using System.Collections.Generic;

namespace EplanRpc
{
    /// <summary>
    /// Self-contained drawing: a JSON spec rendered natively on the offline
    /// typed model. No external C# template, no remoting.script, no reflection —
    /// the same spec format that the client generator (eplan_schematic.py +
    /// draw_template.cs) used, but executed entirely inside the server.
    /// </summary>
    internal sealed class SchematicCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return new CommandDef(
                    "schematic.draw", "schematic", "spec=", "obj",
                    "schematic.draw spec=C:\\tmp\\do_feedback.json",
                    "draw a full page from a JSON spec (offline, no template/remoting)",
                    CommandMode.Offline,
                    a => Offline.WithLock(() => Offline.SchematicDraw(a)));
            }
        }
    }
}
