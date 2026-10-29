# Region: system

## `ping`

- args: ``
- returns: str
- example: `ping`
- health check

## `state`

- args: ``
- returns: obj
- example: `state`
- server state: mode, project, page, online endpoint

## `shutdown`

- args: ``
- returns: str
- example: `shutdown`
- stop the server

## `meta.commands`

- args: ``
- returns: obj
- example: `meta.commands`
- command dictionary (name/region/args/example)

## `meta.regions`

- args: ``
- returns: obj
- example: `meta.regions`
- region list

## `system.init`

- args: ``
- returns: str
- example: `system.init`
- offline: EplApplication.Init

## `system.version`

- args: ``
- returns: str
- example: `system.version`
- EPLAN version string

## `system.mode`

- args: ``
- returns: str
- example: `system.mode`
- current mode offline|online

## `stats`

- args: ``
- returns: obj
- example: `stats`
- function/connection counts on the current page

