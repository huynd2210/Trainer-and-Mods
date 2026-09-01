"""Build a local HELLCARD mod using only supported CUG asset overrides."""

from __future__ import annotations

import struct
from pathlib import Path


GAME_ROOT = Path(__file__).resolve().parent.parent
ARCHIVE = GAME_ROOT / "ccg.pac"
DEV_MOD_ROOT = GAME_ROOT / "ccg_mod"
RUNTIME_ROOT = GAME_ROOT / "ccg"

START_HP = 9000
TURN_MANA = 1000

CHARACTERS = ("warrior", "rogue", "mage", "tinkerer", "bruja")
CRYSTAL_ARTIFACT = r"artifacts\glowing_crystal_self.cug"
CRYSTAL_INFLUENCE = r"influences\glowing_crystal_self.cug"


def xor_decode(data: bytes) -> bytes:
    return bytes(value ^ 0xFF for value in data)


def read_archive_entries(path: Path) -> dict[str, tuple[int, int]]:
    result: dict[str, tuple[int, int]] = {}
    with path.open("rb") as handle:
        count = struct.unpack("<I", xor_decode(handle.read(4)))[0]
        for _ in range(count):
            name_length = struct.unpack("<H", xor_decode(handle.read(2)))[0]
            name = xor_decode(handle.read(name_length)).decode("utf-8")
            offset = struct.unpack("<I", xor_decode(handle.read(4)))[0]
            size = struct.unpack("<I", xor_decode(handle.read(4)))[0]
            result[name.casefold()] = (offset, size)
    return result


def extract_decoded(
    archive: Path, table: dict[str, tuple[int, int]], entry_name: str
) -> bytearray:
    try:
        offset, size = table[entry_name.casefold()]
    except KeyError as error:
        raise RuntimeError(f"Missing archive entry: {entry_name}") from error
    with archive.open("rb") as handle:
        handle.seek(offset)
        return bytearray(xor_decode(handle.read(size)))


class Reader:
    def __init__(self, data: bytearray):
        self.data = data
        self.pos = 0

    def expect_magic(self) -> None:
        if self.data[:4] != b"cUg\x01":
            raise RuntimeError("Unexpected CUG header")
        self.pos = 4

    def u8(self) -> tuple[int, int]:
        offset = self.pos
        value = self.data[offset]
        self.pos += 1
        return offset, value

    def u32(self) -> tuple[int, int]:
        offset = self.pos
        value = struct.unpack_from("<I", self.data, offset)[0]
        self.pos += 4
        return offset, value

    def string(self) -> tuple[int, str]:
        _, length = self.u32()
        offset = self.pos
        value = bytes(self.data[offset : offset + length]).decode("utf-8")
        self.pos += length
        return offset, value


def parse_resource_header(reader: Reader, expected_class: str) -> str:
    reader.expect_magic()
    _, resource_class = reader.string()
    if resource_class != expected_class:
        raise RuntimeError(
            f"Expected resource class {expected_class}, got {resource_class}"
        )
    _, reserved = reader.u32()
    if reserved != 0:
        raise RuntimeError(f"Unexpected reserved value: {reserved}")
    _, name = reader.string()
    reader.u32()  # Payload size.
    return name


def patch_character(data: bytearray, expected_name: str) -> None:
    reader = Reader(data)
    resource_name = parse_resource_header(reader, "BCCGCharacterClass")
    if resource_name != expected_name:
        raise RuntimeError(
            f"Expected character {expected_name}, got {resource_name}"
        )
    _, version = reader.u8()
    if version != 3:
        raise RuntimeError(f"Unsupported character CUG version: {version}")
    _, utf_prefix = reader.string()
    if utf_prefix != expected_name:
        raise RuntimeError(
            f"Unexpected UTF prefix for {expected_name}: {utf_prefix}"
        )
    hp_offset, old_hp = reader.u32()
    if old_hp != 30:
        raise RuntimeError(
            f"Expected {expected_name} base HP to be 30, got {old_hp}"
        )
    struct.pack_into("<I", data, hp_offset, START_HP)


