#!/usr/bin/env python3
"""Godot 4 PCK split/extract/rebuild tool for swhaop.exe modding.

Handles the Godot 4.x pck format version 3 (used by Godot 4.6.x):
  header: magic(4) ver(4) major(4) minor(4) patch(4) flags(4)
          file_base(8) dir_offset(8)
  then data area at pck_start + file_base
  directory table at pck_start + dir_offset: file_count(4), then entries:
    path_len(4) path offset(8) size(8) md5(16) flags(4)
  file data at pck_start + file_base + ofs.
"""
import os
import struct
import sys
import hashlib

PCK_MAGIC = b"GDPC"
PCK_VERSION = 3


class PckError(Exception):
    pass


def locate_pck(exe_path):
    """Return (pck_start, pck_size) for the embedded pck in a Godot exe."""
    with open(exe_path, "rb") as f:
        f.seek(-12, 2)
        trailer = f.read(12)
    # trailer: [pck_size-12 as int64][GDPC]
    if trailer[8:12] != PCK_MAGIC:
        raise PckError("no embedded pck trailer found")
    ds = struct.unpack("<Q", trailer[0:8])[0]
    n = os.path.getsize(exe_path)
    pck_start = n - 12 - ds
    return pck_start, n - 12 - pck_start


def read_header(f):
    magic = f.read(4)
    if magic != PCK_MAGIC:
        raise PckError(f"bad magic {magic!r}")
    fmt = struct.unpack("<I", f.read(4))[0]
    ver = struct.unpack("<III", f.read(12))
    flags = struct.unpack("<I", f.read(4))[0]
    file_base = struct.unpack("<Q", f.read(8))[0]
    dir_offset = struct.unpack("<Q", f.read(8))[0]
    return {
        "format": fmt,
        "godot_version": ver,
        "flags": flags,
        "file_base": file_base,
        "dir_offset": dir_offset,
    }


def read_dir(f, base):
    file_count = struct.unpack("<I", f.read(4))[0]
    files = []
    for _ in range(file_count):
        plen = struct.unpack("<I", f.read(4))[0]
        path = f.read(plen)
        # Godot's String::utf8 stops at the first null; repackers pad with nulls
        path = path.split(b"\0")[0]
        try:
            path = path.decode("utf-8")
        except UnicodeDecodeError:
            path = path.decode("latin1")
        offset, size = struct.unpack("<QQ", f.read(16))
        md5 = f.read(16)
        flags = struct.unpack("<I", f.read(4))[0]
        files.append({
            "path": path,
            "ofs": offset,
            "size": size,
            "md5": md5,
            "flags": flags,
            "abs_offset": base + offset,
        })
    return files


def open_pck(pck_path):
    f = open(pck_path, "rb")
    hdr = read_header(f)
    f.seek(hdr["dir_offset"])
    files = read_dir(f, hdr["file_base"])
    return f, hdr, files


def split(exe_path, out_pck):
    start, size = locate_pck(exe_path)
    with open(exe_path, "rb") as f:
        f.seek(start)
        data = f.read(size)
    with open(out_pck, "wb") as f:
        f.write(data)
    return start, size


def list_files(pck_path):
    f, hdr, files = open_pck(pck_path)
    f.close()
    return hdr, files


def extract(pck_path, out_dir):
    f, hdr, files = open_pck(pck_path)
    os.makedirs(out_dir, exist_ok=True)
    for e in files:
        f.seek(e["abs_offset"])
        data = f.read(e["size"])
        dst = os.path.join(out_dir, e["path"])
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        with open(dst, "wb") as out:
            out.write(data)
    f.close()
    return hdr, files


