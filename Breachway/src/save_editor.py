#!/usr/bin/env python3
"""
Breachway Save Editor
=====================
Safely edits Breachway's persistent save file (PlayerData.bdf).

The save is a 3-line text header ("BreachwayDF 0\n136.0.0\n") followed by
Newtonsoft JSON (with $id/$type metadata). This tool preserves the header
exactly and rewrites the JSON compactly (which Newtonsoft accepts).

ALWAYS backs up to PlayerData.bdf.bak before modifying.

Usage:
    python save_editor.py show
    python save_editor.py backup
    python save_editor.py restore
    python save_editor.py set-money <n>
    python save_editor.py add-money <n>
    python save_editor.py set-fuel <n>
    python save_editor.py add-fuel <n>
    python save_editor.py unlock-achievements
    python save_editor.py --file <path> <command> ...

NOTE: close the game before editing, or it will overwrite your changes.
"""

import json
import os
import shutil
import subprocess
import sys

SAVE_PATH = os.path.expandvars(
    r"%USERPROFILE%\AppData\LocalLow\Edgeflow\Breachway\UserData\PlayerData.bdf"
)

# Achievement keys discovered in the game's Addressables bundles.
ACHIEVEMENT_KEYS = [
    "ach_arbalestvictory",
    "ach_ascension5",
    "ach_avalancheunlock",
    "ach_dmgblocked_01",
    "ach_firebrandunlock",
    "ach_firebrandvictory",
    "ach_flakdamage_01",
    "ach_hacksplayed_01",
    "ach_heatdamage_01",
    "ach_heatdamage_02",
    "ach_lancerunlock",
    "ach_lancervictory",
    "ach_laserdamage_01",
    "ach_marauderunlock",
    "ach_maraudervictory",
    "ach_missiledamage_01",
    "ach_mule",
    "ach_piratebase",
    "ach_railgundamage_01",
    "ach_shielddamage_01",
    "ach_test",
    "ach_totaldamagedealt_01",
    "ach_tutbossvictory",
    "ach_tutorialcompleted",
    "ach_wolfbounty",
]

# Skip these (test/typo keys in the game data).
SKIP_KEYS = {"ach_test", "ach_raildgunamage_01"}


def _die(msg):
    print("ERROR: " + msg)
    sys.exit(1)


def game_running():
    """Return True if Breachway.exe is running (editing would be overwritten)."""
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq Breachway.exe"],
            capture_output=True, text=True, timeout=10,
        ).stdout
        return "Breachway.exe" in out
    except Exception:
        return False


def _load():
    if not os.path.isfile(SAVE_PATH):
        _die(f"Save not found: {SAVE_PATH}")
    with open(SAVE_PATH, "rb") as f:
        raw = f.read()
    start = raw.find(b"{")
    if start < 0:
        _die("Save file has no JSON body — refusing to edit.")
    header = raw[:start]
    try:
        data = json.loads(raw[start:].decode("utf-8"))
    except Exception as e:
        _die(f"Could not parse save JSON: {e}")
    return header, data


def _write(header, data):
    body = json.dumps(data, separators=(",", ":"))
    tmp = SAVE_PATH + ".tmp"
    with open(tmp, "wb") as f:
        f.write(header)
        f.write(body.encode("utf-8"))
    os.replace(tmp, SAVE_PATH)


def _storage_for(data, type_prefix):
    """Find an inventory storage entry by its Il2Cpp type name prefix."""
    storage = data.get("InventoryData", {}).get("storage", {})
    for key, val in storage.items():
        if key.startswith("$"):
            continue
        if key.split(",")[0] == type_prefix:
            return val
    return None


def _amount_editable(data, type_prefix):
    """Return the $values[0].amount dict for a currency storage, or None."""
    storage = _storage_for(data, type_prefix)
    if not storage:
        return None
    values = storage.get("$values") or []
    if not values:
        return None
    first = values[0]
    if "amount" not in first:
        return None
    return first


def _require_backup(header, data):
    """Create a backup the first time a save is modified."""
    bak = SAVE_PATH + ".bak"
    if not os.path.isfile(bak):
        shutil.copy2(SAVE_PATH, bak)
        print(f"Backup created: {bak}")


# --------------------------------------------------------------------------
# Commands
# --------------------------------------------------------------------------

