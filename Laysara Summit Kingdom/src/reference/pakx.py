"""List or extract files from Laysara's AS-WindowsNoEditor.pak (pak v11, UE 4.27).

The index is unencrypted and entries are zlib-compressed, so no key or oodle is
needed.

    python pakx.py list filelist.txt
    python pakx.py extract <outdir> <substring> [<substring> ...]

e.g. python pakx.py extract ext UI/Tooltips/BP_ASMoneyTooltip
"""
import os
import struct
import sys
import zlib

PAK = r"C:\Program Files (x86)\Steam\steamapps\common\Laysara Summit Kingdom\AS\Content\Paks\AS-WindowsNoEditor.pak"


def fstr(buf, o):
    n, = struct.unpack_from('<i', buf, o); o += 4
    if n < 0:
        s = buf[o:o - n * 2].decode('utf-16-le'); o += -n * 2
    else:
        s = buf[o:o + n].decode('utf-8', 'replace'); o += n
    return s.rstrip('\0'), o


def read_index(f):
    # FPakInfo footer: ... magic 0x5A6F12E1, version, index offset, index size
    f.seek(0, 2); size = f.tell()
    f.seek(size - 512); tail = f.read(512)
    k = tail.rfind(struct.pack('<I', 0x5A6F12E1))
    version, idx_off, idx_size = struct.unpack_from('<iqq', tail, k + 4)
    assert version == 11 and tail[k - 1] == 0, "expected an unencrypted v11 pak"
    f.seek(idx_off); d = f.read(idx_size)

    o = 0
    mount, o = fstr(d, o)
    o += 4 + 8                                   # entry count, path hash seed
    has_path_hash, = struct.unpack_from('<I', d, o); o += 4
    if has_path_hash: o += 8 + 8 + 20
    o += 4                                       # has full directory index
    fdi_off, fdi_size = struct.unpack_from('<qq', d, o); o += 16 + 20
    enc_size, = struct.unpack_from('<i', d, o); o += 4
    encoded = d[o:o + enc_size]

    f.seek(fdi_off); di = f.read(fdi_size); p = 0
    files = {}
    ndirs, = struct.unpack_from('<i', di, p); p += 4
    for _ in range(ndirs):
        dname, p = fstr(di, p)
        nfiles, = struct.unpack_from('<i', di, p); p += 4
        for _ in range(nfiles):
            fname, p = fstr(di, p)
            e, = struct.unpack_from('<i', di, p); p += 4
            files[(mount + dname + fname).replace('../../../', '')] = e
    return files, encoded


def decode_entry(enc, eo):
    """FPakEntry bit-packed encoding (FPakFile::DecodePakEntry)."""
    v, = struct.unpack_from('<I', enc, eo); eo += 4
    block_size = v & 0x3f
    if block_size == 0x3f:
        block_size, = struct.unpack_from('<I', enc, eo); eo += 4
    nblocks = (v >> 6) & 0xffff
    encrypted = (v >> 22) & 1
    comp = (v >> 23) & 0x3f

    def rd(is32):
        nonlocal eo
        fmt, n = ('<I', 4) if is32 else ('<Q', 8)
        x, = struct.unpack_from(fmt, enc, eo); eo += n
        return x

    offset = rd(v >> 31 & 1)
    usize = rd(v >> 30 & 1)
    size = rd(v >> 29 & 1) if comp else usize
    assert not encrypted
    # the entry header serialized in front of the data
    header = 8 + 8 + 8 + 4 + 20 + (4 + 16 * nblocks if comp else 0) + 1 + 4
    blocks = []
    if comp:
        if nblocks == 1:
            blocks = [(header, header + size)]
        else:
            s = header
            for _ in range(nblocks):
                bs, = struct.unpack_from('<I', enc, eo); eo += 4
                blocks.append((s, s + bs)); s += bs
    return offset, usize, comp, blocks, header


def main():
    f = open(PAK, 'rb')
    files, encoded = read_index(f)
    if sys.argv[1] == 'list':
        with open(sys.argv[2], 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(sorted(files)))
        print(len(files), 'files')
        return
    outdir, patterns = sys.argv[2], [p.lower() for p in sys.argv[3:]]
    for name, e in sorted(files.items()):
        if not any(p in name.lower() for p in patterns):
            continue
        offset, usize, comp, blocks, header = decode_entry(encoded, e)
        if comp:
            data = b''
            for a, b in blocks:
                f.seek(offset + a); data += zlib.decompress(f.read(b - a))
        else:
            f.seek(offset + header); data = f.read(usize)
        assert len(data) == usize, (name, len(data), usize)
        dst = os.path.join(outdir, name)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        open(dst, 'wb').write(data)
        print('ok', name, usize)


if __name__ == '__main__':
    main()
