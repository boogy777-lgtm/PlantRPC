using System;
using System.Collections.Generic;

namespace EplanRpc
{
    /// <summary>
    /// Compatibility facade for callers that used the old static Cmd surface.
    /// Dispatch and metadata now live in CommandRegistry, with one module per
    /// thin domain layer.
    /// </summary>
    internal static class Cmd
    {
        private static readonly CommandRegistry Registry = Build();

        private static CommandRegistry Build()
        {
            var registry = new CommandRegistry();
            registry.Add(new SystemCommands());
            registry.Add(new ApiCommands());
            registry.Add(new ProjectCommands());
            registry.Add(new PageCommands());
            registry.Add(new SymbolCommands());
            registry.Add(new FunctionCommands());
            registry.Add(new ConnectionCommands());
            registry.Add(new GraphicsCommands());
            registry.Add(new ExportCommands());
            registry.Add(new SettingsCommands());
            registry.Add(new SchematicCommands());
            registry.Add(new RemotingCommands());
            return registry;
        }

        internal static object Dispatch(string cmd, Dictionary<string, object> args)
        {
            return Registry.Dispatch(cmd, args);
        }

        internal static object ListCommands()
        {
            return Registry.ListCommands();
        }

        internal static object ListRegions()
        {
            return Registry.ListRegions();
        }
    }
}