def cmd_show(_args):
    header, data = _load()
    print(f"Save: {SAVE_PATH}")
    print(f"Header: {header.decode('utf-8', 'replace').strip()!r}")

    for label, prefix in (("Money", "Edgeflow.InventoryMoney"),
                          ("Fuel", "Edgeflow.InventoryFuel")):
        entry = _amount_editable(data, prefix)
        print(f"{label}: {entry['amount'] if entry else 'N/A'}")

    ach = data.get("PersistentData", {}).get("AchievementsUnlocked", {})
    ach_list = ach.get("$values", []) if isinstance(ach, dict) else (ach or [])
    print(f"Achievements unlocked: {len(ach_list)}")
    for a in ach_list:
        print(f"    {a}")

    qd = data.get("QuirkData", {})
    uq = qd.get("UnlockedQuirks", {})
    aq = qd.get("ActiveQuirks", {})
    print(f"Unlocked quirks: {len(uq.get('$values', [])) if isinstance(uq, dict) else len(uq or [])}")
    print(f"Active quirks:   {len(aq.get('$values', [])) if isinstance(aq, dict) else len(aq or [])}")

    ships = data.get("UserData", {}).get("AvailableShipMetas")
    print(f"AvailableShipMetas: {'(runtime field, not saved)' if ships is None else len(ships)}")


def cmd_backup(_args):
    if not os.path.isfile(SAVE_PATH):
        _die(f"Save not found: {SAVE_PATH}")
    bak = SAVE_PATH + ".bak"
    shutil.copy2(SAVE_PATH, bak)
    print(f"Backup created: {bak}")


def cmd_restore(_args):
    bak = SAVE_PATH + ".bak"
    if not os.path.isfile(bak):
        _die(f"No backup found at {bak}")
    if game_running():
        _die("Game is running — close it first or it will overwrite the restore.")
    shutil.copy2(bak, SAVE_PATH)
    print(f"Restored from: {bak}")


def cmd_set_money(args):
    _set_amount("Edgeflow.InventoryMoney", "Money", args[0])


def cmd_add_money(args):
    _add_amount("Edgeflow.InventoryMoney", "Money", args[0])


def cmd_set_fuel(args):
    _set_amount("Edgeflow.InventoryFuel", "Fuel", args[0])


def cmd_add_fuel(args):
    _add_amount("Edgeflow.InventoryFuel", "Fuel", args[0])


def _set_amount(prefix, label, amount_str):
    amount = int(amount_str)
    if amount < 0:
        _die("Amount must be >= 0.")
    if game_running():
        _die("Game is running — close it first or it will overwrite your change.")
    header, data = _load()
    entry = _amount_editable(data, prefix)
    if entry is None:
        _die(f"Could not find {label} storage in save.")
    _require_backup(header, data)
    entry["amount"] = amount
    _write(header, data)
    print(f"{label} set to {amount}")


def _add_amount(prefix, label, amount_str):
    amount = int(amount_str)
    if game_running():
        _die("Game is running — close it first or it will overwrite your change.")
    header, data = _load()
    entry = _amount_editable(data, prefix)
    if entry is None:
        _die(f"Could not find {label} storage in save.")
    _require_backup(header, data)
    entry["amount"] += amount
    _write(header, data)
    print(f"{label} is now {entry['amount']}")


def cmd_unlock_achievements(_args):
    if game_running():
        _die("Game is running — close it first or it will overwrite your change.")
    header, data = _load()
    pd = data.setdefault("PersistentData", {})
    ach = pd.get("AchievementsUnlocked")

    if isinstance(ach, dict):
        existing = set(ach.get("$values", []))
        container = ach
        values = ach["$values"]
    else:
        existing = set(ach) if ach else set()
        # Ensure the proper $type structure if it was empty.
        container = {
            "$id": str(99999),
            "$type": "System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib",
            "$values": list(existing),
        }
        pd["AchievementsUnlocked"] = container
        values = container["$values"]

    _require_backup(header, data)
    added = []
    for key in ACHIEVEMENT_KEYS:
        if key in SKIP_KEYS:
            continue
        if key not in existing:
            values.append(key)
            added.append(key)
    _write(header, data)
    if added:
        print(f"Unlocked {len(added)} achievements: {', '.join(added)}")
    else:
        print("All known achievements already unlocked.")


COMMANDS = {
    "show": cmd_show,
    "backup": cmd_backup,
    "restore": cmd_restore,
    "set-money": cmd_set_money,
    "add-money": cmd_add_money,
    "set-fuel": cmd_set_fuel,
    "add-fuel": cmd_add_fuel,
    "unlock-achievements": cmd_unlock_achievements,
}


def main(argv):
    global SAVE_PATH
    args = list(argv)
    if args and args[0] == "--file":
        if len(args) < 2:
            _die("--file requires a path.")
        SAVE_PATH = args[1]
        args = args[2:]
    if not args:
        print(__doc__)
        sys.exit(1)
    cmd = args[0]
    if cmd not in COMMANDS:
        _die(f"Unknown command '{cmd}'. See docstring for usage.")
    COMMANDS[cmd](args[1:])


if __name__ == "__main__":
    main(sys.argv[1:])
