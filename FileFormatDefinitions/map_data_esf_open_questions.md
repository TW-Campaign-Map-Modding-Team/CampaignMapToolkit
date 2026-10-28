# map_data.esf: open questions

What is still unknown about how the Assembly Kit's builders make map_data.esf, and how to
find out. Answers come from disassembling the builder DLLs (see `skills/tooldatabuilder-probe`
and `campaign_tools/exe_tools/exe_query.py --exe <dll>`), never from running reference builds.

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

## To investigate

### Playable areas
- [ ] Do the other games' builders also use only the first playable-area row? (Rome II,
      Attila, WH1, WH2, Three Kingdoms, Pharaoh, Troy: AKs need reinstalling.)
- [ ] Does any game read more than one theatre from map_data.esf, given that `theatres` and
      `THEATRES_AND_REGIONS_FOR_UI` are both lists? This needs the game exe, not the AK.
- [ ] Where do `overlay_file`, `sea_trade`, `minx` and `miny` reach map_data.esf in the
      older games, if anywhere? (Rome II's v2 CAMPAIGN_THEATRE colour table is a candidate for
      `overlay_file`.) The claim that they change the output came from probe runs, and does
      not hold for WH3.

### Per-game rules still inferred from output
- [ ] Each game's hex radius rounding (`MapDataRadiusRounding`), read from its DLL.
- [ ] Each game's region sea-flag rule (WH3: first non-impassable area's kind). Every other
      writer still uses "has a sea or bridge area" (`MapDataSeaFlag.AnySeaArea`).
- [ ] Which regions Three Kingdoms' builder makes settlements for (CAIME: land regions; WH3:
      every region whose sea flag is clear), and whether its invalid-settlement rules match
      WH3's, which CAIME now applies to both.
- [ ] Settlement rules for WH1, WH2, Troy and Rome II, which reuse Pharaoh's or Attila's.

### WH3 details not yet read
- [ ] The region a hex with no region falls back to: the loader types it as Sea in a region
      taken from a header count (empireutility 0x330e52). CAIME rejects such maps instead.
- [ ] The distance metric the logical-position sort uses (0x1b94b0); Three Kingdoms' is
      |dc| + |dr|.

### CAIME
- [ ] Keep map.hex mask bitmaps (CAIME discards them) and apply them per mask entry.
