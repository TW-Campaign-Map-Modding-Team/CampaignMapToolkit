"""Dumps an ABCA or ABCB .esf file (map_data.esf and friends) as an indented tree that keeps every
node's exact type code, so two files can be compared node by node.

    python esf_dump.py <file.esf> [--max-entries N] [--max-items N] [--path PREFIX]
    python esf_dump.py <a.esf> --compare <b.esf>

--max-entries limits how many entries of each record block are printed (default 3), --max-items
how many items of each array (default 8). --path prints only nodes whose path starts with PREFIX.
--compare walks both files in step and prints the first nodes that differ, or says whether only
the string table order differs (the games list strings in hash-map order, which writers may not
reproduce).
"""
import argparse
import hashlib
import struct
import sys

TYPE_NAMES = {
    0x01: 'bool', 0x02: 'i8', 0x03: 'i16', 0x04: 'i32', 0x05: 'i64', 0x06: 'u8', 0x07: 'u16', 0x08: 'u32',
    0x09: 'u64', 0x0a: 'f32', 0x0b: 'f64', 0x0c: 'coord2d', 0x0d: 'coord3d', 0x0e: 'utf16', 0x0f: 'ascii',
    0x10: 'angle', 0x12: 'true', 0x13: 'false', 0x14: 'u32_0', 0x15: 'u32_1', 0x16: 'u32_b', 0x17: 'u32_s',
    0x18: 'u32_24', 0x19: 'i32_0', 0x1a: 'i32_b', 0x1b: 'i32_s', 0x1c: 'i32_24', 0x1d: 'f32_0',
}
# Fixed-size value payloads, by type code.
VALUE_FORMATS = {
    0x01: '<?', 0x02: '<b', 0x03: '<h', 0x04: '<i', 0x05: '<q', 0x06: '<B', 0x07: '<H', 0x08: '<I', 0x09: '<Q',
    0x0a: '<f', 0x0b: '<d', 0x0c: '<ff', 0x0d: '<fff', 0x10: '<H',
}
# Array element formats, by array type code (0x40 + value type code). None: raw width, unsigned.
ARRAY_FORMATS = {
    0x41: ('<?', 1), 0x42: ('<b', 1), 0x43: ('<h', 2), 0x44: ('<i', 4), 0x45: ('<q', 8), 0x46: ('<B', 1),
    0x47: ('<H', 2), 0x48: ('<I', 4), 0x49: ('<Q', 8), 0x4a: ('<f', 4), 0x4b: ('<d', 8), 0x4c: ('<ff', 8),
    0x4d: ('<fff', 12), 0x4e: ('<i', 4), 0x4f: ('<i', 4), 0x50: ('<H', 2),
    0x56: (None, 1), 0x57: (None, 2), 0x58: (None, 3), 0x5a: ('<b', 1), 0x5b: ('<h', 2),
}


class Node:
    __slots__ = ('kind', 'type', 'name', 'version', 'children', 'value', 'start', 'end')

    def __init__(self, kind, type_code, start):
        self.kind, self.type, self.start = kind, type_code, start
        self.name, self.version, self.children, self.value, self.end = None, None, None, None, None


