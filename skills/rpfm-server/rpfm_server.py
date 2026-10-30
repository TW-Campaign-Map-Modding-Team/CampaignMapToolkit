"""Talk to RPFM's rpfm_server the way CAIME's RPFM database workflow does.

Reads db tables through the same JSON-RPC methods CAIME calls (files.list, then table.rows
for each fragment), from one of:
  --game-packs     the game's own packs (pack.open_vanilla), as CAIME reads vanilla tables
  --mod NAME       mods by .pack file name, found by RPFM in the data or Workshop folder
                   (dependencies.rebuild on a throwaway pack), as CAIME reads a project's mods
  --pack PATH      one .pack file on disk (pack.open)
and prints each fragment's columns and rows as JSON, one fragment a line. Also lists the db
tables in one .pack. Starts rpfm_server from --rpfm when none is already listening; the server
exits by itself once the last session disconnects.

Requires: pip install websocket-client

Examples:
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 list "<pack>" --table regions
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 rows --table regions --game-packs
  python rpfm_server.py --rpfm "<RPFM folder>" --game warhammer_3 rows --table regions --mod my_campaign.pack
"""

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.request

import websocket

SERVER_URL = "ws://127.0.0.1:45127/ws"
SESSIONS_URL = "http://127.0.0.1:45127/sessions"
STARTUP_TIMEOUT_SECONDS = 30
SERVER_EXIT_TIMEOUT_SECONDS = 10
JOB_WAIT_SECONDS = 30
# Rows asked for in one table.rows call; RPFM sends 100 when not told, and takes any limit.
ROWS_PER_PAGE = 10000


class RpfmError(Exception):
    pass


def connect(rpfm_folder):
    try:
        return websocket.create_connection(SERVER_URL, timeout=900)
    except OSError:
        pass

    server = os.path.join(rpfm_folder, "rpfm_server.exe")
    if not os.path.isfile(server):
        raise RpfmError(f"rpfm_server.exe not found in {rpfm_folder} (RPFM 5.3 or later is needed)")

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
        if greeting.get("method") != "session.connected":
            raise RpfmError(f"unexpected greeting: {greeting}")

    def call(self, method, params=None):
        self.last_id += 1
        request = {"jsonrpc": "2.0", "id": self.last_id, "method": method}
        if params is not None:
            request["params"] = params
        self.ws.send(json.dumps(request))
        while True:
            message = json.loads(self.ws.recv())
            # Notifications, such as job.updated, have no id.
            if message.get("id") != self.last_id:
                continue
            if "error" in message:
                raise RpfmError(f"{method}: {message['error'].get('message')}")
            return message.get("result")

    def call_job(self, method, params=None):
        """Calls a method RPFM runs as a job, and returns the job's result once it ends."""
        job = self.call(method, params)["job"]
        while True:
            status = self.call("job.wait", {"job": job, "timeout_secs": JOB_WAIT_SECONDS})
            if status["state"] == "finished":
                return status.get("result")
            if status["state"] == "failed":
                raise RpfmError(f"{method}: {status['error'].get('message')}")
            if status["state"] == "cancelled":
                raise RpfmError(f"{method}: cancelled")

    def close(self):
        # The server may close the socket rather than answer.
        self.last_id += 1
        self.ws.send(json.dumps({"jsonrpc": "2.0", "id": self.last_id, "method": "session.disconnect"}))
        try:
            while self.ws.recv():
                pass
        except (websocket.WebSocketException, OSError):
            pass
        self.ws.close()
        wait_for_server_exit_if_idle()


def wait_for_server_exit_if_idle():
    """The server exits once its last session ends, but accepts connections for a moment while
    it shuts down; a run started then loses its connection mid-request. Wait it out."""
    try:
        with urllib.request.urlopen(SESSIONS_URL, timeout=2) as response:
            if json.load(response):
                return
    except OSError:
        return
    deadline = time.time() + SERVER_EXIT_TIMEOUT_SECONDS
    while time.time() < deadline:
        try:
            urllib.request.urlopen(SESSIONS_URL, timeout=1).close()
        except OSError:
            return
        time.sleep(0.1)


