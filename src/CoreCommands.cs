using System.Collections.Generic;

namespace EplanRpc
{
    internal sealed class ProjectCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("project.create", "project", "link= template=", "obj",
                    "project.create link=C:\\tmp\\p.elk", "create a project",
                    a => Offline.WithLock(() => Offline.ProjectCreate(a)));
                yield return Cmd("project.open", "project", "link=", "obj",
                    "project.open link=C:\\tmp\\p.elk", "open a project",
                    a => Offline.WithLock(() => Offline.ProjectOpen(a)));
                yield return Cmd("project.close", "project", "", "str", "project.close",
                    "close the project", a => Offline.WithLock(() => Offline.ProjectClose()));
                yield return Cmd("project.pages", "project", "", "obj", "project.pages",
                    "list pages", a => Offline.WithLock(() => Offline.ProjectPages()));
                yield return Cmd("project.current", "project", "", "obj", "project.current",
                    "current project path",
                    a => S.Project == null ? null : S.Project.ProjectLinkFilePath);
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class PageCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("page.create", "page", "name= type=", "obj",
                    "page.create name=1 type=Circuit", "create/select a page",
                    a => Offline.WithLock(() => Offline.PageCreate(a)));
                yield return Cmd("page.list", "page", "", "obj", "page.list",
                    "list pages (alias project.pages)",
                    a => Offline.WithLock(() => Offline.ProjectPages()));
                yield return Cmd("page.use", "page", "name=", "obj", "page.use name=1",
                    "select a page", a => Offline.PageUse(a));
                yield return Cmd("page.clear", "page", "", "obj", "page.clear",
                    "remove all functions on the page",
                    a => Offline.WithLock(() => Offline.PageClear()));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class SymbolCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("symbol.list", "symbol", "library= contains= limit=", "obj",
                    "symbol.list library=IEC_symbol contains=X", "list symbols in a library",
                    a => Offline.SymbolList(a));
                yield return Cmd("symbol.check", "symbol", "library= name= variant=", "obj",
                    "symbol.check name=X variant=3", "inspect a symbol variant",
                    a => Offline.WithLock(() => Offline.SymbolCheck(a)));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class FunctionCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("function.place", "function",
                    "symbol= library= name= visible= x= y= variant= designations=[..]", "obj",
                    "function.place symbol=X tag=-X1 vis=L x=40 y=180 v=3",
                    "place a device on the page",
                    a => Offline.WithLock(() => Offline.FunctionPlace(a)));
                yield return Cmd("function.remove", "function", "name= handle=", "obj",
                    "function.remove name=-X1", "remove a function",
                    a => Offline.WithLock(() => Offline.FunctionRemove(a)));
                yield return Cmd("function.tag", "function", "name= tag=", "obj",
                    "function.tag name=+ tag=-X1", "assign a device tag",
                    a => Offline.WithLock(() => Offline.FunctionTag(a)));
                yield return Cmd("function.designation", "function", "name= designations=[..]", "obj",
                    "function.designation name=-X1 designations=[L0,L1]",
                    "set connection designations",
                    a => Offline.WithLock(() => Offline.FunctionDesignation(a)));
                yield return Cmd("function.pins", "function", "", "obj", "function.pins",
                    "pin positions of every function",
                    a => Offline.WithLock(() => Offline.FunctionPins()));
                yield return Cmd("stats", "system", "", "obj", "stats",
                    "function/connection counts on the current page",
                    a => Offline.WithLock(() => Offline.Stats()));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class ConnectionCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("connection.generate", "connection", "", "str",
                    "connection.generate", "Generate.Connections",
                    a => Offline.WithLock(() => Offline.GenerateConnections()));
                yield return Cmd("connection.read", "connection", "", "obj",
                    "connection.read", "read connections of the page",
                    a => Offline.WithLock(() => Offline.ConnectionRead()));
                yield return Cmd("generate.connections", "generate", "", "str",
                    "generate.connections", "alias of connection.generate",
                    a => Offline.WithLock(() => Offline.GenerateConnections()));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class GraphicsCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("graphics.line", "graphics", "x1= y1= x2= y2=", "str",
                    "graphics.line x1=0 y1=0 x2=40 y2=0", "draw a graphic line (NOT electrical)",
                    a => Offline.WithLock(() => Offline.LineDraw(a)));
                yield return Cmd("graphics.rect", "graphics", "x1= y1= x2= y2=", "str",
                    "graphics.rect x1=0 y1=0 x2=40 y2=40", "draw a rectangle",
                    a => Offline.WithLock(() => Offline.RectDraw(a)));
                yield return Cmd("graphics.text", "graphics", "x= y= text=", "str",
                    "graphics.text x=10 y=10 text=hi", "draw text",
                    a => Offline.WithLock(() => Offline.TextDraw(a)));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class ExportCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("export.pdf", "export", "path= ", "obj",
                    "export.pdf path=C:\\tmp\\p.pdf", "export page as PDF",
                    a => Offline.WithLock(() => Offline.ExportPdf(a)));
                yield return Cmd("export.image", "export", "path= width=", "obj",
                    "export.image path=C:\\tmp\\p.png width=1600", "export page as PNG",
                    a => Offline.WithLock(() => Offline.ExportImage(a)));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }

    internal sealed class SettingsCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return Cmd("settings.get", "settings", "path= index=", "obj",
                    "settings.get path=USER.TrDMProject.UserData.Longname", "read a setting",
                    a => Offline.SettingsGet(a));
                yield return Cmd("settings.set", "settings", "path= value= index=", "str",
                    "settings.set path=USER... value=x", "write a setting",
                    a => Offline.SettingsSet(a));
            }
        }

        private static CommandDef Cmd(string name, string region, string args, string ret,
                                      string example, string desc,
                                      System.Func<Dictionary<string, object>, object> handler)
        {
            return new CommandDef(name, region, args, ret, example, desc,
                                  CommandMode.Offline, handler);
        }
    }
}