class Esf:
    def __init__(self, data):
        self.data = data
        magic, = struct.unpack_from('<I', data, 0)
        if magic not in (0xABCA, 0xABCB):
            sys.exit(f'magic {magic:#x} is not ABCA or ABCB')
        self.wide_strings = magic == 0xABCB
        self.magic = magic
        names_offset, = struct.unpack_from('<I', data, 12)
        self.read_tables(names_offset)
        self.names_offset = names_offset
        self.root = self.read_node(16, root=True)[0]

    def read_tables(self, pos):
        count, = struct.unpack_from('<H', self.data, pos)
        pos += 2
        self.names = []
        for _ in range(count):
            n, = struct.unpack_from('<H', self.data, pos)
            self.names.append(self.data[pos + 2:pos + 2 + n].decode('ascii'))
            pos += 2 + n
        self.utf16, pos = self.read_strings(pos, 2, 'utf-16-le')
        self.ascii, pos = self.read_strings(pos, 1, 'ascii')

    def read_strings(self, pos, width, encoding):
        count, = struct.unpack_from('<i', self.data, pos)
        pos += 4
        table = {}
        for _ in range(count):
            if self.wide_strings:
                n, = struct.unpack_from('<i', self.data, pos)
                pos += 4
            else:
                n, = struct.unpack_from('<H', self.data, pos)
                pos += 2
            text = self.data[pos:pos + n * width].decode(encoding)
            pos += n * width
            ident, = struct.unpack_from('<i', self.data, pos)
            pos += 4
            table[ident] = text
        return table, pos

    def varint(self, pos):
        result = 0
        while True:
            b = self.data[pos]
            pos += 1
            result = (result << 7) | (b & 0x7f)
            if not b & 0x80:
                return result, pos

    def read_node(self, pos, root=False):
        start = pos
        code = self.data[pos]
        pos += 1
        if code & 0x80:
            node = Node('record', code, start)
            if root or code & 0x20:
                name_index, version = struct.unpack_from('<HB', self.data, pos)
                pos += 3
            else:
                version = (code & 0x1f) >> 1
                name_index = ((code & 1) << 8) | self.data[pos]
                pos += 1
            node.name, node.version = self.names[name_index], version
            size, pos = self.varint(pos)
            if code & 0x40 and not root:
                node.kind = 'block'
                count, pos = self.varint(pos)
                node.children = []
                for _ in range(count):
                    entry_size, pos = self.varint(pos)
                    entry = Node('entry', None, pos)
                    entry.children = self.read_children(pos, pos + entry_size)
                    pos += entry_size
                    entry.end = pos
                    node.children.append(entry)
            else:
                node.children = self.read_children(pos, pos + size)
                pos += size
            node.end = pos
            return node, pos
        node = Node('value', code, start)
        if code in VALUE_FORMATS:
            fmt = VALUE_FORMATS[code]
            v = struct.unpack_from(fmt, self.data, pos)
            node.value = v if len(v) > 1 else v[0]
            pos += struct.calcsize(fmt)
        elif code in (0x0e, 0x0f):
            ident, = struct.unpack_from('<i', self.data, pos)
            node.value = (self.utf16 if code == 0x0e else self.ascii).get(ident, f'<missing {ident}>')
            pos += 4
        elif code in (0x12, 0x13, 0x14, 0x15, 0x19, 0x1d):
            node.value = {0x12: True, 0x13: False, 0x14: 0, 0x15: 1, 0x19: 0, 0x1d: 0.0}[code]
        elif code in (0x16, 0x1a):
            node.value = struct.unpack_from('<B' if code == 0x16 else '<b', self.data, pos)[0]
            pos += 1
        elif code in (0x17, 0x1b):
            node.value = struct.unpack_from('<H' if code == 0x17 else '<h', self.data, pos)[0]
            pos += 2
        elif code in (0x18, 0x1c):
            node.value = int.from_bytes(self.data[pos:pos + 3], 'big', signed=code == 0x1c)
            pos += 3
        elif code in ARRAY_FORMATS:
            size, pos = self.varint(pos)
            node.kind = 'array'
            node.value = (pos, size)
            pos += size
        else:
            raise ValueError(f'type code {code:#x} at {start:#x}')
        node.end = pos
        return node, pos

    def read_children(self, pos, end):
        children = []
        while pos < end:
            child, pos = self.read_node(pos)
            children.append(child)
        return children

    def array_items(self, node):
        pos, size = node.value
        fmt, width = ARRAY_FORMATS[node.type]
        raw = self.data[pos:pos + size]
        if fmt is None:
            items = [int.from_bytes(raw[i:i + width], 'little') for i in range(0, size, width)]
        else:
            items = [v if len(v) > 1 else v[0] for v in struct.iter_unpack(fmt, raw)]
        if node.type in (0x4e, 0x4f):
            table = self.utf16 if node.type == 0x4e else self.ascii
            items = [table.get(i, f'<missing {i}>') for i in items]
        return items


def type_label(node):
    if node.kind in ('record', 'block'):
        return f'{node.type:#04x}'
    if node.kind == 'array':
        base = TYPE_NAMES.get(node.type - 0x40, f'{node.type - 0x40:#x}')
        return f'{base}[] {node.type:#04x}'
    return f'{TYPE_NAMES.get(node.type, hex(node.type))} {node.type:#04x}'


def describe(esf, node, args):
    if node.kind == 'array':
        pos, size = node.value
        items = esf.array_items(node)
        digest = hashlib.md5(esf.data[pos:pos + size]).hexdigest()[:8]
        shown = ', '.join(repr(i) for i in items[:args.max_items])
        more = ', ...' if len(items) > args.max_items else ''
        return f'{type_label(node)} n={len(items)} bytes={size} md5={digest} [{shown}{more}]'
    return f'{type_label(node)} = {node.value!r}'