def select_game(session, game):
    status = session.call_job("session.set_game", {"game": game, "rebuild_dependencies": False})
    if not status["schema_loaded"]:
        raise RpfmError(f"RPFM has no schema for {game}; open the game in RPFM once")


def list_files(session, source, prefix):
    paths = []
    while True:
        page = session.call("files.list", {"source": source, "path_prefix": prefix, "offset": len(paths)})
        paths += [f["path"] for f in page["files"]]
        if not page["files"] or len(paths) >= page["total"]:
            return paths


def read_rows(session, source, path):
    """Returns a fragment's columns and every row's values, paging through table.rows."""
    columns, rows = None, []
    while True:
        page = session.call("table.rows", {"file": {"source": source, "path": path}, "offset": len(rows), "limit": ROWS_PER_PAGE})
        columns = columns or page["columns"]
        rows += [row["values"] for row in page["rows"]]
        if not page["rows"] or len(rows) >= page["total"]:
            return columns, rows


def open_source(session, args):
    """Returns the source to read from."""
    if args.game_packs:
        try:
            return {"pack": session.call("pack.open_vanilla")["key"]}
        except RpfmError as error:
            raise RpfmError(f"{error} (is the game's folder set in RPFM's settings?)")

    if args.mod:
        key = session.call("pack.new")["key"]
        session.call("pack.update", {"pack": key, "dependencies": [{"enabled": True, "name": name} for name in args.mod]})
        session.call_job("dependencies.rebuild", {"only_parent_packs": True})
        # The reply also carries RPFM's whole vanilla file list, so it can take a while.
        loaded = session.call("dependencies.info")
        found = sorted({f["container_name"] for f in loaded["parent_packed_files"] if f["container_name"]})
        print("mods RPFM loaded: " + (", ".join(found) or "none"), file=sys.stderr)
        return "parent_files"

    return {"pack": session.call("pack.open", {"paths": [os.path.abspath(args.pack)]})["key"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--rpfm", required=True, help="RPFM installation folder (contains rpfm_server.exe)")
    parser.add_argument("--game", required=True, help="RPFM game key, e.g. warhammer_3, three_kingdoms, rome_2")
    sub = parser.add_subparsers(dest="action", required=True)

    list_cmd = sub.add_parser("list", help="list db table entries in one .pack")
    list_cmd.add_argument("pack")
    list_cmd.add_argument("--table", help="only this table, e.g. regions (no _tables suffix)")

    rows_cmd = sub.add_parser("rows", help="print db tables' rows as JSON")
    rows_cmd.add_argument("--table", action="append", required=True,
                          help="table to read, e.g. regions (no _tables suffix); repeatable")
    source = rows_cmd.add_mutually_exclusive_group(required=True)
    source.add_argument("--game-packs", action="store_true", help="the game's own packs")
    source.add_argument("--mod", action="append", help="a mod's .pack file name; repeatable")
    source.add_argument("--pack", help="one .pack file on disk")

    args = parser.parse_args()

    session = Session(args.rpfm)
    try:
        select_game(session, args.game)

        if args.action == "list":
            key = session.call("pack.open", {"paths": [os.path.abspath(args.pack)]})["key"]
            prefix = f"db/{args.table}_tables/" if args.table else "db/"
            print("\n".join(list_files(session, {"pack": key}, prefix)))
            return

        source = open_source(session, args)
        found = False
        for table in args.table:
            for path in list_files(session, source, f"db/{table}_tables/"):
                columns, rows = read_rows(session, source, path)
                print(json.dumps({"path": path, "columns": columns, "rows": rows}))
                found = True
        if not found:
            print("no fragments of those tables found", file=sys.stderr)
    finally:
        session.close()


if __name__ == "__main__":
    try:
        main()
    except RpfmError as error:
        sys.exit(f"RPFM: {error}")
