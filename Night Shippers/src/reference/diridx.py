import struct

P = r"C:\Program Files (x86)\Steam\steamapps\common\Night Shippers\ProjectSH\Content\Paks\pakchunk0-Windows.utoc"
NONE = 0xFFFFFFFF

def get_dirindex(p):
    d = open(p, 'rb').read()
    hdrsize, entrycount, cbcount, cbsize, cmcount, cmlen, cblocksize, diridxsize, partcount = struct.unpack_from('<9I', d, 20)
    off = 20 + 36 + 8 + 16
    flags = d[off]
    off += 4
    ph_seeds, ph_without = struct.unpack_from('<ii', d, off)
    o = hdrsize + entrycount * 12 + entrycount * 10
    if ph_seeds > 0:
        o += ph_seeds * 4
    if ph_without > 0:
        o += ph_without * 4
    o += cbcount * 12 + cmcount * cmlen
    if flags & 0x04:
        sigsize, = struct.unpack_from('<I', d, o)
        o += 4 + sigsize * 2 + 20
    return d[o:o + diridxsize]

di = get_dirindex(P)
off = 0
mplen, = struct.unpack_from('<I', di, off); off += 4
mount = di[off:off + mplen].split(b'\0')[0].decode(); off += mplen
ndirs, = struct.unpack_from('<I', di, off); off += 4
dirs = [struct.unpack_from('<4I', di, off + i * 16) for i in range(ndirs)]; off += ndirs * 16
nfiles, = struct.unpack_from('<I', di, off); off += 4
files = [struct.unpack_from('<3I', di, off + i * 12) for i in range(nfiles)]; off += nfiles * 12
nstr, = struct.unpack_from('<I', di, off); off += 4
print("mount=%r ndirs=%d nfiles=%d nstr=%d stroff=%d" % (mount, ndirs, nfiles, nstr, off))
print("first str bytes:", di[off:off + 48].hex())
strs = []
for i in range(nstr):
    l, = struct.unpack_from('<i', di, off); off += 4
    if l < 0:
        n = -l * 2
        strs.append(di[off:off + n].decode('utf-16-le', 'replace').split('\0')[0]); off += n
    else:
        strs.append(di[off:off + l].decode('utf-8', 'replace').split('\0')[0]); off += l
print("strings parsed:", len(strs), strs[:5])

out = []
def rec(diridx, path):
    while diridx != NONE:
        name_i, first_child, next_sib, first_file = dirs[diridx]
        newpath = path if name_i == NONE else path + strs[name_i] + "/"
        f = first_file
        while f != NONE:
            ni, nxt, ui = files[f]
            out.append(newpath + strs[ni])
            f = nxt
        if first_child != NONE:
            rec(first_child, newpath)
        diridx = next_sib

rec(0, mount)
print("total files:", len(out))
with open("filelist.txt", "w", encoding="utf-8") as fh:
    for p in sorted(out):
        fh.write(p + "\n")
