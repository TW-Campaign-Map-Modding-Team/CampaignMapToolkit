"""Finds out what a Total War Assembly Kit data builder (ToolDataBuilder*.dll) reads to build a
campaign map's map_data.esf or dynamic_resources.esf, and which of it changes the output.

    python tooldatabuilder.py build
    python tooldatabuilder.py trace          --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map
    python tooldatabuilder.py trace-dyn-res  --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map
    python tooldatabuilder.py ablate         --game warhammer3 --ak "<assembly_kit>" --map wh3_main_prologue_map [--full]
    python tooldatabuilder.py string-layout  --game phar --ak "<assembly_kit>"

--game takes MapDataBuilder's game names. --hex gives a map.hex for a map the Assembly Kit does not
ship (Rome II and Attila ship none; the repo's Templates folder has one per game). Every command
only reads from the Assembly Kit: output goes to --work, by default <temp>/tooldatabuilder-probe.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))

# As MapDataBuilder/main.cpp loads and calls them: pointer size, DLL, how the export's CA::String
# arguments are mangled, and whether MapDataBuilder creates an empty map_data.esf first.
X86_STRUCT = 'YA_NABUString@CA@@'
X64_STRUCT = 'YA_NAEBUString@CA@@'
X64_CLASS = 'YA_NAEBVString@CA@@'
GAMES = {
    'rome2':          (32, 'ToolDataBuilderDll.AssemblyKit.dll',     X86_STRUCT, True),
    'attila':         (32, 'ToolDataBuilderDll.AssemblyKit.dll',     X86_STRUCT, True),
    'thrones':        (32, 'ToolDataBuilderDll.AssemblyKit.dll',     X86_STRUCT, True),
    'warhammer':      (64, 'ToolDataBuilderDll.AssemblyKit.x64.dll', X64_STRUCT, False),
    'warhammer2':     (64, 'ToolDataBuilderDLL.modder.x64.dll',      X64_STRUCT, False),
    'troy':           (64, 'ToolDataBuilderDLL.modder.x64.dll',      X64_STRUCT, False),
    'phar':           (64, 'ToolDataBuilderDLL.modder.x64.dll',      X64_STRUCT, False),
    'warhammer3':     (64, 'ToolDataBuilderDLL.modder.x64.dll',      X64_CLASS,  False),
    'three_kingdoms': (64, 'ToolDataBuilder.modder.x64.dll',         X64_CLASS,  False),
}

# map_data.esf and dynamic_resources.esf hold the Unix time they were written at bytes 8-11.
ESF_TIMESTAMP = range(8, 12)


class Game:
    def __init__(self, args):
        self.name = args.game
        self.bits, self.dll, mangled, self.precreate = GAMES[args.game]
        self.map_data_export = f'?do_campaign_maps_regions_process@TOOLDATABUILDER@@{mangled}000@Z'
        self.dyn_res_export = f'?do_dynamic_resources_process@TOOLDATABUILDER@@{mangled}00000@Z'
        self.ak = os.path.abspath(args.ak)
        self.binaries = os.path.join(self.ak, 'binaries')
        self.db = os.path.join(self.ak, 'raw_data', 'db')
        self.design = os.path.join(self.ak, 'raw_data', 'EmpireDesignData')
        self.map = args.map
        self.hex = os.path.abspath(args.hex) if args.hex else os.path.join(self.design, 'campaign_maps', args.map or '', 'map.hex')
        self.work = os.path.join(os.path.abspath(args.work), self.name)
        self.probe = os.path.join(os.path.abspath(args.work), f'probe{self.bits}.exe')
        if not os.path.exists(self.probe):
            sys.exit(f'{self.probe} is missing - run "python {os.path.basename(__file__)} build" first.')


def forward(path):
    return path.replace('\\', '/')


def fresh_dir(path):
    if os.path.exists(path):
        shutil.rmtree(path)
    os.makedirs(path)
    return path


# ------------------------------------------------------------------------------- build

def build(args):
    vswhere = os.path.join(os.environ.get('ProgramFiles(x86)', r'C:\Program Files (x86)'), 'Microsoft Visual Studio', 'Installer', 'vswhere.exe')
    install = subprocess.run([vswhere, '-latest', '-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64',
                              '-property', 'installationPath'], capture_output=True, text=True).stdout.strip()
    if not install:
        sys.exit('No Visual Studio with the C++ x86/x64 build tools found.')
    os.makedirs(args.work, exist_ok=True)
    source = os.path.join(HERE, 'probe.cpp')
    for bits, vcvars in ((64, 'vcvars64.bat'), (32, 'vcvars32.bat')):
        out = os.path.join(args.work, f'probe{bits}')
        command = (f'"{os.path.join(install, "VC", "Auxiliary", "Build", vcvars)}" >nul && '
                   f'cl /nologo /O2 /EHsc /MT /std:c++17 "{source}" /Fe:"{out}.exe" /Fo:"{out}.obj" user32.lib')
        result = subprocess.run(command, shell=True, capture_output=True, text=True)
        print(f'probe{bits}.exe: {"built" if result.returncode == 0 else "FAILED"}')
        if result.returncode:
            sys.exit(result.stdout + result.stderr)


# ------------------------------------------------------------------------------- running the probe

def run_map_data(game, name, db, design):
    """Runs the data builder's map_data export once. Returns (exit code, esf bytes or None, trace path, output)."""
    job = os.path.join(game.work, name)
    work = fresh_dir(os.path.join(job, 'work'))
    os.makedirs(os.path.join(work, 'campaign_maps', game.map))
    trace = os.path.join(job, 'trace.log')
    result = subprocess.run([game.probe, 'trace-map-data', forward(game.binaries), game.dll, game.map_data_export, game.map,
                             forward(db), forward(design), forward(work), trace, '1' if game.precreate else '0'],
                            capture_output=True, text=True, timeout=3600)
    esf = os.path.join(work, 'campaign_maps', game.map, 'map_data.esf')
    data = open(esf, 'rb').read() if os.path.exists(esf) else None
    return result.returncode, data or None, trace, (result.stdout + result.stderr).strip()


