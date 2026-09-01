"""Structural verification for the generated HELLCARD local mod."""

from __future__ import annotations

import struct
from pathlib import Path

from build_overpowered_mod import (
    CHARACTERS,
    DEV_MOD_ROOT,
    RUNTIME_ROOT,
    START_HP,
    TURN_MANA,
    Reader,
    parse_resource_header,
)

MOD_ROOT = RUNTIME_ROOT


def verify_character(name: str) -> str:
    path = MOD_ROOT / "characters" / f"{name}.cug"
    reader = Reader(bytearray(path.read_bytes()))
    assert parse_resource_header(reader, "BCCGCharacterClass") == name
    assert reader.u8()[1] == 3
    assert reader.string()[1] == name
    hp = reader.u32()[1]
    assert hp == START_HP, f"{name}: expected {START_HP} HP, got {hp}"
    return f"{name}: {hp} HP"


def verify_artifact() -> str:
    path = MOD_ROOT / "artifacts" / "glowing_crystal_self.cug"
    reader = Reader(bytearray(path.read_bytes()))
    assert (
        parse_resource_header(reader, "BCCGArtifactClass")
        == "glowing_crystal_self"
    )
    assert reader.u8()[1] == 6
    reader.u32()
    reader.string()
    assert reader.string()[1] == "glowing_crystal_self"
    assert reader.string()[1] == ""
    starting_cost = reader.u32()[1]
    reader.u32()
    reader.u32()
    reader.pos += 8
    artifact_type = reader.u32()[1]
    temporary = reader.u8()[1]
    visible = reader.u8()[1]
    assert starting_cost == 0
    assert artifact_type == 0
    assert temporary == 0
    assert visible == 1

    marker = (
        struct.pack("<I", len("BCCGInfluencePushBehaviour"))
        + b"BCCGInfluencePushBehaviour"
    )
    behavior_offset = reader.data.find(marker)
    assert behavior_offset >= 0
    behavior_reader = Reader(reader.data)
    behavior_reader.pos = behavior_offset
    assert behavior_reader.string()[1] == "BCCGInfluencePushBehaviour"
    assert behavior_reader.u32()[1] == 2
    assert behavior_reader.string()[1] == "add_influence"
    payload_size = behavior_reader.u32()[1]
    payload_offset = behavior_reader.pos
    assert payload_size == 39
    pushed_counter = struct.unpack_from(
        "<I", reader.data, payload_offset + 3
    )[0]
    assert pushed_counter == TURN_MANA
    return (
        "God Mode Crystal: free, Starter, persistent, visible, "
        f"pushes {pushed_counter} mana"
    )


def verify_influence() -> str:
    path = MOD_ROOT / "influences" / "glowing_crystal_self.cug"
    reader = Reader(bytearray(path.read_bytes()))
    assert (
        parse_resource_header(reader, "BCCGInfluenceClass")
        == "glowing_crystal_self"
    )
    assert reader.u8()[1] == 1
    reader.string()
    reader.string()
    assert reader.string()[1] == "BCCGAddManaInfluence"
    reader.string()
    reader.u32()
    mana = reader.u32()[1]
    assert mana == TURN_MANA, f"expected {TURN_MANA} mana, got {mana}"
    return f"God Mode influence: {mana} mana per turn"


def main() -> None:
    results = [verify_character(name) for name in CHARACTERS]
    results.append(verify_artifact())
    results.append(verify_influence())

    language = (DEV_MOD_ROOT / "languages" / "en.utf8").read_text(encoding="utf-8")
    assert 'glowing_crystal_self_name = "God Mode Crystal"' in language
    results.append("Editor-copy English labels: present")

    for category in ("characters", "artifacts", "influences"):
        runtime_files = {
            path.name: path.read_bytes()
            for path in (RUNTIME_ROOT / category).glob("*.cug")
        }
        dev_files = {
            path.name: path.read_bytes()
            for path in (DEV_MOD_ROOT / category).glob("*.cug")
        }
        assert runtime_files == dev_files
    results.append("Runtime loose package matches editor copy")

    for result in results:
        print(f"PASS: {result}")


if __name__ == "__main__":
    main()