def patch_crystal_artifact(data: bytearray) -> None:
    reader = Reader(data)
    resource_name = parse_resource_header(reader, "BCCGArtifactClass")
    if resource_name != "glowing_crystal_self":
        raise RuntimeError(f"Unexpected artifact name: {resource_name}")
    _, version = reader.u8()
    if version != 6:
        raise RuntimeError(f"Unsupported artifact CUG version: {version}")
    reader.u32()  # Sprite ID.
    reader.string()  # Texture override.
    _, utf_prefix = reader.string()
    reader.string()  # Character class (empty means classless).
    if utf_prefix != "glowing_crystal_self":
        raise RuntimeError(f"Unexpected artifact UTF prefix: {utf_prefix}")

    starting_cost_offset, old_starting_cost = reader.u32()
    reader.u32()  # Unlock experience.
    reader.u32()  # Weight as raw float bits.
    reader.pos += 8  # Torment and floor ranges (four uint16 values).
    _, artifact_type = reader.u32()
    temporary_offset, old_temporary = reader.u8()
    _, visible = reader.u8()

    if old_starting_cost != 4:
        raise RuntimeError(
            f"Expected crystal starting cost 4, got {old_starting_cost}"
        )
    if artifact_type != 0 or old_temporary != 1 or visible != 1:
        raise RuntimeError(
            "Crystal is not the expected visible, temporary Starter artifact"
        )

    struct.pack_into("<I", data, starting_cost_offset, 0)
    data[temporary_offset] = 0

    behavior_marker = (
        struct.pack("<I", len("BCCGInfluencePushBehaviour"))
        + b"BCCGInfluencePushBehaviour"
    )
    behavior_offset = data.find(behavior_marker)
    if behavior_offset < 0 or data.find(behavior_marker, behavior_offset + 1) >= 0:
        raise RuntimeError("Expected exactly one influence-push behavior")

    behavior_reader = Reader(data)
    behavior_reader.pos = behavior_offset
    _, behavior_class = behavior_reader.string()
    _, behavior_id = behavior_reader.u32()
    _, behavior_name = behavior_reader.string()
    _, payload_size = behavior_reader.u32()
    payload_offset = behavior_reader.pos
    pushed_counter_offset = payload_offset + 3
    pushed_counter = struct.unpack_from("<I", data, pushed_counter_offset)[0]
    payload = bytes(data[payload_offset : payload_offset + payload_size])

    if (
        behavior_class != "BCCGInfluencePushBehaviour"
        or behavior_id != 2
        or behavior_name != "add_influence"
        or payload_size != 39
        or payload[0] != 1
        or pushed_counter != 1
        or b"glowing_crystal_self" not in payload
    ):
        raise RuntimeError("Unexpected crystal influence-push behavior layout")

    struct.pack_into("<I", data, pushed_counter_offset, TURN_MANA)


def patch_crystal_influence(data: bytearray) -> None:
    reader = Reader(data)
    resource_name = parse_resource_header(reader, "BCCGInfluenceClass")
    if resource_name != "glowing_crystal_self":
        raise RuntimeError(f"Unexpected influence name: {resource_name}")
    _, version = reader.u8()
    if version != 1:
        raise RuntimeError(f"Unsupported influence CUG version: {version}")
    reader.string()  # UTF prefix.
    reader.string()  # Tags.
    _, influence_class = reader.string()
    reader.string()  # Texture override.
    reader.u32()  # Sprite ID.
    counter_offset, old_counter = reader.u32()

    if influence_class != "BCCGAddManaInfluence":
        raise RuntimeError(f"Unexpected influence class: {influence_class}")
    if old_counter != 1:
        raise RuntimeError(f"Expected crystal mana counter 1, got {old_counter}")

    struct.pack_into("<I", data, counter_offset, TURN_MANA)


def write_resource(relative_path: str, data: bytearray) -> list[Path]:
    outputs: list[Path] = []
    normalized = Path(relative_path.replace("\\", "/"))
    for root in (DEV_MOD_ROOT, RUNTIME_ROOT):
        output = root / normalized
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(data)
        outputs.append(output)
    return outputs


def main() -> None:
    if not ARCHIVE.is_file():
        raise SystemExit(f"Archive not found: {ARCHIVE}")

    table = read_archive_entries(ARCHIVE)
    outputs: list[Path] = []

    for character in CHARACTERS:
        entry_name = rf"characters\{character}.cug"
        data = extract_decoded(ARCHIVE, table, entry_name)
        patch_character(data, character)
        outputs.extend(write_resource(entry_name, data))

    artifact = extract_decoded(ARCHIVE, table, CRYSTAL_ARTIFACT)
    patch_crystal_artifact(artifact)
    outputs.extend(write_resource(CRYSTAL_ARTIFACT, artifact))

    influence = extract_decoded(ARCHIVE, table, CRYSTAL_INFLUENCE)
    patch_crystal_influence(influence)
    outputs.extend(write_resource(CRYSTAL_INFLUENCE, influence))

    print(f"Built editor copy in: {DEV_MOD_ROOT}")
    print(f"Built runtime loose package in: {RUNTIME_ROOT}")
    print(f"Starting HP: {START_HP}")
    print(f"Mana each turn: {TURN_MANA}")
    for output in outputs:
        print(output.relative_to(GAME_ROOT))


if __name__ == "__main__":
    main()