def design_with_map(game):
    """The Assembly Kit's EmpireDesignData when it has the map, else a work folder holding --hex."""
    if os.path.exists(os.path.join(game.design, 'campaign_maps', game.map, 'map.hex')) and game.hex.startswith(game.design):
        return game.design
    folder = os.path.join(game.work, 'design')
    os.makedirs(os.path.join(folder, 'campaign_maps', game.map), exist_ok=True)
    shutil.copy(game.hex, os.path.join(folder, 'campaign_maps', game.map, 'map.hex'))
    return folder


def calls(trace):
    """The (op, access, status, path) lines between "#### call" and "#### done"."""
    lines, inside = [], False
    for line in open(trace, encoding='utf-8', errors='replace').read().splitlines():
        if line.startswith('#### call'):
            inside = True
        elif line.startswith('#### done'):
            break
        elif inside and line.count('\t') == 3:
            lines.append(line.split('\t'))
    return lines


def is_write(access):
    return int(access, 16) & (0x40000000 | 0x2 | 0x4 | 0x10000) != 0


def report_trace(trace, work_root):
    seen = set()
    for op, access, status, path in calls(trace):
        if re.search(r'\\windows\\|\.dll$|\\device\\|\\pipe\\', path, re.I) or status in ('c0000033', 'c000003a'):
            continue
        key = (op, status, path.lower(), is_write(access))
        if key not in seen:
            seen.add(key)
            print(f'  {op:6s} {"WRITE " if is_write(access) else ""}{"ok" if status == "00000000" else "status " + status}  {path}')
    for line in open(trace, encoding='utf-8', errors='replace').read().splitlines():
        fields = line.split('\t')
        if len(fields) == 4 and fields[0] in ('CREATE', 'OPEN') and is_write(fields[1]) \
                and work_root.lower() not in fields[3].lower() and not re.search(r'\\device\\|\\pipe\\', fields[3], re.I):
            print(f'  written outside the work folder: {fields[3]}')


def differing(left, right, ignore=ESF_TIMESTAMP):
    return [i for i in range(max(len(left), len(right)))
            if i not in ignore and (i >= len(left) or i >= len(right) or left[i] != right[i])]


def trace(args):
    game = Game(args)
    design = design_with_map(game)
    code, data, log, out = run_map_data(game, 'trace', game.db, design)
    print(f'{game.name} {game.map}: exit {code}, map_data.esf {len(data) if data else "not written"} bytes. {out}')
    report_trace(log, game.work)
    again = run_map_data(game, 'trace_again', game.db, design)[1]
    if data and again:
        print(f'  a second run differs at {differing(data, again, ignore=())} (bytes 8-11 are the ESF timestamp)')


