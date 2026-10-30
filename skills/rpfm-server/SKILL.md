---
name: rpfm-server
description: Read db tables' rows as JSON from a Total War game's own packs, from mods looked up by .pack name, or from one .pack, through RPFM's rpfm_server (RPFM 5.3+), using the same JSON-RPC methods CAIME's RPFM database workflow sends. Use it to check what RPFM returns, reproduce an RPFM workflow failure outside CAIME, or look at the rows CAIME converts to Assembly Kit XML.
---

# RPFM server

CAIME's RPFM database source (`CAIME/Classes/Common/Rpfm/`) reads db tables through
`rpfm_server.exe`, RPFM's headless backend: vanilla tables from
the game's own packs, and a project's mod (with its dependencies) by `.pack` file name. `rpfm_server.py` makes the
same calls from the command line, so you can see exactly what the server returns without
opening a project.

Requires Python 3 and `pip install websocket-client`.

```
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 rows --table regions --game-packs
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 rows --table regions --mod my_campaign.pack
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 rows --table regions --pack "<pack>"
python skills/rpfm-server/rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 list "<pack>" --table regions
```

- `--game` takes RPFM's game key; `GameMappingProvider.cs` maps CAIME's games to them.
- `--table` is the table name without the `_tables` suffix, as in
  `DatabaseTableProvider.cs`; repeat it for several tables.
- `rows` prints one JSON line per fragment: its in-pack `path`, its `columns` and its `rows`'
  values, as `RpfmService` reads them into a `TableFragment`.

## Protocol notes

The protocol is JSON-RPC 2.0 over a WebSocket, documented in RPFM's `docs/server`
(`ws-protocol.md`, and `methods/*.md` for every method's params and result). It needs RPFM 5.3
or later.

- The server listens on `ws://127.0.0.1:45127/ws`. If RPFM is open, its server is already
  running; every connection gets its own session, so open packs are never shared.
- The server greets each connection with a `session.connected` notification. Notifications
  (such as `job.updated`) have no `id`; responses carry the request's `id` and a `result` or an
  `error` with a `message`.
- `session.set_game` and `dependencies.rebuild` run as jobs: they answer `{"job": n}` at once,
  and `job.wait` returns the job's state, with its `result` once it is `finished`.
- `session.set_game`'s result says whether the game's schema is loaded (`schema_loaded`).
  There is no method to read RPFM's settings; without the game's folder in them,
  `pack.open_vanilla` fails with only "cannot find the path specified", and mods are not found
  at all.
- Find a table's fragments with `files.list` and `path_prefix: "db/<table>_tables/"`. The
  server filters by prefix itself, so this is instant even though the game's packs hold ~750k
  files for Warhammer 3, and a table the source lacks lists nothing. Pages hold 500 files;
  pass `offset` until `total` is reached.
- Read a fragment with `table.rows` and `{"file": {"source": ..., "path": "db/<table>_tables/<fragment>"}}`.
  It answers `columns` (the fragment's own table version), `total` and `rows`, each
  `{"index", "values"}` with JSON-typed values: booleans as booleans, a colour as an `RRGGBB`
  string, and an F32 as the shortest number that reads back as it (533.74, not
  533.739990234375). It sends 100 rows unless given a `limit`, and takes any limit with an
  `offset`.
- Do not use `files.extract` with `as_tsv: true` to read tables: its TSV prints floats with four
  decimals, so a value like 756.83997 comes back as 756.8400, a different F32.
- Mods are loaded by name only as the dependencies of an open pack: `pack.new`, then
  `pack.update` with `dependencies`, then `dependencies.rebuild` with
  `only_parent_packs: true`, then read from the `"parent_files"` source. Per
  `load_parent_pack` in RPFM's `rpfm_extensions/src/dependencies/mod.rs`:
  - each name is looked up in `data`, then RPFM's secondary folder, then the Workshop
    folders, and the first hit wins, so a local copy in `data` beats the Workshop one;
  - names are compared to file names exactly, case included;
  - every loaded pack's own dependency list is followed recursively, so one mod name pulls in
    the whole chain;
  - a pack's dependencies are loaded before its own files are added over them, so on identical
    paths the dependent mod's file wins.
- A name RPFM can't find is ignored without an error; compare against
  `parent_packed_files[].container_name` in `dependencies.info`. That result also carries the
  entire vanilla file list, so read it as a stream.
- `pack.open` merges every path it is given into one pack, so open packs one at a time
  to keep track of which fragment came from which pack.
- Call `session.disconnect` before closing; the server removes the session at once and exits
  once its last session is gone. For about a second while it shuts down it still accepts
  connections, then drops them, so a session opened right after the last one ended fails
  mid-request. Wait it out: if `GET http://127.0.0.1:45127/sessions` returns `[]` after
  disconnecting, wait for the server to exit (`GET /version` returns its `pid`) before
  connecting again.

Full reference: https://github.com/Frodo45127/rpfm/tree/master/docs/server