def dump(esf, node, args, path='', depth=0, index=None):
    pad = '  ' * depth
    if node.kind == 'entry':
        here = f'{path}[{index}]'
        if here.startswith(args.path) or args.path.startswith(here):
            print(f'{pad}[{index}] size={node.end - node.start}')
        for i, child in enumerate(node.children):
            dump(esf, child, args, here, depth + 1, i)
        return
    if node.kind in ('record', 'block'):
        here = f'{path}/{node.name}'
        if here.startswith(args.path) or args.path.startswith(here):
            extra = f' count={len(node.children)}' if node.kind == 'block' else ''
            print(f'{pad}{node.name} v{node.version} {node.kind} {type_label(node)}{extra}')
        entries = node.children
        if node.kind == 'block' and len(entries) > args.max_entries:
            entries = entries[:args.max_entries]
        for i, child in enumerate(entries):
            dump(esf, child, args, here, depth + 1, i)
        if node.kind == 'block' and len(node.children) > len(entries):
            if here.startswith(args.path):
                print(f'{pad}  ... {len(node.children) - len(entries)} more entries')
        return
    here = f'{path}.{index}'
    if here.startswith(args.path):
        print(f'{pad}{index}: {describe(esf, node, args)}')


def compare(a, b, node_a, node_b, path, out, limit=20):
    if len(out) >= limit:
        return
    if node_a.kind != node_b.kind or node_a.type != node_b.type or node_a.name != node_b.name or node_a.version != node_b.version:
        out.append(f'{path}: {node_a.kind} {node_a.name} {type_label(node_a)} v{node_a.version} != {node_b.kind} {node_b.name} {type_label(node_b)} v{node_b.version}')
        return
    if node_a.children is not None:
        if len(node_a.children) != len(node_b.children):
            out.append(f'{path}/{node_a.name or ""}: {len(node_a.children)} children != {len(node_b.children)}')
        for i, (ca, cb) in enumerate(zip(node_a.children, node_b.children)):
            compare(a, b, ca, cb, f'{path}/{node_a.name or ""}[{i}]', out, limit)
        return
    if node_a.kind == 'array':
        pa, sa = node_a.value
        pb, sb = node_b.value
        if a.data[pa:pa + sa] != b.data[pb:pb + sb]:
            ia, ib = a.array_items(node_a), b.array_items(node_b)
            first = next((i for i, (x, y) in enumerate(zip(ia, ib)) if x != y), min(len(ia), len(ib)))
            ndiff = sum(1 for x, y in zip(ia, ib) if x != y)
            out.append(f'{path}: array n={len(ia)} vs {len(ib)}, {ndiff} items differ, first at {first}: '
                       f'{ia[first:first + 4]} vs {ib[first:first + 4]}')
    elif node_a.value != node_b.value:
        out.append(f'{path}: {node_a.value!r} != {node_b.value!r}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('file')
    parser.add_argument('--compare')
    parser.add_argument('--max-entries', type=int, default=3)
    parser.add_argument('--max-items', type=int, default=8)
    parser.add_argument('--path', default='')
    parser.add_argument('--limit', type=int, default=20)
    args = parser.parse_args()
    a = Esf(open(args.file, 'rb').read())
    if args.compare:
        b = Esf(open(args.compare, 'rb').read())
        if a.data[:8] + a.data[12:] == b.data[:8] + b.data[12:]:
            print('IDENTICAL (ignoring the timestamp at bytes 8-11)')
            return
        out = []
        compare(a, b, a.root, b.root, '', out, args.limit)
        if a.names != b.names:
            out.append(f'node names differ: {a.names} vs {b.names}')
        if a.ascii != b.ascii or a.utf16 != b.utf16:
            out.append('string tables differ')
        if out:
            print('\n'.join(out))
        elif a.data[:8] + a.data[12:a.names_offset] == b.data[:8] + b.data[12:b.names_offset]:
            print('IDENTICAL NODES (ignoring the timestamp); the string tables hold the same strings and ids in a different order')
        else:
            print('trees equal; the node bytes differ in encoding (type codes or size padding)')
        return
    print(f'magic {a.magic:#x}, {len(a.names)} node names, {len(a.utf16)} utf16 strings, {len(a.ascii)} ascii strings')
    dump(a, a.root, args)


if __name__ == '__main__':
    main()