def trace_dyn_res(args):
    game = Game(args)
    job = os.path.join(game.work, 'dyn_res')
    work = fresh_dir(os.path.join(job, 'work'))
    os.makedirs(os.path.join(work, 'campaign_maps', game.map))
    log = os.path.join(job, 'trace.log')
    result = subprocess.run([game.probe, 'trace-dyn-res', forward(game.binaries), game.dll, game.dyn_res_export, game.map,
                             forward(game.design), forward(work), log], capture_output=True, text=True, timeout=3600)
    print(f'{game.name} {game.map}: exit {result.returncode}. {(result.stdout + result.stderr).strip()}')
    report_trace(log, game.work)


def string_layout(args):
    game = Game(args)
    calibs = next((f for f in os.listdir(game.binaries) if re.match(r'calibs\..*\.dll$', f, re.I)), None)
    if calibs is None:
        sys.exit(f'No CALibs DLL in {game.binaries}.')
    result = subprocess.run([game.probe, 'string-layout', forward(game.binaries), calibs], capture_output=True, text=True)
    print(f'{game.name}: {calibs}\n{result.stdout}{result.stderr}')


# ------------------------------------------------------------------------------- ablation

def read_table(path):
    raw = open(path, 'rb').read()
    if raw[:2] in (b'\xff\xfe', b'\xfe\xff'):
        return raw.decode('utf-16')
    return raw.decode('utf-8-sig')


def split(table, text):
    """(text before the first record, [records], text after the last)."""
    pattern = re.compile(r'<%s(?:\s[^>]*)?>.*?</%s>\s*' % (table, table), re.S)
    matches = list(pattern.finditer(text))
    if not matches:
        end = text.rfind('</dataroot>')
        return text[:end], [], text[end:]
    return text[:matches[0].start()], [m.group(0) for m in matches], text[matches[-1].end():]


def columns(record):
    return list(dict.fromkeys(re.findall(r'<([A-Za-z0-9_]+)[\s>/]', record.split('>', 1)[1])))


def field(record, column):
    match = re.search(r'<%s(?:\s[^>]*)?>(.*?)</%s>' % (column, column), record, re.S)
    return match.group(1) if match else None


def drop_column(record, column):
    return re.sub(r'\s*<%s(?:\s[^>]*)?(?:/>|>.*?</%s>)' % (column, column), '', record, flags=re.S)


def set_field(record, column, value):
    return re.sub(r'(<%s(?:\s[^>]*)?>).*?(</%s>)' % (column, column), lambda m: m.group(1) + value + m.group(2), record, count=1, flags=re.S)


def bare(tables_rows):
    """Bare Assembly Kit XML for {table: [ {column: value} ]}."""
    out = {}
    for table, rows in tables_rows.items():
        body = ''.join('<%s>\n%s</%s>\n' % (table, ''.join('<%s>%s</%s>\n' % (k, v, k) for k, v in row.items()), table) for row in rows)
        out[table] = '<?xml version="1.0" encoding="UTF-8"?>\n<dataroot>\n' + body + '</dataroot>\n'
    return out


