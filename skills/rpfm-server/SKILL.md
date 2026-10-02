---
name: rpfm-server
description: Extract db tables as TSV from a Total War game's own packs, from mods looked up by .pack name, or from one .pack, through RPFM's rpfm_server (RPFM 5+), using the same WebSocket commands CAIME's RPFM database workflow sends. Use it to check what RPFM returns, reproduce an RPFM workflow failure outside CAIME, or look at the TSV CAIME converts to Assembly Kit XML.
---

# RPFM server

CAIME's RPFM database source (`CAIME/Classes/Common/Rpfm/`) reads db tables through
`rpfm_server.exe`, which RPFM 5 ships in place of the old `rpfm_cli.exe`: vanilla tables from
the game's own packs, and a project's mod (with its dependencies) by `.pack` file name. `rpfm_server.py` makes the
same calls from the command line, so you can see exactly what the server returns without
opening a project.

Requires Python 3 and `pip install websocket-client`.

```
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 extract "<out dir>" --table regions --game-packs
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 extract "<out dir>" --table regions --mod my_campaign.pack
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 extract "<out dir>" --table regions --pack "<pack>"
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 list "<pack>" --table regions
```

- `--game` takes RPFM's game key; `GameMappingProvider.cs` maps CAIME's games to them.
- `--table` is the table name without the `_tables` suffix, as in
  `DatabaseTableProvider.cs`; repeat it for several tables.
- `extract` writes `<out dir>/db/<table>_tables/<fragment>.tsv`, the layout
  `ExtractedTableFragment` reads back.

## Protocol notes

- The server listens on `ws://127.0.0.1:45127/ws`. If RPFM is open, its server is already
  running; every connection gets its own session, so open packs are never shared.
- Each message is `{"id": n, "data": <command or response>}`; commands and responses follow
  serde's enum encoding (`"Unit"`, `{"Newtype": v}`, `{"Tuple": [a, b]}`).
- The server greets each connection with `{"id": 0, "data": {"SessionConnected": n}}` and
  uses id 0 for other unsolicited broadcasts.
- RPFM keeps each game's install folder as the setting named after the game key
  (`SettingsGetString`). Without it, `LoadAllCAPackFiles` fails with only "cannot find the
  path specified", and mods are not found at all.
- Extract whole table folders (`{"Folder": "db/<table>_tables"}`) rather than listing first:
  missing folders are skipped silently, and listing the game's packs returns ~750k entries
  for Warhammer 3.
- Mods are loaded by name only as the dependencies of an open pack: `NewPack`, then
  `SetDependencyPackFilesList`, then `RebuildDependencies(false)`, then extract from the
  `ParentFiles` source. Per `load_parent_pack` in RPFM's
  `rpfm_extensions/src/dependencies/mod.rs`:
  - each name is looked up in `data`, then RPFM's secondary folder, then the Workshop
    folders, and the first hit wins, so a local copy in `data` beats the Workshop one;
  - names are compared to file names exactly, case included;
  - every loaded pack's own dependency list is followed recursively, so one mod name pulls in
    the whole chain;
  - a pack's dependencies are loaded before its own files are added over them, so on identical
    paths the dependent mod's file wins.
- A name RPFM can't find is ignored without an error; compare against
  `parent_packed_files[].container_name` in the reply. That reply also carries the entire
  vanilla file list (~100 MB for Warhammer 3), so read it as a stream.
- `OpenPackFiles` merges every path it is given into one pack, so open packs one at a time
  to keep track of which fragment came from which pack.
- Send `"ClientDisconnecting"` before closing. The server closes the socket without
  replying, and exits once its last session is gone.

Full reference: https://frodo45127.github.io/rpfm/manual/server/overview.html
