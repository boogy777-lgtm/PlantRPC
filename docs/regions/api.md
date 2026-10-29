# Region: api

## `api.regions`

- args: ``
- returns: obj
- example: `api.regions`
- region -> namespace map

## `api.namespaces`

- args: `contains=`
- returns: obj
- example: `api.namespaces contains=DataModel`
- list namespaces

## `api.types`

- args: `ns contains= limit=`
- returns: obj
- example: `api.types ns=Eplan.EplApi.DataModel`
- types in a namespace

## `api.members`

- args: `type contains= limit=`
- returns: obj
- example: `api.members type=Eplan.EplApi.DataModel.Function`
- members of a type

## `api.desc`

- args: `target`
- returns: obj
- example: `api.desc Eplan.EplApi.DataModel.Function`
- description/signatures of a type or member

## `api.invoke`

- args: `target k=v...`
- returns: obj
- example: `api.invoke DataModel.Function.Create page=@page variant=@variant:X:0`
- generic reflective call (static/ctor/instance)

