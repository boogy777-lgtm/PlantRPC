# Recipes (copy a line, don't re-derive)

## offline: draw the 230V->24V PSU  (mode: offline)

    project.create link=<out>\psu.elk
    page.create name=1
    function.place symbol=X tag=-X1 x=40 y=180 v=3
    function.place symbol=X tag=-X2 x=40 y=160 v=3
    function.place symbol=X tag=-X3 x=40 y=140 v=3
    function.place symbol=F1 tag=-F1 x=100 y=180 v=1
    generate.connections
    export.image path=<out>\psu.png width=1600
    stats

page name is read back (may be /1, not 1). Electrical wiring between pins uses DynamicConnectionLine; the self-contained schematic.draw does this in one command.

## online: read the project open in the GUI  (mode: online)

    remoting.connect host=127.0.0.1 port=49152
    api.invoke @project.ProjectName
    api.invoke @project.ProjectLinkFilePath
    remoting.disconnect

port 49152 is the usual remoting port; discover with remoting.connect (omit port) or netstat.

## explore the 100% API surface  (mode: any)

    api.namespaces contains=DataModel
    api.types ns=Eplan.EplApi.DataModel contains=Function
    api.members type=Eplan.EplApi.DataModel.Function contains=Create
    api.desc Eplan.EplApi.DataModel.Function.Create

metadata needs no EPLAN runtime; api.invoke needs a live backend.

## draw a page from a JSON spec (self-contained, offline)  (mode: offline)

    project.create link=<out>\page.elk template=GOST_bas001
    schematic.draw spec=specs\do_feedback.json

One server command renders placements + real DynamicConnectionLine wires on actual pins + texts + generate.connections + PNG. No C# template, no Python, no remoting.script. Wire endpoints are {placement,pin} refs or [x,y]; pins resolve from SymbolVariant.ConnectionPoints (fallback: embedded symbol_pins.json). The result carries wireCheck: every line's stored Location must equal the pin coordinate it was asked to start at (locationOk), so "lines are connected" is verified, not asserted. A bare template name resolves in the installed EPLAN template folder: GOST_bas001 registers GOST_symbol, IEC_bas001 ships IEC_symbol. export.path is a DIRECTORY (Export.GraphicPage names the file after the page).

