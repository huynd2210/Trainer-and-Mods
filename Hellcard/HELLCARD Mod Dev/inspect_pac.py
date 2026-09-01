"""Read-only inspector for HELLCARD's XOR-encoded PAC archives."""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path


def decode(data: bytes) -> bytes:
    return bytes(value ^ 0xFF for value in data)


def read_u16(handle) -> int:
    return struct.unpack("<H", decode(handle.read(2)))[0]


def read_u32(handle) -> int:
    return struct.unpack("<I", decode(handle.read(4)))[0]


def entries(path: Path):
    with path.open("rb") as handle:
        count = read_u32(handle)
        for _ in range(count):
            name_length = read_u16(handle)
            name = decode(handle.read(name_length)).decode("utf-8", errors="replace")
            offset = read_u32(handle)
            size = read_u32(handle)
            yield name, offset, size


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def u8(self) -> tuple[int, int]:
        offset = self.pos
        value = self.data[self.pos]
        self.pos += 1
        return offset, value

    def u16(self) -> tuple[int, int]:
        offset = self.pos
        value = struct.unpack_from("<H", self.data, self.pos)[0]
        self.pos += 2
        return offset, value

    def u32(self) -> tuple[int, int]:
        offset = self.pos
        value = struct.unpack_from("<I", self.data, self.pos)[0]
        self.pos += 4
        return offset, value

    def f32(self) -> tuple[int, float]:
        offset = self.pos
        value = struct.unpack_from("<f", self.data, self.pos)[0]
        self.pos += 4
        return offset, value

    def string(self) -> tuple[int, str]:
        offset, length = self.u32()
        value = self.data[self.pos : self.pos + length].decode("utf-8")
        self.pos += length
        return offset, value


def parse_artifact(data: bytes) -> dict[str, tuple[int, object]]:
    reader = Reader(data)
    if reader.data[:4] != b"cUg\x01":
        raise ValueError("Not a CUG resource")
    reader.pos = 4
    result: dict[str, tuple[int, object]] = {}
    result["resource_class"] = reader.string()
    result["reserved"] = reader.u32()
    result["name"] = reader.string()
    result["payload_size"] = reader.u32()
    result["version"] = reader.u8()
    result["sprite_id"] = reader.u32()
    result["texture"] = reader.string()
    result["utf_prefix"] = reader.string()
    result["class"] = reader.string()
    result["starting_cost"] = reader.u32()
    result["unlock_exp"] = reader.u32()
    result["weight"] = reader.f32()
    result["min_torment"] = reader.u16()
    result["max_torment"] = reader.u16()
    result["min_floor"] = reader.u16()
    result["max_floor"] = reader.u16()
    result["type"] = reader.u32()
    result["temporary"] = reader.u8()
    result["visible"] = reader.u8()
    result["unlock_condition"] = reader.string()
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path)
    parser.add_argument("--contains", default="")
    parser.add_argument("--dump", help="Decode one exact archive path to stdout.")
    parser.add_argument("--hex", help="Print one exact decoded entry as a hex dump.")
    parser.add_argument(
        "--parse-artifact", help="Parse the common fields of one artifact entry."
    )
    args = parser.parse_args()

    exact_name = args.dump or args.hex or args.parse_artifact
    if exact_name:
        target = exact_name.casefold()
        with args.archive.open("rb") as handle:
            for name, offset, size in entries(args.archive):
                if name.casefold() == target:
                    handle.seek(offset)
                    data = decode(handle.read(size))
                    if args.parse_artifact:
                        for field, (offset, value) in parse_artifact(data).items():
                            print(f"{offset:04X} {field:18} {value!r}")
                    elif args.dump:
                        sys.stdout.buffer.write(data)
                    else:
                        for index in range(0, len(data), 16):
                            chunk = data[index : index + 16]
                            values = " ".join(f"{value:02X}" for value in chunk)
                            ascii_text = "".join(
                                chr(value) if 32 <= value < 127 else "."
                                for value in chunk
                            )
                            print(f"{index:08X}  {values:<47}  {ascii_text}")
                    return
        raise SystemExit(f"Entry not found: {exact_name}")

    needle = args.contains.casefold()
    for name, offset, size in entries(args.archive):
        if needle in name.casefold():
            print(f"{offset:10d} {size:8d} {name}")


if __name__ == "__main__":
    main()
