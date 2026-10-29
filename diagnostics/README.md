# Diagnostics

PowerShell drivers used to reproduce and measure. All of them start their own `EplanRpc.exe`
instance, talk line-JSON over TCP, and shut it down; they never touch the user's open project
except `run_online_script.ps1`, which targets the GUI by explicit script file.

| file | what it does |
|---|---|
| `repro_gost.ps1` | offline: create project from `GOST_bas001`, `schematic.draw spec=do_fb_gost.json`, print `wireCheck` numbers and `connection.read`, export PNG. |
| `run_online_script.ps1` | online: start `--mode online`, `remoting.connect 127.0.0.1:49152`, `remoting.script file=<.cs>`, print the result JSON. |
| `check_symbols.ps1` | offline: `symbol.check` for `X K O S Y1` (connection-point count per variant). |
| `desc.ps1` | offline: `api.desc` for selected types (used to discover `PinBase.Location` etc.). |
| `crop2.ps1` | crop + nearest-neighbour upscale of an exported PNG, in mm coordinates (A3, 420x297). |
| `crop_mm.ps1` / `crop.ps1` / `scan*.ps1` | earlier pixel probes (superseded by `crop2.ps1`). |

## Data files

| file | meaning |
|---|---|
| `do_fb_gost.json` | the ГОСТ variant of the DO-FB spec (template `GOST_bas001`), export to `gost_out\`. |
| `dcl_test_spec.json` | minimal two-pin + one-wire spec used to prove `DynamicConnectionLine` semantics. |
| `=DO+FB_1.png` | offline render of the ГОСТ spec (1800x1273). |
| `dcl_out.png` | render of the minimal spec. |
| `draw_result*.txt` | recorded RPC transcripts (online draw attempts). |
| `pin_offsets.txt`, `symbols_dump.txt` | earlier symbol/pin dumps. |

## Coordinate mapping for any exported page

```
px_per_mm = width_px / 420          # A3 landscape
x_px = mm_x * px_per_mm
y_px = (297 - mm_y) * px_per_mm
```

Example: `mm (90,170)` → `px (386, 544)` in the 1800 px render.

## Note

`scan*.ps1` were written before the color map was known. The render uses
`0,0,255` for symbol graphics and `102,0,255` for connection lines; background is white,
`144,219,255` appears as symbol fill. Prefer measuring *numbers* from the API over pixel guessing.
