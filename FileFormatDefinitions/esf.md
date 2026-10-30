# ESF files (ABCA, ABCB)

The container map_data.esf and dynamic_resources.esf are written in, as the games' own writer
lays it out. `EsfLibrary/Esf/EsfWriter.cs` writes it; the node layout is the one RPFM documents
in `rpfm_lib/src/files/esf/`.

## Header

| Bytes | Value |
|---|---|
| 0-3 | `0xABCA` (Rome II to Three Kingdoms) or `0xABCB` (Warhammer III) |
| 4-7 | 0 |
| 8-11 | build time, Unix seconds |
| 12-15 | offset of the string tables, after the nodes |

## String tables

After the nodes come three tables: node names (u16 count, then u16 length and the name),
UTF-16 strings, and ASCII strings. An ASCII or UTF-16 entry is its length, the string and its
u32 id. The length is a u16 in ABCA and a u32 in ABCB. A string's id is the order in which it was
first used in the nodes, and values in the nodes refer to it by id.

The entries are listed in the order of a hash map of the strings, not by id:

- **Rome II and Attila** use CA_STD's hash map: 4 buckets to start, a maximum load of 1.0, and a
  rehash to 2n + 1 buckets on the insert that passes it. A bucket is a list that keys join at the
  back, a rehash moves keys bucket by bucket from the front, and iteration runs from bucket 0
  up (`CaHashMapOrder`). The hash is CALibs' string hash (`CaStringHash`):
  - Rome II: the characters at even positions summed into the low 16 bits, the odd ones into
    the high 16.
  - Attila: starting from `0xF73A51C9`, each character from the last xored in, shifted left by
    0, 8, 16 and 24 bits in turn.
- **The 64-bit games** use a different map, which isn't reproduced; the writer lists their
  strings by id. Readers look strings up by id, so the order doesn't change what the file holds.

## Sizes and the write buffer

Every record, block, block entry and array is preceded by its size, written as CAULEB128 (seven
bits a byte, most significant first). The games write through a 1 MiB buffer and put out each
size as a five-byte placeholder that is filled in once the size is known. While the placeholder
is still in the buffer it shrinks to the size's shortest encoding; once the buffer has been
flushed past it, the size keeps all five bytes, padded with leading `0x80`s. So where sizes are
padded depends on where the buffer flushed, and that depends on how the data was cut into writes.
The writer makes the same writes the games do.