class Ablation:
    def __init__(self, game):
        self.game = game
        self.design = design_with_map(game)
        code, _, log, out = run_map_data(game, 'trace', game.db, self.design)
        self.tables = []
        for op, access, status, path in calls(log):
            match = re.search(r'\\([^\\]+)\.xml$', path)
            if match and status == '00000000' and os.path.normcase(os.path.dirname(path)).endswith(os.path.normcase(os.path.join('raw_data', 'db'))) \
                    and match.group(1) not in self.tables:
                self.tables.append(match.group(1))
        self.source = {t: read_table(os.path.join(game.db, t + '.xml')) for t in self.tables}
        self.base = self.run('base', self.source)[1]
        if self.base is None:
            sys.exit(f'The unchanged tables wrote no map_data.esf (exit {code}). {out}')
        again = self.run('base_again', self.source)[1]
        self.ignore = set(ESF_TIMESTAMP) | set(differing(self.base, again, ignore=()))
        print(f'{game.name} {game.map}: reads {", ".join(self.tables)}; map_data.esf {len(self.base)} bytes; '
              f'bytes that vary between identical runs: {sorted(self.ignore)}', flush=True)

    def run(self, name, tables):
        job = os.path.join(self.game.work, 'ablate_' + name)
        db = fresh_dir(os.path.join(job, 'db'))
        for table, text in tables.items():
            if text is not None:
                with open(os.path.join(db, table + '.xml'), 'w', encoding='utf-8', newline='') as f:
                    f.write(text)
        code, data, _, out = run_map_data(self.game, 'ablate_' + name, db, self.design)
        return code, data, out

    def report(self, label, tables):
        code, data, out = self.run('variant', tables)
        if data is None:
            verdict = f'FAILED (exit {code}, no map_data.esf)'
        elif not differing(self.base, data, self.ignore):
            verdict = 'IDENTICAL'
        else:
            verdict = f'DIFFERENT ({len(differing(self.base, data, self.ignore))} bytes, size {len(data)} vs {len(self.base)})'
        print(f'  {label:70s} {verdict}', flush=True)
        return verdict

    def own_rows(self, records):
        return [r for r in records if any(field(r, c) == self.game.map for c in columns(r))]

    def all(self, full):
        source = self.source
        relevant = {}
        for table in self.tables:
            head, records, tail = split(table, source[table])
            print(f'-- {table}: {len(records)} rows', flush=True)
            self.report(f'{table}: file missing', {**source, table: None})
            self.report(f'{table}: no rows', {**source, table: head + tail})
            relevant[table] = []
            if not records:
                continue
            if full:
                self.report(f'{table}: rows reversed', {**source, table: head + ''.join(reversed(records)) + tail})
                self.report(f'{table}: only the first row', {**source, table: head + records[0] + tail})
            if not full and not self.own_rows(records):
                continue
            for column in columns(records[0]):
                verdict = self.report(f'{table}: without <{column}>', {**source, table: head + ''.join(drop_column(r, column) for r in records) + tail})
                if verdict != 'IDENTICAL':
                    relevant[table].append(column)
                values = [field(r, column) for r in records]
                if full and len(set(values)) > 1:
                    shifted = values[1:] + values[:1]
                    self.report(f'{table}: <{column}> shifted by a row',
                                {**source, table: head + ''.join(set_field(r, column, v or '') for r, v in zip(records, shifted)) + tail})
        print(f'columns whose removal changes or breaks the output: {relevant}', flush=True)
        self.minimal(relevant)

    def minimal(self, relevant):
        """Bare tables: the map's own rows with only the columns that matter, else one placeholder row."""
        rows = {}
        for table in self.tables:
            head, records, tail = split(table, self.source[table])
            own = self.own_rows(records)
            if not records:
                rows[table] = []
            elif own:
                keep = relevant[table] or columns(own[0])[:1]
                rows[table] = [{c: field(r, c) for c in keep} for r in own]
            else:
                rows[table] = [{(relevant[table] or columns(records[0])[:1])[0]: 'unused'}]
        print(f'minimal tables: {rows}', flush=True)
        self.report('minimal: bare tables, the map\'s rows or one placeholder', bare(rows))
        for table, table_rows in rows.items():
            for bound in ('minx', 'miny', 'maxx', 'maxy'):
                if table_rows and bound in table_rows[0]:
                    for value in ('0', '-1'):
                        changed = {t: [dict(r) for r in rs] for t, rs in rows.items()}
                        for row in changed[table]:
                            row[bound] = value
                        self.report(f'minimal, {table}.{bound} = {value}', bare(changed))


def ablate(args):
    Ablation(Game(args)).all(args.full)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=['build', 'trace', 'trace-dyn-res', 'ablate', 'string-layout'])
    parser.add_argument('--game', choices=sorted(GAMES))
    parser.add_argument('--ak', help='the game\'s assembly_kit folder')
    parser.add_argument('--map', help='campaign map name')
    parser.add_argument('--hex', help='map.hex to use when the Assembly Kit has none for the map')
    parser.add_argument('--full', action='store_true', help='ablate: also reorder rows and shift values, and drop every column of every table')
    parser.add_argument('--work', default=os.path.join(tempfile.gettempdir(), 'tooldatabuilder-probe'))
    args = parser.parse_args()

    if args.command == 'build':
        return build(args)
    if not args.game or not args.ak or (args.command != 'string-layout' and not args.map):
        parser.error('--game, --ak and (except for string-layout) --map are required')
    {'trace': trace, 'trace-dyn-res': trace_dyn_res, 'ablate': ablate, 'string-layout': string_layout}[args.command](args)


if __name__ == '__main__':
    main()
