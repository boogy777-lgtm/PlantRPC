using System.Collections.Generic;

namespace EplanRpc
{
    internal sealed class RemotingCommands : ICommandModule
    {
        public IEnumerable<CommandDef> Commands
        {
            get
            {
                yield return new CommandDef("remoting.connect", "remoting", "host= port= timeout=", "obj",
                    "remoting.connect host=localhost port=49152", "connect to a running EPLAN GUI",
                    CommandMode.Any, a => Online.Connect(a));
                yield return new CommandDef("remoting.ping", "remoting", "", "obj", "remoting.ping",
                    "ping the GUI", CommandMode.Any, a => Online.Ping());
                yield return new CommandDef("remoting.disconnect", "remoting", "", "str",
                    "remoting.disconnect", "disconnect", CommandMode.Any, a => Online.Disconnect());
                yield return new CommandDef("remoting.action", "remoting", "action=", "obj",
                    "remoting.action action=XMsgShowAction", "run an EPLAN action",
                    CommandMode.Any, a => Online.Action(a));
                yield return new CommandDef("remoting.script", "remoting", "code= file= timeout=", "obj",
                    "remoting.script file=C:\\tmp\\s.cs", "run a reflection C# script in the GUI",
                    CommandMode.Any, a => Online.Script(a));
            }
        }
    }
}
