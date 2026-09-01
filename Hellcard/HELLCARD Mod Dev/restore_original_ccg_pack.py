"""Restore the untouched ccg.pac backup created by patch_ccg_pack.py."""

from __future__ import annotations

import shutil

from build_overpowered_mod import ARCHIVE
from patch_ccg_pack import BACKUP


def main() -> None:
    if not BACKUP.is_file():
        raise SystemExit(f"Backup not found: {BACKUP}")
    shutil.copy2(BACKUP, ARCHIVE)
    if ARCHIVE.stat().st_size != BACKUP.stat().st_size:
        raise RuntimeError("Restore size verification failed")
    print(f"Restored: {ARCHIVE}")


if __name__ == "__main__":
    main()
