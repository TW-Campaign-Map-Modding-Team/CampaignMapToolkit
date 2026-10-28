# map_data.esf: open questions

What is still unknown about how the Assembly Kit's builders make map_data.esf, and how to
find out. Answers come from disassembling the builder DLLs (see `skills/tooldatabuilder-probe`
and `campaign_tools/exe_tools/exe_query.py --exe <dll>`), never from running reference builds.
`exe_query.py` only reads x64 DLLs; Rome II's and Attila's are 32-bit and need a capstone
`CS_MODE_32` reader that finds string references as absolute addresses (image base + RVA).

## Answered from Warhammer III's Assembly Kit (2026-10-28)

The WH3 builder is `tooldatabuilderdll.modder.x64.dll`; it fills empireutility's
`CAMPAIGN_MAP_FILE_DATA` and calls its `save`, which writes MAP_DATA v2.

- **Playable areas.** The builder reads every `campaign_map_playable_areas` row for the map
  (`overlay_file`, `maxx`, `maxy`, `sea_trade`, `index`; never `minx` or `miny`), but uses only
  the first: its `{0, 0, maxx, maxy}` bounds load the hex map, and its `index` names the one
  theatre. `overlay_file` and `sea_trade` are read and never reach the file.
- **Settlements** follow the Three Kingdoms rules (sprawl fill, 7- or 19-hex hexagon, centre,
  nearest-land logical position), not a radius-2 hexagon search. An invalid settlement is still
  written, with `INVALID` (0xFFFF, 0xFFFF) positions, and logged. The UI position is the
  logical position clamped to the grid, so an invalid settlement's is the last column and row.
- **A region's sea flag** is the kind of its first non-impassable area in row-major order.
  Settlements are only built for regions whose flag is 0.
- **Hex types** and the bridge-cliff rule (the east neighbour is never checked) match
  `MapDataAreas`. A mask bitmap's set bit makes that hex Impassable for that mask's entry.
- **The logical-position sort** (tooldatabuilder 0x1a88e0, calling `std::sort` at 0x1b94b0)
  orders the main slot hexes followed by the port slot hexes by |dc| + |dr| from the centre,
  then takes the first plain land hex. The list holds 7 or 19 entries, under `std::sort`'s
  32-element threshold, so only its insertion sort runs and ties keep list order: the same
  result as `MapDataSettlement`'s stable `OrderBy`.
- **A hex with region field 0** (empireutility 0x330e4e) is typed Sea and given region
  `count - 1`, where `count` is the u32 before map.hex's region name list: the last region
  that map.hex names.

## Answered from every installed Assembly Kit (2026-10-28)

Read from the Rome II, Attila, Warhammer, Warhammer II, Three Kingdoms, Pharaoh and Warhammer III
Assembly Kits. Troy and Thrones have none installed; Troy is taken to build exactly as
Warhammer II and Thrones as Attila.

### Playable areas and theatres
- **Every builder makes one theatre from the first playable-area row.** WH1, WH2, 3K and
  WH3's `create_map_data` push a single theatre named after row 0's `index`, bounded by the
  loaded hex map's bounds, and pass row 0's `overlay_file` alongside. Rome II's, Attila's and
  Pharaoh's also assert `map_name_and_bounds.m_playable_area_data.size() == 1` ("Only
  supporting single theatre maps, but multiple playable areas found for map..."), which logs
  and carries on with row 0. No builder writes a second theatre, and no game is taken to read
  one.
- **`THEATRES_AND_REGIONS_FOR_UI`** (WH2 0x5bbe0) loops over the theatres and lists in each
  one's `REGION_KEYS` the regions whose settlement's world position lies inside the theatre's
  bounds. With one theatre covering the whole map, that is every region with a settlement.
- **`overlay_file` reaches no file in any installed builder.** WH2 builds the path
  `theatres/<overlay_file>` and passes it to the `CAMPAIGN_THEATRE` writer (0x5b9b0), which
  never reads it; Rome II's (0x202be0) and Attila's (0xed3a0) writers also ignore that argument.
  All of them write `CAMPAIGN_THEATRE` version 3 with no colour table, so the version 2 record
  and 256-entry colour table in the shipped Rome II file come from an older builder.
- **`sea_trade`** is read into the playable-area record by every builder and dropped when the
  theatre is made from it.
