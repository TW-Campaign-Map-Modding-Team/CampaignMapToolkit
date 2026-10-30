# map_data.esf: open questions

What is still unknown about how the Assembly Kit's builders make map_data.esf, and how to
find out. Answers come from disassembling the builder DLLs
(`campaign_tools/exe_tools/exe_query.py --exe <dll>`), never from running reference builds.
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
  `MapDataAreas`. Masks are described in their own section below.
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

## Answered from the Warhammer game (2026-10-29)

Read from `Warhammer.exe` with `campaign_tools/exe_tools/exe_query.py --exe`.

- **The game loads one `MASKED_REGIONS_DATA` entry: the running campaign's mask.** The
  map_data loader (0x10d9160, called by 0xf725c0, which opens `/map_data.esf`) walks the
  entries and reads only one whose name equals the mask it is given, or the unnamed entry
  when it is given none (0x10da49c-0x10da55f). It parses that entry's `REGIONS_BLOCK` into the
  campaign's regions (`REGION_DATA` parser 0x10dd2e0) and applies its hex area runs to them
  (0x110d9c0). Every other entry is skipped unread.
- **The mask is the `campaigns` row's `mask`**, a key of `campaign_map_masks`. A new campaign
  takes it from its `campaigns_table` record (0x1402a00, looked up by campaign key in
  0xafde50, passed through 0xa99e40, 0xfd8a30 and 0xee6970). A loaded save stores the mask
  name and looks it up again in `campaign_map_masks` (0xfd6e4c-0xfd6ecc). In the Assembly
  Kit's `campaigns.xml`, `main_warhammer` has none, `wh_dlc03_beastmen` has `8` and
  `wh_dlc05_wood_elves` has `32`. The packs ship no `campaigns` table, only
  `campaign_map_masks` (`32`, `8`, `placeholder`).
- So a mask's entry reaches the game only in a campaign whose `campaigns.mask` names that
  mask. `campaign_map_masks_to_excluded_regions` is applied by the game and is not part of
  map_data.esf.

## Answered from Warhammer III's game and Assembly Kit (2026-10-29)

- **map.hex** v0x14 (every vanilla WH3 map) has the mask list and bitmaps. The prologue, chaos
  4 and combi 7 maps list only the unnamed mask.
- **The game** (`Warhammer3.exe`) reads every `MASKED_REGIONS_DATA` entry (0x1479c0c), then
  keeps the one named by the campaign's mask, or the unnamed one, when it builds the campaign
  map data (0x2928000, the same rule as Warhammer). It copies that entry's u16 triples, bounds
  and regions.
- **What a mask changes**, from the Assembly Kit's builder run on prologue with a mask painted in
  (a 10-column wall through every row, and rows 10-20's last 10 columns): masked hexes are
  impassable before the regions are split into areas, as in the older builders below. The masked
  entry's areas differ from the unmasked entry's (masked land joins the impassable areas beside
  it, and the areas and groups after it are renumbered), and so do its hex area runs, links and
  everything else worked out from the areas. The UI's region list repeats for each mask. Our
  writer's output matches the builder's node for node.
  - Reading the builder's code had suggested the opposite: `apply_mask` (empireutility
    0x32efb0, a set bit makes a hex type 7) is called through the import table only by the area
    link builder (tooldatabuilderdll 0x1a2ce0 / 0x1a2bc0). Where the masked types reach the area
    assignment was not found; the build settles it.
  - `generate_mask_data` (0x32f000) writes the entry's u16 array: one (row, first column, end
    column) triple per run of set bits in a row. The end is the first clear column, or, for a run
    reaching the end of the row, the row's byte count (not its width): a CA bug, which the build
    confirms.
- Since a masked entry's areas are numbered differently from the unmasked entry's, and
  hlp_data.esf and spd_data.esf refer to (region, area) indices and exist once per map folder,
  they can match only one mask's entry.

## Masks in the older builders (2026-10-29)

- map.hex v0x12 (Warhammer, Warhammer II, Troy, Pharaoh) stores masks; v0x13 (Three Kingdoms)
  and the older versions do not.
- Warhammer II's builder (tooldatabuilderdll, `REGIONS_DATA_FOR_PROCESSING::apply_mask`
  0x5d660, which calls empireutility's `apply_mask` with a mask index) applies the mask in its
  area flood fill (0x5c4e0: a hex joins an area only when its masked type is the area's type)
  and in its area links (0x59760). So **its masked entries split masked hexes off into
  impassable areas**, and their areas can be numbered differently from the unmasked entry's.
  The builder run on wh2_main_great_vortex_map_7 with the same kind of mask painted in matches
  our writer's output node for node. Its `generate_mask_data` is Warhammer III's. The other builders' wrappers
  are called from the same places:
  - Warhammer (0x1083e0): flood fill 0x11ae5e, links 0xfdeaf and 0xfe440, sea flag 0x113af0.
  - Warhammer II: also the sea flag (0x5c740, beside the first-area rule at 0x5c761).
  - Pharaoh (0x24ebb0, through the thunk 0x37597): flood fill 0x2698a7, links 0x24c3ea; its
    sea flag reads the (masked) areas' types instead.
  - Three Kingdoms (0x727e0): flood fill 0x717b6, links 0x6fb1f and 0x6fa10, sea flag 0x71940.
  Troy has no Assembly Kit and is taken to be Warhammer II.
- Three Kingdoms' map.hex loader (empireutility `HEX_MAP_DATA_FILE_DATA::load` 0x146c90)
  reads a mask list only for map.hex versions 0x0E, 0x10 and 0x12 (0x147764-0x14776f). For any
  other version, 0x13 included, it makes one unnamed mask with an empty bitmap (0x147865). So
  every Three Kingdoms map has exactly one mask, and its map_data.esf one unnamed entry with no
  runs; the builder's mask code never applies to a Three Kingdoms map.

Our map_data writers do the same for every game (`MapDataMask`): one model per mask, with its
masked hexes impassable. `MapHexFile` keeps the bitmaps (`CampaignMasks`), and the importer
rebuilds them from the entries' runs. Every vanilla map writes the same file as before.

## To investigate

### Our map_data writer
- Warhammer's and Pharaoh's handling of a painted mask is read from their builders only (see
  "Masks in the older builders"); only Warhammer II's and Warhammer III's builders were run on a
  masked map. No vanilla map paints a mask, so every vanilla file matches either way.

### The game, not the file
These do not change what the writers produce; they are what a modder painting masks would want
to know.
- [ ] What the game does with an entry's run triples (whether it closes those hexes in the
      campaign pathfinder), and whether a run's end column is exclusive.
- [ ] What the Warhammer game does when a campaign's mask names no entry in its
      map_data.esf. The loader then reads no regions at all; whether it fails or runs with
      an empty map was not traced.
