"""Reversibly install the overpowered overrides directly into ccg.pac."""

from __future__ import annotations

import shutil
from pathlib import Path

from build_overpowered_mod import (
    ARCHIVE,
    CHARACTERS,
    CRYSTAL_ARTIFACT,
    CRYSTAL_INFLUENCE,
    RUNTIME_ROOT,
    extract_decoded,
    read_archive_entries,
    xor_decode,
)


BACKUP = ARCHIVE.with_name("ccg.pac.original")

TARGETS = tuple(
    [rf"characters\{character}.cug" for character in CHARACTERS]
    + [CRYSTAL_ARTIFACT, CRYSTAL_INFLUENCE]
)


def expected_override(entry_name: str) -> bytes:
    return (
        RUNTIME_ROOT / Path(entry_name.replace("\\", "/"))
    ).read_bytes()


def backup_original() -> None:
    if BACKUP.exists():
        if BACKUP.stat().st_size != ARCHIVE.stat().st_size:
            raise RuntimeError(
                f"Existing backup has the wrong size: {BACKUP}"
            )
        print(f"Backup already exists: {BACKUP}")
        return

    print(f"Creating backup: {BACKUP}")
    shutil.copy2(ARCHIVE, BACKUP)
    if BACKUP.stat().st_size != ARCHIVE.stat().st_size:
        raise RuntimeError("Backup size verification failed")


def archive_matches(
    archive: Path, table: dict[str, tuple[int, int]], entry_name: str
) -> bool:
    return bytes(extract_decoded(archive, table, entry_name)) == expected_override(
        entry_name
    )


def install() -> None:
    table = read_archive_entries(ARCHIVE)
    backup_table = read_archive_entries(BACKUP)

    for entry_name in TARGETS:
        expected = expected_override(entry_name)
        _, size = table[entry_name.casefold()]
        if len(expected) != size:
            raise RuntimeError(
                f"Size mismatch for {entry_name}: {len(expected)} != {size}"
            )

        if archive_matches(ARCHIVE, table, entry_name):
            print(f"Already patched: {entry_name}")
            continue

        original = bytes(
            extract_decoded(BACKUP, backup_table, entry_name)
        )
        current = bytes(extract_decoded(ARCHIVE, table, entry_name))
        if current != original:
            raise RuntimeError(
                f"Refusing to overwrite an unknown modification: {entry_name}"
            )

        offset, _ = table[entry_name.casefold()]
        encoded = xor_decode(expected)
        with ARCHIVE.open("r+b") as handle:
            handle.seek(offset)
            handle.write(encoded)
            handle.flush()
        print(f"Patched: {entry_name}")

    verify_table = read_archive_entries(ARCHIVE)
    failures = [
        entry_name
        for entry_name in TARGETS
        if not archive_matches(ARCHIVE, verify_table, entry_name)
    ]
    if failures:
        raise RuntimeError(f"Verification failed: {', '.join(failures)}")

    print("PASS: all packed resources match the generated overrides")


def main() -> None:
    if not ARCHIVE.is_file():
        raise SystemExit(f"Archive not found: {ARCHIVE}")
    backup_original()
    install()


if __name__ == "__main__":
    main()
