#!/usr/bin/env python3
"""Patch project.binary to register a trainer autoload, then rebuild the pck."""
import os
import struct
import shutil
import sys

MOD = r"C:\Games\Sir.We.Have.an.Orc.Problem\mod"
UNPACKED = os.path.join(MOD, "unpacked")
PROJECT_BIN = os.path.join(UNPACKED, "project.binary")
TRAINER_SRC = os.path.join(MOD, "trainer.gd")
TRAINER_DST = os.path.join(UNPACKED, "trainer.gd")


def u32(b, o):
    return struct.unpack("<I", b[o:o + 4])[0]


def parse_records(data):
    assert data[:4] == b"ECFG", data[:4]
    count = u32(data, 4)
    o = 8
    records = []
    for _ in range(count):
        kl = u32(data, o)
        key = data[o + 4:o + 4 + kl]
        vl = u32(data, o + 4 + kl)
        value = data[o + 8 + kl:o + 8 + kl + vl]
        records.append((key, value))
        o += 8 + kl + vl
    assert o == len(data), (o, len(data))
    return records


def build_record(key: bytes, value: bytes) -> bytes:
    return struct.pack("<I", len(key)) + key + struct.pack("<I", len(value)) + value


def string_variant(text: str) -> bytes:
    """Godot binary-variant String: [type u32=4][strlen u32][chars][pad to 4]."""
    b = text.encode("utf-8")
    pad = (4 - len(b) % 4) % 4
    return struct.pack("<II", 4, len(b)) + b + b"\0" * pad


def patch(autoload_key, autoload_value):
    data = open(PROJECT_BIN, "rb").read()
    records = parse_records(data)

    # remove any existing entry with the same key
    records = [(k, v) for k, v in records if k != autoload_key]

    # insert after the last autoload/* entry
    idx = len(records)
    for i, (k, _) in enumerate(records):
        if k.startswith(b"autoload/"):
            idx = i + 1

    new_record = build_record(autoload_key, autoload_value)
    records.insert(idx, (autoload_key, autoload_value))

    out = b"ECFG" + struct.pack("<I", len(records))
    for k, v in records:
        out += build_record(k, v)

    open(PROJECT_BIN, "wb").write(out)
    print(f"patched {PROJECT_BIN}: inserted {autoload_key!r} = {autoload_value!r}, "
          f"now {len(records)} records")


GAME = r"C:\Games\Sir.We.Have.an.Orc.Problem\game"
EXE = os.path.join(GAME, "swhaop.exe")
EXE_BACKUP = os.path.join(GAME, "swhaop.exe.original")


def main():
    import pck_tool
    shutil.copyfile(TRAINER_SRC, TRAINER_DST)
    print("copied trainer.gd -> unpacked/trainer.gd")
    patch(b"autoload/Trainer", string_variant("*res://trainer.gd"))
    os.system(f'python "{os.path.join(MOD, "pck_tool.py")}" rebuild')
    pck = os.path.join(MOD, "swhaop.pck")

    # reversibility: back up the original exe once
    if not os.path.exists(EXE_BACKUP):
        shutil.copyfile(EXE, EXE_BACKUP)
        print(f"backed up {EXE} -> {EXE_BACKUP}")

    # rebuild from the ORIGINAL exe (prefix + target size come from the backup)
    pck_tool.reembed(EXE_BACKUP, pck, EXE)
    # external pck is ignored by this engine build (embedded wins); remove if present
    ext = os.path.join(GAME, "swhaop.pck")
    if os.path.exists(ext):
        os.remove(ext)
        print("removed external swhaop.pck (embedded pck wins in this engine)")


if __name__ == "__main__":
    main()
