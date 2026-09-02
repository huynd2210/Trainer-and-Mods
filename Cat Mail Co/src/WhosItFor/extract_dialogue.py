"""Extract Cat Mail Co. customer dialogue from the I2 Localization table
serialized inside CatMailCo_Data/resources.assets.

Record layout (derived empirically from the serialized bytes):

    <int32 len> <utf8 term> <pad to 4>
    <int32 0>                       -- empty string slot
    <int32 0x1C>                    -- marker: 28 value slots follow
    slot 0      English
    slot 1      Description (translator note, usually empty)
    slots 2-13  French, Italian, German, Spanish, Portuguese, Polish,
                Chinese(S), Chinese(T), Japanese, Korean, Ukrainian, Russian
    slot 15     ordinal index
    slot 23     DialogueType
    slot 24     DialogueCategory
    slot 26     DialogueAge
    slot 27     DialogueMood
    <int32 0x1C>                    -- marker: next record

Every slot is a length-prefixed, 4-byte-aligned UTF-8 string.
"""
import json
import re
import struct
import sys
from collections import Counter

ASSETS = r"C:\Games\Cat.Mail.Co\game\CatMailCo_Data\resources.assets"
MARKER = struct.pack("<i", 0x1C)
SLOT_COUNT = 28

LANGUAGES = [
    "English", "_Description", "French", "Italian", "German", "Spanish",
    "Portuguese", "Polish", "ChineseSimplified", "ChineseTraditional",
    "Japanese", "Korean", "Ukrainian", "Russian",
]
SLOT_TYPE, SLOT_CATEGORY, SLOT_AGE, SLOT_MOOD = 23, 24, 26, 27

TERM_RE = re.compile(
    rb"(?:ClientDialogues2?|BoatDialogues|OldPostman_Dialogue)/[A-Za-z0-9_]+"
)


def read_string(data, off):
    """Read a length-prefixed, 4-byte-aligned UTF-8 string -> (text, next_off)."""
    if off + 4 > len(data):
        return None, off
    (length,) = struct.unpack_from("<i", data, off)
    if length < 0 or length > 4096 or off + 4 + length > len(data):
        return None, off
    try:
        text = data[off + 4:off + 4 + length].decode("utf-8")
    except UnicodeDecodeError:
        return None, off
    end = off + 4 + length
    end += (-end) % 4
    return text, end


def parse_record(data, term_off, term):
    """Parse one term record starting at its length prefix."""
    off = term_off + 4 + len(term.encode())
    off += (-off) % 4

    # Skip forward to the 0x1C marker that opens the value block.
    scan = off
    for _ in range(4):
        if data[scan:scan + 4] == MARKER:
            break
        scan += 4
    else:
        return None
    off = scan + 4

    slots = []
    for _ in range(SLOT_COUNT):
        text, off = read_string(data, off)
        if text is None:
            return None
        slots.append(text)

    record = {"term": term}
    for i, lang in enumerate(LANGUAGES):
        if slots[i]:
            record[lang.lstrip("_") if lang == "_Description" else lang] = slots[i]
    record["type"] = slots[SLOT_TYPE]
    record["category"] = slots[SLOT_CATEGORY]
    record["age"] = slots[SLOT_AGE] or "Any"
    record["mood"] = slots[SLOT_MOOD]
    return record


def main():
    data = open(ASSETS, "rb").read()

    seen = set()
    records = []
    for m in TERM_RE.finditer(data):
        off = m.start()
        if off < 4:
            continue
        term = m.group().decode()
        (length,) = struct.unpack_from("<i", data, off - 4)
        if length != len(m.group()) or term in seen:
            continue
        rec = parse_record(data, off - 4, term)
        if rec and rec.get("English"):
            seen.add(term)
            records.append(rec)

    def sort_key(r):
        base, _, num = r["term"].rpartition("_")
        return (base, int(num) if num.isdigit() else 0)

    records.sort(key=sort_key)

    with open("dialogue.json", "w", encoding="utf-8") as fh:
        json.dump(records, fh, indent=2, ensure_ascii=False)

    print("parsed %d dialogue records -> dialogue.json" % len(records))
    client = [r for r in records if r["term"].startswith("ClientDialogues")]
    print("\n=== customer lines by DialogueType ===")
    for t, n in Counter(r["type"] for r in client).most_common():
        print("  %-22s %3d" % (t or "(blank)", n))
    print("\n=== customer lines by DialogueCategory ===")
    for c, n in Counter(r["category"] for r in client).most_common():
        print("  %-22s %3d" % (c or "(blank)", n))


if __name__ == "__main__":
    main()
