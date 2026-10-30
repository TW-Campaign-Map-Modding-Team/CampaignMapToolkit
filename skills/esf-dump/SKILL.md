---
name: esf-dump
description: Dump an ABCA/ABCB .esf file (Warhammer III map_data.esf and similar) as a tree that keeps every node's exact type code, or compare two such files node by node - for checking CAIME's map_data.esf writer (Wh3MapDataWriter) against an Assembly Kit build, or working out what a field of an ESF file holds.
---

# ESF dump

`esf_dump.py` reads an ABCA or ABCB file on its own (no EsfLibrary), so what it prints is
exactly what is in the file: optimised integer encodings, string table ids and 24-bit values
included. It takes about a second on a 10 MB map_data.esf.

```
python skills/esf-dump/esf_dump.py <file.esf>                         # whole tree, 3 entries per block
python skills/esf-dump/esf_dump.py <file.esf> --max-entries 50 --path "/MAP_DATA/REGIONS_DATA/MASKED_REGIONS_DATA[0]/REGIONS_BLOCK[9]"
python skills/esf-dump/esf_dump.py <a.esf> --compare <b.esf>
```

- `--path` prints only the nodes under a path; block entries are `[n]`, values `.n`.
- `--compare` prints `IDENTICAL` (ignoring the timestamp at bytes 8-11), `IDENTICAL NODES` when
  only the string table order differs, or the first nodes that differ. Arrays report how many
  items differ and where the first difference is.

## Notes

- 24-bit integers (`0x18`, `0x1c`) are big-endian.
- Sizes are CAULEB128: seven bits a byte, most significant first. The games pad a size to five
  bytes (leading `0x80`s) when their 1 MiB write buffer was flushed while it was open; see
  `EsfLibrary/Esf/EsfWriter.cs`.
- The games list the string tables in the order of a hash map of the strings; ids are the order
  of first use in the tree. Every game's order is known (`EsfLibrary/Esf/CaHashMap.cs`,
  `CaStringHash.cs`, and FileFormatDefinitions/esf.md), so a writer's string tables should match
  byte for byte.
- Python's numpy `float32` reproduces the games' float arithmetic when every step is cast, which
  is how the map_data.esf geometry in `CAIME/Classes/Exporters/MapData/MapDataGeometry.cs` was
  matched.
