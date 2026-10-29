# Region: function

## `function.place`

- args: `symbol= library= name= visible= x= y= variant= designations=[..]`
- returns: obj
- example: `function.place symbol=X tag=-X1 vis=L x=40 y=180 v=3`
- place a device on the page

## `function.remove`

- args: `name= handle=`
- returns: obj
- example: `function.remove name=-X1`
- remove a function

## `function.tag`

- args: `name= tag=`
- returns: obj
- example: `function.tag name=+ tag=-X1`
- assign a device tag

## `function.designation`

- args: `name= designations=[..]`
- returns: obj
- example: `function.designation name=-X1 designations=[L0,L1]`
- set connection designations

## `function.pins`

- args: ``
- returns: obj
- example: `function.pins`
- pin positions of every function

