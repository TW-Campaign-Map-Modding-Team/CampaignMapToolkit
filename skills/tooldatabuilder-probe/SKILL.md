---
name: tooldatabuilder-probe
description: Find out which files and database table columns a Total War Assembly Kit data builder (the ToolDataBuilder DLL MapDataBuilder drives) reads to build map_data.esf or dynamic_resources.esf, and which of them actually change the output - by tracing its file opens and by rebuilding with each table, row and column taken away or changed. Use it when a game or an Assembly Kit update changes what MapDataBuilder needs, to check the tables CAIME writes for the RPFM database source (MapDataBuilderTables), or to see how a game's CA::String is laid out.
---

# ToolDataBuilder probe

MapDataBuilder (`MapDataBuilder/main.cpp`) loads a game's Assembly Kit data-builder DLL and calls
its `do_campaign_maps_regions_process` (map_data.esf) or `do_dynamic_resources_process`
(dynamic_resources.esf). `probe.cpp` makes the same calls, logging every file the DLL opens by
hooking ntdll, and `tooldatabuilder.py` drives it. Every command only reads from the Assembly Kit:
output goes to `--work`, by default `%TEMP%\tooldatabuilder-probe`.

Requires Python 3 and Visual Studio with the C++ x86/x64 build tools.

```
python skills/tooldatabuilder-probe/tooldatabuilder.py build
python skills/tooldatabuilder-probe/tooldatabuilder.py trace          --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map
python skills/tooldatabuilder-probe/tooldatabuilder.py trace-dyn-res  --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map
python skills/tooldatabuilder-probe/tooldatabuilder.py ablate         --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map [--full]
python skills/tooldatabuilder-probe/tooldatabuilder.py string-layout  --game phar --ak "<assembly_kit>"
```

- `--game` takes MapDataBuilder's game names: `rome2`, `attila`, `thrones`, `warhammer`,
  `warhammer2`, `warhammer3`, `three_kingdoms`, `troy`, `phar`.
- `--hex` supplies a `map.hex` for a map the Assembly Kit does not ship - Rome II's and Attila's ship
  none; `Templates/<map>/map.hex` has one per game.
- `trace` lists every file the export touches, flags writes outside the work folder, and runs it
  twice to show which output bytes vary between identical runs.
- `ablate` reruns the export with each table missing or empty and, for the tables that have a row
  for the map, each column dropped, then with bare tables holding only the map's rows (and the
  columns that mattered) or one placeholder row, then with each bound set to 0 and -1. `--full` also
  reorders rows, shifts each column's values by a row, and drops every column of every table. Each
  run takes from a second to half a minute, depending on the map's size.

## What it found (2026-10)

Rome II, Attila, Warhammer 1-3, Three Kingdoms and Pharaoh Dynasties tested; Thrones is taken to
match Attila and Troy Warhammer 2 or Pharaoh.

- map_data reads `campaign_maps`, `campaign_map_playable_areas`, `regions`,
  `campaign_map_settlements` and `campaign_ground_types` from the db path it is given (Rome II also
  `campaign_map_slots`, which may be empty), plus the map's `map.hex`. Never the TWaD schemas.
- What changes the output: the map's `campaign_map_playable_areas` row's `index`, `overlay_file`,
  `sea_trade` and `maxx` (and `minx`/`miny` on Rome II and Attila). Rome II stores its `maxy` without
  using it; every other bound only has to be present. `regions`, settlements and ground types need one
  row of any content. Bare one-row tables give byte-identical output in every game tested, which is
  what `CAIME/Classes/Exporters/MapData/MapDataBuilderTables.cs` writes.
- A missing table crashes the DLL or fails it; an empty one crashes it; no `campaign_maps` row for
  the map makes it report success yet write nothing.
- dyn_res reads the same five tables from `raw_data/EmpireDesignData`, not `raw_data/db`.

## Gotchas

- Bytes 8-11 of an ESF file are the Unix time it was written: ignore all four when comparing outputs,
  not only the ones that differed in one pair of runs.
- `CA::String` is not laid out the same everywhere: Pharaoh keeps a string of up to 11 characters
  inline, so a hand-built `{size, capacity, pointer}` struct turns a short map name such as
  `phar_main` into garbage and the export silently processes no map. Build arguments with the game's
  own exported constructor (`??0String@CA@@QEAA@PEBD@Z`, x86 `??0String@CA@@QAE@PBD@Z`) from its
  CALibs DLL, as both the probe and MapDataBuilder do.
- Rome II, Attila and Warhammer 1 start a TCPConsole helper from the binaries folder when the DLL
  loads, refuse to load without it, and give it a console window. The probe therefore runs on a
  private desktop and clears `NoDefaultCurrentDirectoryInExePath`, which stops Windows looking in
  the current directory.
- The 32-bit DLLs (Rome II, Attila) look a path up under several virtual-filesystem roots before
  opening it directly, so their traces hold many failed opens of nonsense paths; `trace` hides them.
- Pharaoh's DLL treats the map name `all` as every map.
