# dynamic_resources.esf

The farmland tiles that appear on the campaign map as a region's buildings level up. The
Assembly Kit builds it from the map's `map.hex`, `dynamic_resources.png` and
`dynamic_resources_database.xml`; CAIME's writer is
`CAIME/Classes/Exporters/DynamicResources/`. It reproduces Rome II's and Attila's main maps byte
for byte, and the Troy, Warhammer and Warhammer II files, which have no regions.

## Layout

An ESF file (see [esf.md](esf.md)): ABCB for Warhammer III, ABCA for every other game.

```
CAMPAIGN_DYNAMIC_RESOURCES v0
  ascii                         build time (UTC): year, month, day, hour, minute, second, none padded
  REGION_RESOURCE_INFO_BLOCK v0, one entry per region with tiles:
    u16                         0xFE00 | region index
    REGION_RESOURCE_INFO_AREAS_BLOCK v1, one entry per building level:
      ascii building_superchain, u32 level, ascii tile_upgrade_id
      REGION_RESOURCE_AREAS_BLOCK v0, one entry per group of hexes:
        u16[]                   column, row, column, row, ...
```

The build time text matches the header's timestamp: Troy's troy_mythic, built 2021-03-19
09:00:01, has `2021319901`. Only Rome II's and Attila's shipped files have regions; Warhammer
III's only map with inputs, wh3_main_prologue_map, paints its whole image one colour no
resource uses.

## How the builder fills it

Read from Rome II's and Attila's `ToolDataBuilderDll.AssemblyKit.dll`.

- **Which hexes.** Each database row is a colour. Hex (c, r) reads pixel
  (2c, height - 1 - (2r + c % 2)) of the image: two pixels per column and per row, one more for
  the odd columns' half-row offset, row 0 at the bottom. A hex whose pixel has a row's colour gets
  that row's tiles, unless map_data.esf types the hex as open sea. Sea regions count
  (main_rome_map's region 177 has a tile). Where two rows share a colour, the first is used.
- **Building levels.** A region's levels are keyed by superchain and level only, not by tile
  upgrade, so colours that differ only in their tile upgrade make one level. Its tile upgrade is
  that of its first hex (main_rome_map paints farmland_northern colours inside
  farmland_mediterranean farmland and gets mediterranean tiles there).
- **Groups.** The builder walks the grid column by column. Each hex joins the first of its
  level's groups that holds one of its neighbours, or starts a new group. Then, until a pass
  changes nothing, each group in turn takes in every later group that touches it, and the emptied
  groups are dropped.
- **Order.** Regions, and each region's levels, are CA_STD hash maps (the one in
  [esf.md](esf.md#string-tables)) filled in walking order, and written in the maps' order. A
  region hashes as its u16 xored with `0x4A545EED`. A level hashes as its superchain followed by
  its level in decimal, through the game's string hash. Each game uses its string tables' map;
  for the games after Attila that is read from Warhammer III's builder, since none of their
  shipped files has a region to check it against.
