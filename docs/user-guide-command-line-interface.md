# Using CAIME from the Command Line (CLI)

## Overview

Everything under the **Process** menu — Pathfinding, Map Data, Dynamic Resources, Trade Routes, Borders, and Lookup & Minimap images — can also be run from a terminal, without opening the editor window. The same is true of the **Validate** menu, which checks a layer for issues (Rivers, Town Slots, Roads, Bridges, Beaches, Regions, Attritions, Climates, Ground Types, Impassable, and Town Sprawl). This is the **command-line interface (CLI)**.

The CLI is useful when you want to:

- **Batch-process** several maps from a script or build pipeline.
- **Re-export** a map quickly without clicking through menus.
- **Automate** processing as part of a larger mod-building workflow.

When you launch CAIME with arguments, it runs the requested processing in a console, prints its progress, and exits — **no editor window ever appears.** Launching CAIME with no arguments opens the normal graphical editor exactly as before.

> **The CLI reuses the exact same processing code as the menu.** A map processed from the command line produces the same output files, in the same locations, as if you had clicked the matching items in the **Process** menu.

---

## Table of Contents

- [Before You Start](#before-you-start)
- [Command Syntax](#command-syntax)
- [Getting Help](#getting-help)
- [Processing a Map](#processing-a-map)
  - [The `--map` Option](#the---map-option)
  - [Choosing Which Tasks to Run](#choosing-which-tasks-to-run)
  - [The Task List](#the-task-list)
- [Validating a Map](#validating-a-map)
  - [The Layer List](#the-layer-list)
- [Worked Examples](#worked-examples)
- [Exit Codes](#exit-codes)
- [Troubleshooting](#troubleshooting)

---

## Before You Start

The CLI relies on settings you configure **once** in the graphical editor. Make sure both of the following are true before running it:

1. **The RPFM path must be set.**
   The CLI reads the project's database through RPFM, exactly as the editor does, and waits for it before running anything. These tasks need it, as their menu items do in the editor: `--map-data`, `--pathfinding` and `--lookup`, and the `--regions`, `--attritions`, `--climates` and `--ground-types` validators. If the database can't be read, a run that asks for any of them stops before running anything, and names them. See [Settings & Preferences](user-guide-settings-and-preferences.md#3-database-rpfm-and-output-folder).

2. **For `process`, an output folder must be set.**
   The CLI writes where the editor does: the **Output folder** saved under **Settings > Preferences**. If none is configured, processing stops with a clear error.

> **Assembly Kit (legacy):** with no Output folder set, the CLI writes to the game's Assembly Kit `working_data` instead, and with **Assembly Kit Integration** enabled, `--map-data` and `--dynamic-resources` run the Assembly Kit's own builder, as in the editor. See [Annex: The Assembly Kit](user-guide-assembly-kit.md).

---

## Command Syntax

```
CAIME.exe process --map <path-to-.hex> (--all | <task> [<task> ...])
CAIME.exe validate --map <path-to-.hex> (--all | <layer> [<layer> ...])
CAIME.exe --help
```

There are three commands:

| Command | What it does |
|---|---|
| `process` | Loads a `.hex` map and runs one or more processing tasks. |
| `validate` | Loads a `.hex` map and validates one or more layers. |
| `help`, `--help`, `-h` | Prints usage information and exits. |

> **Tip:** Run from a terminal such as **PowerShell** or **Command Prompt**, from the folder where `CAIME.exe` lives (or use the full path to the executable). Because CAIME is a windowed application, the CLI attaches to your terminal so you can read its output; control returns to your prompt automatically when it finishes.

---

## Getting Help

To see the full list of commands, options, and tasks at any time:

```
CAIME.exe --help
```

This prints a summary like the following:

```
Campaign Map Toolkit (CAIME) 1.0.0 - command line interface

USAGE:
  CAIME.exe process --map <path-to-.hex> (--all | <task> [<task> ...])
  CAIME.exe validate --map <path-to-.hex> (--all | <layer> [<layer> ...])
  CAIME.exe --help

COMMANDS:
  process              Process a campaign map (no window is shown).
  validate             Validate one or more campaign map layers.
  help, --help, -h     Show this help and exit.

OPTIONS:
  --map, -m <path>     Path to the project's map .hex file. Required.
  --all                Run every task below, in a sensible order.

PROCESS TASKS (mirror the GUI 'Process' menu):
  --map-data           Process map_data.esf
  --dynamic-resources  Process dynamic resources (.esf)
  --pathfinding        Generate processed pathfinding data (.ppd)
  --borders            Generate processed borders data (.pbd)
  --trade-routes       Generate processed trade routes data (.ptd)
  --lookup             Generate lookup and minimap images

VALIDATE LAYERS (mirror the GUI 'Validate' menu):
  --rivers             Validate the Rivers layer
  --town-slots         Validate the Town Slots layer
  --roads              Validate the Roads layer
  --bridges            Validate the Bridges layer
  --beaches            Validate the Beaches layer
  --regions            Validate the Regions layer
  --attritions         Validate the Attritions layer
  --climates           Validate the Climates layer
  --ground-types       Validate the Ground Types layer
  --impassable         Validate the Impassable layer
  --town-sprawl        Validate the Town Sprawl layer
```

---

## Processing a Map

The `process` command always needs two things: **which map** to process (`--map`) and **which tasks** to run (either `--all`, or one or more individual task flags).

### The `--map` Option

Point `--map` (or its short form `-m`) at the project's `.hex` file:

```
CAIME.exe process --map "C:\maps\my_map\map.hex" --all
```

The path is validated before any work begins. CAIME checks that:

- a path was actually provided after `--map`,
- the file ends in `.hex`, and
- the file exists on disk.

If any check fails, processing stops immediately with an error and **nothing is run**. Quote the path if it contains spaces.

### Choosing Which Tasks to Run

You have two ways to choose tasks:

- **`--all`** runs every task in a sensible order (Map Data → Dynamic Resources → Pathfinding → Borders → Trade Routes → Lookup).
- **One or more individual task flags** run just those tasks. When you list several, they always run in the same canonical order regardless of the order you type them, so results are predictable.

```
CAIME.exe process -m map.hex --pathfinding --trade-routes
```

> **`--all` cannot be combined with individual task flags.** Pick one approach or the other. Doing both is rejected as an invalid command.

If you specify no tasks at all, the command is rejected — you must ask for at least one piece of work.

### The Task List

Each task corresponds exactly to one entry in the **Process** menu. See [Processing & Exporting Your Campaign Map](user-guide-processing-and-exporting.md) for a full description of what each one reads and produces.

| CLI flag | Process menu item | Output file |
|---|---|---|
| `--map-data` | Map Data | `map_data.esf` |
| `--dynamic-resources` | Dynamic resources | `dynamic_resources.esf` |
| `--pathfinding` | Pathfinding data | `pathfinding.bin` / `.ppd` |
| `--borders` | Borders data | `borders.pbd` |
| `--trade-routes` | Trade Routes | `trade_routes.ptd` |
| `--lookup` | Lookup and Minimap images | `lookup_*` / `minimap_*` images |

All outputs are written to the same place as the editor:
```
{output folder}\campaign_maps\{your map name}\
```
or, with no output folder set, `{Assembly Kit path}\working_data\campaign_maps\{your map name}\` (see [Annex: The Assembly Kit](user-guide-assembly-kit.md#3-output-fallback-working_data)).

---

## Validating a Map

The `validate` command checks a `.hex` map for layer-specific issues without writing any output files. It takes the same `--map`/`-m` option and the same `--all` / individual-flag choice as `process`, but needs no output folder. The `--regions`, `--attritions`, `--climates` and `--ground-types` validators check the map against its database, read through RPFM; the rest only read the map itself.

```
CAIME.exe validate --map "C:\maps\my_map\map.hex" --all
CAIME.exe validate -m map.hex --roads --rivers
```

Results are printed to the console as each layer is checked; a layer that fails validation is reported as `FAILED` and the process exits with code `2`. See the log output for the specific issues found.

### The Layer List

Each layer flag corresponds exactly to one entry in the GUI's **Validate** menu.

| CLI flag | Validate menu item |
|---|---|
| `--rivers` | Rivers |
| `--town-slots` | Town slots |
| `--roads` | Roads |
| `--bridges` | Bridges |
| `--beaches` | Beaches |
| `--regions` | Regions |
| `--attritions` | Attritions |
| `--climates` | Climates |
| `--ground-types` | Ground types |
| `--impassable` | Impassable |
| `--town-sprawl` | Town sprawl |

---

## Worked Examples

**Process everything for a map:**
```
CAIME.exe process --map "C:\maps\my_map\map.hex" --all
```

**Re-export just pathfinding and trade routes:**
```
CAIME.exe process -m "C:\maps\my_map\map.hex" --pathfinding --trade-routes
```

**Generate only the lookup and minimap images:**
```
CAIME.exe process -m "C:\maps\my_map\map.hex" --lookup
```

**Validate every layer that has a validator:**
```
CAIME.exe validate --map "C:\maps\my_map\map.hex" --all
```

**Validate just the roads and rivers layers:**
```
CAIME.exe validate -m "C:\maps\my_map\map.hex" --roads --rivers
```

**Process several maps in a row (PowerShell):**
```powershell
$maps = "C:\maps\map_a\map.hex", "C:\maps\map_b\map.hex"
foreach ($m in $maps) {
    CAIME.exe process --map $m --all
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $m"; break }
}
```

---

## Exit Codes

The CLI sets a process exit code so scripts can detect success or failure:

| Code | Meaning |
|---|---|
| `0` | Success — all requested tasks completed. |
| `1` | Invalid arguments — the command was rejected before any processing ran. |
| `2` | Processing failure — the map could not be loaded, the database a requested task needs could not be read, or one or more tasks failed. |

In PowerShell the code is available as `$LASTEXITCODE`; in Command Prompt as `%ERRORLEVEL%`.

---

## Troubleshooting

**"No output folder, and no assembly kit path for *\<game\>*, is configured."**
The CLI writes to the output folder you saved in the editor. Open CAIME normally, go to **Settings > Preferences**, set an **Output folder**, and click **Save**. Then re-run the CLI. See [Settings & Preferences](user-guide-settings-and-preferences.md).

**"The map's database was not read through RPFM, and these tasks need it: ..."**
The tasks named need the project's database, and it couldn't be read; the messages above it say why. Check the **RPFM path** in **Settings > Preferences**, fix what the messages report, and re-run the CLI. Tasks that don't need the database still run on their own.

**"Map file not found" / "The map file must be a .hex file."**
Check the path after `--map`. It must point to an existing file ending in `.hex`. Wrap paths containing spaces in double quotes.

**"Unknown command" / "Unknown option."**
The CLI validates every argument and refuses to run anything it does not recognise — this prevents a typo from silently doing the wrong thing. Run `CAIME.exe --help` to see the exact, supported spelling of every command, option, and task.

**`--map-data` or `--dynamic-resources` reports a failure.**
These two tasks have the same requirements as in the editor: the project must have no unsaved changes, and `--map-data` needs your map's row in `campaign_map_playable_areas`. See [Processing & Exporting](user-guide-processing-and-exporting.md) for details.

**The editor window opened instead of running the CLI.**
The graphical editor only opens when CAIME is launched **with no arguments**. If a window appeared, the arguments did not reach the program — double-check that you typed a command (`process` or `--help`) after the executable name.
