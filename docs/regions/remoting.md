# Region: remoting

## `remoting.connect`

- args: `host= port= timeout=`
- returns: obj
- example: `remoting.connect host=localhost port=49152`
- connect to a running EPLAN GUI

## `remoting.ping`

- args: ``
- returns: obj
- example: `remoting.ping`
- ping the GUI

## `remoting.disconnect`

- args: ``
- returns: str
- example: `remoting.disconnect`
- disconnect

## `remoting.action`

- args: `action=`
- returns: obj
- example: `remoting.action action=XMsgShowAction`
- run an EPLAN action

## `remoting.script`

- args: `code= file= timeout=`
- returns: obj
- example: `remoting.script file=C:\tmp\s.cs`
- run a reflection C# script in the GUI

