"""Talk to RPFM's rpfm_server the way CAIME's RPFM database workflow does.

Extracts db tables as TSV through the same WebSocket commands CAIME sends, from one of:
  --game-packs     the game's own packs (LoadAllCAPackFiles), as CAIME reads vanilla tables
  --mod NAME       mods by .pack file name, found by RPFM in the data or Workshop folder
                   (RebuildDependencies on a throwaway pack), as CAIME reads a project's mods
  --pack PATH      one .pack file on disk (OpenPackFiles)
Also lists the db tables in one .pack. Starts rpfm_server from --rpfm when none is already
listening; the server exits by itself once the last session disconnects.

Requires: pip install websocket-client

Examples:
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 list "<pack>" --table regions
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 extract "<out dir>" --table regions --game-packs
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 extract "<out dir>" --table regions --mod my_campaign.pack
"""

import argparse
import json
import os
import subprocess
import sys
import time

import websocket

SERVER_URL = "ws://127.0.0.1:45127/ws"
STARTUP_TIMEOUT_SECONDS = 30


class RpfmError(Exception):
    pass


def connect(rpfm_folder):
    try:
        return websocket.create_connection(SERVER_URL, timeout=900)
    except OSError:
        pass

    server = os.path.join(rpfm_folder, "rpfm_server.exe")
    if not os.path.isfile(server):
        raise RpfmError(f"rpfm_server.exe not found in {rpfm_folder} (RPFM 5.0 or later is needed)")

    # stderr is the server's log; discard it so a full pipe can never stall the server.
    subprocess.Popen([server], cwd=rpfm_folder, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    deadline = time.time() + STARTUP_TIMEOUT_SECONDS
    while time.time() < deadline:
        try:
            return websocket.create_connection(SERVER_URL, timeout=900)
        except OSError:
            time.sleep(0.25)

    raise RpfmError("rpfm_server did not start listening in time")


class Session:
    def __init__(self, rpfm_folder):
        self.ws = connect(rpfm_folder)
        self.last_id = 0
        greeting = json.loads(self.ws.recv())
        if "SessionConnected" not in (greeting.get("data") or {}):
            raise RpfmError(f"unexpected greeting: {greeting}")

    def send(self, command, expected):
        self.last_id += 1
        self.ws.send(json.dumps({"id": self.last_id, "data": command}))
        while True:
            message = json.loads(self.ws.recv())
            # id 0 carries unsolicited broadcasts such as SettingsChanged.
            if message["id"] != self.last_id:
                continue
            data = message["data"]
            if isinstance(data, dict) and "Error" in data:
                raise RpfmError(data["Error"])
            # Commands without a payload answer with a bare string such as "Success".
            if data == expected:
                return None
            if not isinstance(data, dict) or expected not in data:
                raise RpfmError(f"unexpected response: {json.dumps(data)[:300]}")
            return data[expected]

    def close(self):
        # The server answers by closing the socket, not with a reply.
        self.last_id += 1
        self.ws.send(json.dumps({"id": self.last_id, "data": "ClientDisconnecting"}))
        self.ws.close()


def select_game(session, game):
    session.send({"SetGameSelected": [game, False]}, "CompressionFormatDependenciesInfo")
    if not session.send("IsSchemaLoaded", "Bool"):
        raise RpfmError(f"RPFM has no schema for {game}; open the game in RPFM once")
    if not session.send({"SettingsGetString": game}, "String"):
        raise RpfmError(f"RPFM does not know where {game} is installed; set it in RPFM's settings")


def open_source(session, args):
    """Returns (pack key, data source) to extract from."""
    if args.game_packs:
        key, _ = session.send("LoadAllCAPackFiles", "StringContainerInfo")
        return key, "PackFile"

    if args.mod:
        key = session.send("NewPack", "String")
        session.send({"SetDependencyPackFilesList": [key, [[True, name] for name in args.mod]]}, "Success")
        # The reply also carries RPFM's whole vanilla file list, so it can take a while.
        loaded = session.send({"RebuildDependencies": False}, "DependenciesInfo")
        found = sorted({f["container_name"] for f in loaded["parent_packed_files"] if f["container_name"]})
        print("mods RPFM loaded: " + (", ".join(found) or "none"), file=sys.stderr)
        return key, "ParentFiles"

    key, _ = session.send({"OpenPackFiles": [os.path.abspath(args.pack)]}, "StringContainerInfo")
    return key, "PackFile"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--rpfm", required=True, help="RPFM installation folder (contains rpfm_server.exe)")
    parser.add_argument("--game", required=True, help="RPFM game key, e.g. warhammer_3, three_kingdoms, rome_2")
    sub = parser.add_subparsers(dest="action", required=True)

    list_cmd = sub.add_parser("list", help="list db table entries in one .pack")
    list_cmd.add_argument("pack")
    list_cmd.add_argument("--table", help="only this table, e.g. regions (no _tables suffix)")

    extract_cmd = sub.add_parser("extract", help="extract db tables as TSV")
    extract_cmd.add_argument("destination")
    extract_cmd.add_argument("--table", action="append", required=True,
                             help="table to extract, e.g. regions (no _tables suffix); repeatable")
    source = extract_cmd.add_mutually_exclusive_group(required=True)
    source.add_argument("--game-packs", action="store_true", help="the game's own packs")
    source.add_argument("--mod", action="append", help="a mod's .pack file name; repeatable")
    source.add_argument("--pack", help="one .pack file on disk")

    args = parser.parse_args()

    session = Session(args.rpfm)
    try:
        select_game(session, args.game)

        if args.action == "list":
            key, _ = session.send({"OpenPackFiles": [os.path.abspath(args.pack)]}, "StringContainerInfo")
            _, files = session.send({"GetPackFileDataForTreeView": key}, "ContainerInfoVecRFileInfo")
            prefix = f"db/{args.table}_tables/" if args.table else "db/"
            print("\n".join(sorted(f["path"] for f in files if f["path"].lower().startswith(prefix.lower()))))
            return

        key, data_source = open_source(session, args)
        # Whole table folders: RPFM skips any it does not have, so nothing needs listing first.
        folders = {data_source: [{"Folder": f"db/{table}_tables"} for table in args.table]}
        _, written = session.send(
            {"ExtractPackedFiles": [key, folders, os.path.abspath(args.destination), True]}, "StringVecPathBuf")
        print("\n".join(written) or "no fragments of those tables found")
    finally:
        session.close()


if __name__ == "__main__":
    try:
        main()
    except RpfmError as error:
        sys.exit(f"RPFM: {error}")
