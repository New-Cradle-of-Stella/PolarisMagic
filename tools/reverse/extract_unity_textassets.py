#!/usr/bin/env python3
"""Extract embedded Unity TextAsset records by serialized string layout.

This intentionally avoids depending on a Unity asset library.  It is suitable for
plain TextAsset payloads embedded directly in an uncompressed ``*.assets`` file:

    int32 name_length, name_utf8, align4,
    int32 script_length, script_bytes, align4

Only records whose names end with one of the requested suffixes are emitted.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path


SAFE_NAME = re.compile(rb"[A-Za-z0-9_.-]+")


def align4(offset: int) -> int:
    return (offset + 3) & ~3


def discover(data: bytes, suffixes: tuple[bytes, ...]):
    seen: set[tuple[int, str]] = set()
    for suffix in suffixes:
        cursor = 0
        while True:
            suffix_pos = data.find(suffix, cursor)
            if suffix_pos < 0:
                break
            cursor = suffix_pos + 1

            name_start = suffix_pos
            while name_start > 0 and SAFE_NAME.fullmatch(data[name_start - 1 : name_start]):
                name_start -= 1
            name_end = suffix_pos + len(suffix)
            name = data[name_start:name_end]
            if name_start < 4 or len(name) == 0:
                continue
            if struct.unpack_from("<I", data, name_start - 4)[0] != len(name):
                continue

            length_offset = align4(name_end)
            if length_offset + 4 > len(data):
                continue
            payload_length = struct.unpack_from("<I", data, length_offset)[0]
            payload_start = length_offset + 4
            payload_end = payload_start + payload_length
            if payload_length == 0 or payload_end > len(data):
                continue

            try:
                decoded_name = name.decode("utf-8")
                payload = data[payload_start:payload_end]
                payload.decode("utf-8")
            except UnicodeDecodeError:
                continue

            identity = (name_start, decoded_name)
            if identity in seen:
                continue
            seen.add(identity)
            yield {
                "name": decoded_name,
                "offset": name_start - 4,
                "size": payload_length,
                "sha256": hashlib.sha256(payload).hexdigest(),
                "payload": payload,
            }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("asset_file", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument(
        "--suffix",
        action="append",
        default=[".particle"],
        help="TextAsset filename suffix; repeat for multiple suffixes",
    )
    args = parser.parse_args()

    data = args.asset_file.read_bytes()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    records = list(discover(data, tuple(s.encode("utf-8") for s in args.suffix)))

    manifest = []
    used_names: dict[str, int] = {}
    for record in records:
        name = record["name"]
        duplicate = used_names.get(name, 0)
        used_names[name] = duplicate + 1
        output_name = name if duplicate == 0 else f"{name}.{duplicate}"
        (args.output_dir / output_name).write_bytes(record.pop("payload"))
        manifest.append({**record, "output": output_name})

    (args.output_dir / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"count": len(manifest), "output": str(args.output_dir)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
