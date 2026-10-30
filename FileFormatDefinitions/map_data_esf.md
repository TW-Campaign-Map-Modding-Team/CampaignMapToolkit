# map_data.esf

A campaign map's regions, areas, settlements and geometry, which the game reads instead of
`map.hex`. The Assembly Kit's builder makes it from `map.hex` and, of the database, only the
first `campaign_map_playable_areas` row for the map: its `index` (stored as the theatre's name)
and `maxx`, plus `maxy` on Rome II. CAIME's writers are in `CAIME/Classes/Exporters/MapData/`:
`MapDataModel` works out what every game shares, and each `*MapDataWriter` lays it out for its
game. [map_data_esf_open_questions.md](map_data_esf_open_questions.md) records what was learned
from which builder, and what is still open.

## Layouts

An ESF file (see [esf.md](esf.md)). MAP_DATA's version tells the three layouts apart:

| Game | MAP_DATA | ESF | Writer | Hex radius rounding |
|---|---|---|---|---|
| Rome II | v0 | ABCA | Attila's, keeping maxy | per column and a half |
| Attila, Thrones of Britannia | v0 | ABCA | `AttilaMapDataWriter` | per column and a half |
| Warhammer | v1 | ABCA | Pharaoh's, once per mask | per column and a half |
| Warhammer II, Troy | v1 | ABCA | Warhammer's | two thirds per column |
| Pharaoh | v1 | ABCA | `PharaohMapDataWriter` | two thirds per column |
| Warhammer III | v2 | ABCB | `Wh3MapDataWriter` | two thirds per column |
| Three Kingdoms | v2 | ABCA | Warhammer III's, with its own layout | two thirds per column |

Thrones of Britannia is taken to be Attila's; no Thrones file has been checked.

### v2: Warhammer III, Three Kingdoms

The full layout. REGIONS_DATA holds a MASKED_REGIONS_DATA entry per campaign mask, per-hex region
runs and areas of interest, and each REGION_DATA has a REGION_INDEX. Three Kingdoms differs in:
ABCA rather than ABCB; MAP_FILE_DATA_VERSION 2; MAP_FILE_VALIDATION holding the map.hex's save
time; and packed area references and per-hex runs, as in Pharaoh.

### v0: Rome II, Attila

Compared with Warhammer III:

- no version records;
- REGIONS_DATA holds empty `trade_nodes` and `bridges` records, the theatre, the bounds and the
  regions directly: no MASKED_REGIONS_DATA, no per-hex region runs, no areas of interest;
- seven bytes per hex, the first two the region and area;
- REGION_DATA has no REGION_INDEX;
- an area's touching areas are packed u16s;
- settlements have several building slots.

Rome II's builder writes Attila's file, but stores the playable area's maxy as given rather than
the top of the hex grid. Rome II's packs have no maxy (the table lost minx, miny and maxy after
version 4), so the writer estimates it from the grid when the table has none. The game's own
main_rome_map was built by an older builder: its CAMPAIGN_THEATRE is v2, with a 256-colour table
before HEX_MAP_DATA. The Assembly Kit's builder writes v3 with no table, as Attila's does.

### v1: Pharaoh, Warhammer, Warhammer II, Troy

Between Attila's and Warhammer III's: a MAP_FILE_DATA_VERSION record but no validation record;
MASKED_REGIONS_DATA and REGION_INDEX as in Warhammer III; five bytes per hex, no areas of
interest; packed area references and settlements with building slots, as in Attila; and the
per-hex runs as a u16 array. A port lists the main slot's hexes after its own, as in Warhammer
III.

Warhammer's builder writes Pharaoh's file once for each campaign mask the map.hex lists: the
UI's region list repeats for each mask, and each gets a MASKED_REGIONS_DATA entry under its name.
The main maps have only the unnamed mask; the mini-campaign maps add `8` and `32`. Each mask also
has a bitmap of hexes, which the builder makes impassable in that mask's entry. Every vanilla
bitmap is empty and CAIME discards them, so every entry it writes is a copy of the unnamed one.
Warhammer II's and Troy's builders write Warhammer's file.
