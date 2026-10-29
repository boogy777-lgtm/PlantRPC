# EplanRpc commands (index)

Generated from the server command table. Grep here; the per-region
files under `regions/` carry the detail and are loaded on demand.

| command | args | returns | example |
|---|---|---|---|
| `ping` | `` | str | `ping` |
| `state` | `` | obj | `state` |
| `shutdown` | `` | str | `shutdown` |
| `meta.commands` | `` | obj | `meta.commands` |
| `meta.regions` | `` | obj | `meta.regions` |
| `system.init` | `` | str | `system.init` |
| `system.version` | `` | str | `system.version` |
| `system.mode` | `` | str | `system.mode` |
| `api.regions` | `` | obj | `api.regions` |
| `api.namespaces` | `contains=` | obj | `api.namespaces contains=DataModel` |
| `api.types` | `ns contains= limit=` | obj | `api.types ns=Eplan.EplApi.DataModel` |
| `api.members` | `type contains= limit=` | obj | `api.members type=Eplan.EplApi.DataModel.Function` |
| `api.desc` | `target` | obj | `api.desc Eplan.EplApi.DataModel.Function` |
| `api.invoke` | `target k=v...` | obj | `api.invoke DataModel.Function.Create page=@page variant=@variant:X:0` |
| `project.create` | `link= template=` | obj | `project.create link=C:\tmp\p.elk` |
| `project.open` | `link=` | obj | `project.open link=C:\tmp\p.elk` |
| `project.close` | `` | str | `project.close` |
| `project.pages` | `` | obj | `project.pages` |
| `project.current` | `` | obj | `project.current` |
| `page.create` | `name= type=` | obj | `page.create name=1 type=Circuit` |
| `page.list` | `` | obj | `page.list` |
| `page.use` | `name=` | obj | `page.use name=1` |
| `page.clear` | `` | obj | `page.clear` |
| `symbol.list` | `library= contains= limit=` | obj | `symbol.list library=IEC_symbol contains=X` |
| `symbol.check` | `library= name= variant=` | obj | `symbol.check name=X variant=3` |
| `function.place` | `symbol= library= name= visible= x= y= variant= designations=[..]` | obj | `function.place symbol=X tag=-X1 vis=L x=40 y=180 v=3` |
| `function.remove` | `name= handle=` | obj | `function.remove name=-X1` |
| `function.tag` | `name= tag=` | obj | `function.tag name=+ tag=-X1` |
| `function.designation` | `name= designations=[..]` | obj | `function.designation name=-X1 designations=[L0,L1]` |
| `function.pins` | `` | obj | `function.pins` |
| `stats` | `` | obj | `stats` |
| `connection.generate` | `` | str | `connection.generate` |
| `connection.read` | `` | obj | `connection.read` |
| `generate.connections` | `` | str | `generate.connections` |
| `graphics.line` | `x1= y1= x2= y2=` | str | `graphics.line x1=0 y1=0 x2=40 y2=0` |
| `graphics.rect` | `x1= y1= x2= y2=` | str | `graphics.rect x1=0 y1=0 x2=40 y2=40` |
| `graphics.text` | `x= y= text=` | str | `graphics.text x=10 y=10 text=hi` |
| `export.pdf` | `path= ` | obj | `export.pdf path=C:\tmp\p.pdf` |
| `export.image` | `path= width=` | obj | `export.image path=C:\tmp\p.png width=1600` |
| `settings.get` | `path= index=` | obj | `settings.get path=USER.TrDMProject.UserData.Longname` |
| `settings.set` | `path= value= index=` | str | `settings.set path=USER... value=x` |
| `schematic.draw` | `spec=` | obj | `schematic.draw spec=C:\tmp\do_feedback.json` |
| `remoting.connect` | `host= port= timeout=` | obj | `remoting.connect host=localhost port=49152` |
| `remoting.ping` | `` | obj | `remoting.ping` |
| `remoting.disconnect` | `` | str | `remoting.disconnect` |
| `remoting.action` | `action=` | obj | `remoting.action action=XMsgShowAction` |
| `remoting.script` | `code= file= timeout=` | obj | `remoting.script file=C:\tmp\s.cs` |