- **`minx` and `miny`** are never read by the x64 builders (WH1, WH2, 3K, WH3, Pharaoh), which
  use `{0, 0, maxx, maxy}`. Rome II and Attila read them:
  - Rome II (loader 0x1ff1c0) copies `{minx, miny, maxx, maxy}` as given into the hex map's
    bounds, which become the theatre's bounds, so all four reach the file, and the hex
    radius is worked out from `maxx - minx`.
  - Attila (geometry 0xe8fe0) uses `(minx, miny)` as the hex map's origin and `maxx - minx`
    as its width; its top edge is worked out from the row count, so `maxy` is never used.
    Every hex centre is offset by the origin: x = `minx + column * column spacing`, y =
    `miny + row * row spacing`, plus half a row on odd columns (0xe9330, 0xe93b0 and
    `create_map_data` at 0xef7a9).
  Vanilla rows have `minx = miny = 0`, which is what our map_data writer assumes.

### Hex radius rounding
- **WH1, WH2, 3K, WH3 and Pharaoh**: `((2 / 3) / (columns - 1)) * (maxx - minx)`, in both
  empireutility's `HEX_MAP_DATA_FILE_DATA::load` and tooldatabuilder's own copy
  (`TwoThirdsPerColumn`, with minx 0). Half a row is the radius times 0.8660254, and a row is
  twice that.
- **Rome II and Attila**: `(maxx - minx) / ((columns - 1) * 1.5)` (`PerColumnAndAHalf`).
- **WH1's installed Assembly Kit disagrees with the WH1 game's files.** Its DLLs use
  `TwoThirdsPerColumn`, but the shipped `warhammer_map_1_1_wood_elves` map_data.esf only
  matches `PerColumnAndAHalf`, so it was built by an older builder. `Wh1MapDataWriter`
  follows the shipped files.

### Region sea flag
- **Every builder uses the first-area rule.** Each area's kind goes through the same table
  (Bridge and Sea set the flag to 1, Impassable leaves it unset, every other kind sets 0),
  and only a region whose flag is still unset (3) takes the value; any left unset afterwards
  become 0. Found in Rome II (0x203cb2, byte table `0 0 0 1 1 0 0 2 0`), Attila (0xee748, same
  table), WH1 (0x113b11), WH2 (0x5c761), 3K (0x71961), Pharaoh (0x25c169, which also asserts
  when a later area disagrees) and WH3 (0x1a594b). `MapDataModel` applies it for every game.

### Settlements
- **Three Kingdoms** builds settlements only for regions whose sea flag is 0, the same gate
  as WH3 (0x70abc). Its validator (0x74280) makes the same checks in the same order as WH3's
  (0x1a8440): no main hexes, a dock without a port, main and port hexes not 7 or 19, no centre
  or a centre that is not a main hex, no logical position, a port that is not 3 hexes. An
  invalid settlement is still written, with INVALID positions, and logged.
- **WH1 and WH2** share one settlement builder: their functions (WH1 0x10afa0, WH2 0x5aa20)
  are identical instruction for instruction. It is Pharaoh's "warhammer system" (the same
  `SLOT_ARRAY_BLOCK`, `SLOT_POSITIONS_BLOCK`, `PORT_POSITIONS_BLOCK` records and the same three
  log messages). It checks the warhammer rules first and then the old Rome II rules, and logs
  a settlement that only passes the second as "only valid for the old rome2 system".
  Pharaoh's DLL is a debug-style build, so its code was not compared instruction by
  instruction.
- **Rome II** uses Attila's slot-position rules (the same validation and log messages:
  `validate_land_slot_positions`, `validate_port_slot_positions`,
  `find_all_slot_connections_and_unlock_paths`). Unlike Attila it also reads
  `campaign_map_slots` (`slot_id`, `slot_type`).

## To investigate

### Our map_data writer
- [ ] RPFM's TSV export prints floats with four decimals, so a `maxx` stored with more
      precision is read a unit in the last place off (the shipped Pharaoh file implies
      756.83997, which prints as 756.8400). `table.rows` returns floats as JSON numbers.
- [ ] Apply map.hex mask bitmaps per mask entry. CAIME's map.hex reader (`MapHexFile`) discards them, so the writer never sees them.