def rebuild(pck_path, src_dir, godot_version=(4, 6, 4), pack_flags=2):
    """Rebuild a Godot 4 V3 pck from an extracted directory tree.
    Layout: header + file_base padding + data + dir table at dir_offset.
    """
    files = []
    for root, dirs, names in os.walk(src_dir):
        dirs.sort()
        for name in sorted(names):
            full = os.path.join(root, name)
            rel = os.path.relpath(full, src_dir).replace("\\", "/")
            files.append((rel, full))
    files.sort(key=lambda x: x[0])

    file_base = 112  # keep original convention (header size + slack)
    header_size = 4 + 4 + 12 + 4 + 8 + 8  # 40 bytes
    data_start = file_base
    # table size guess
    table_size = 4 + sum(
        4 + len(p.encode("utf-8")) + 16 + 16 + 4 for p, _ in files
    )
    dir_offset = data_start + sum(
        os.path.getsize(full) for _, full in files
    )
    # pad so table starts on 16-byte alignment (mirror original behavior)
    dir_offset = ((dir_offset + 15) // 16) * 16

    with open(pck_path, "wb") as f:
        # --- header ---
        f.write(PCK_MAGIC)
        f.write(struct.pack("<I", PCK_VERSION))
        f.write(struct.pack("<III", *godot_version))
        f.write(struct.pack("<I", pack_flags))
        f.write(struct.pack("<Q", file_base))
        f.write(struct.pack("<Q", dir_offset))
        # pad header region to file_base
        pos = f.tell()
        if pos < file_base:
            f.write(b"\0" * (file_base - pos))

        # --- data ---
        entries = []  # (rel, ofs, size, md5)
        for rel, full in files:
            with open(full, "rb") as df:
                data = df.read()
            entries.append((rel, f.tell() - file_base, len(data), hashlib.md5(data).digest()))
            f.write(data)
        # pad to dir_offset
        pos = f.tell()
        if pos < dir_offset:
            f.write(b"\0" * (dir_offset - pos))

        # --- directory table ---
        f.write(struct.pack("<I", len(entries)))
        for rel, ofs, size, digest in entries:
            pb = rel.encode("utf-8")
            f.write(struct.pack("<I", len(pb)))
            f.write(pb)
            f.write(struct.pack("<QQ", ofs, size))
            f.write(digest)
            f.write(struct.pack("<I", 0))  # flags


def reembed(exe_path, pck_path, out_exe):
    """Rebuild a Godot exe with a modified pck embedded (trailer: [pck_size][GDPC]).

    The repacked exe's last PE section is sized to the original full file length,
    so we pad the pck so the total file size matches the original -- otherwise
    Windows rejects the exe as truncated ("not a valid application").
    """
    start, _size = locate_pck(exe_path)
    n_orig = os.path.getsize(exe_path)
    with open(exe_path, "rb") as f:
        prefix = f.read(start)
    with open(pck_path, "rb") as f:
        pck_data = f.read()
    n_target = max(n_orig, start + len(pck_data) + 12)
    pad = n_target - 12 - start - len(pck_data)
    if pad > 0:
        pck_data += b"\0" * pad
    with open(out_exe, "wb") as f:
        f.write(prefix)
        f.write(pck_data)
        f.write(struct.pack("<Q", len(pck_data)))
        f.write(PCK_MAGIC)
    print(f"re-embedded pck ({len(pck_data)} bytes, {pad} pad) into {out_exe}")


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "help"
    game = r"C:\Games\Sir.We.Have.an.Orc.Problem\game"
    mod = r"C:\Games\Sir.We.Have.an.Orc.Problem\mod"
    exe = os.path.join(game, "swhaop.exe")
    pck = os.path.join(mod, "swhaop_extracted.pck")
    extract_dir = os.path.join(mod, "unpacked")
    rebuild_path = os.path.join(mod, "swhaop.pck")

    if cmd == "split":
        start, size = split(exe, pck)
        print(f"embedded pck at exe offset {start}, size {size}, split to {pck}")
        hdr, files = list_files(pck)
        print(f"format={hdr['format']} godot={hdr['godot_version']} flags={hdr['flags']} "
              f"file_base={hdr['file_base']} dir_offset={hdr['dir_offset']} files={len(files)}")
    elif cmd == "list":
        hdr, files = list_files(pck)
        print(f"format={hdr['format']} godot={hdr['godot_version']} flags={hdr['flags']} "
              f"file_base={hdr['file_base']} dir_offset={hdr['dir_offset']} files={len(files)}")
        for e in files:
            print(f"{e['size']:>10}  {e['path']}")
    elif cmd == "extract":
        hdr, files = extract(pck, extract_dir)
        print(f"extracted {len(files)} files to {extract_dir}")
        print(f"format={hdr['format']} godot={hdr['godot_version']} flags={hdr['flags']}")
    elif cmd == "rebuild":
        rebuild(rebuild_path, extract_dir)
        print(f"rebuilt {rebuild_path} ({os.path.getsize(rebuild_path)} bytes)")
    else:
        print("usage: pck_tool.py split|list|extract|rebuild")


if __name__ == "__main__":
    main()
