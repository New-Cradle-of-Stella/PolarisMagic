#!/usr/bin/env python3
"""Build a static Alice in Cradle effect catalog from extracted assets and C#.

The output is metadata only: keys, source locations, parameter names, render
types, dependencies, directives, and literal call sites.  Particle script bodies
are deliberately not copied into the repository.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path


HEADER_RE = re.compile(r"/\*\s*___\s*(.*?)\s*___\s*\*/")
PARAM_RE = re.compile(r"&([A-Za-z_][A-Za-z0-9_]*)")
DIRECTIVE_RE = re.compile(r"(?m)^\s*%([A-Z][A-Z0-9_]*)\b")
TYPE_RE = re.compile(r"(?mi)^\s*type\s+([A-Za-z_][A-Za-z0-9_]*)\b")
LAYER_RE = re.compile(r"(?mi)^\s*layer\s+([A-Za-z_][A-Za-z0-9_]*)\b")
CLONE_RE = re.compile(r"(?mi)^\s*%(?:CLONE|MERGE)\s+([A-Za-z_][A-Za-z0-9_]*)\b")


CALL_PATTERNS = {
    "timeline": re.compile(r"\bPtcST(?:T|TimeFixed)?\s*\(\s*\"([^\"]+)\""),
    "particle": re.compile(r"\bPtcN\s*\(\s*\"([^\"]+)\""),
    "particle_get": re.compile(r"\bEfParticle\.(?:Get)\s*\(\s*\"([^\"]+)\""),
    "particle_once": re.compile(r"\bnew\s+EfParticleOnce\s*\(\s*\"([^\"]+)\""),
    "effect": re.compile(r"\bsetE\s*\(\s*\"([^\"]+)\""),
    "custom_effect": re.compile(r"\bsetEffectWithSpecificFn\s*\(\s*\"([^\"]+)\""),
    "attack_ghost": re.compile(r"\bsetAgdEffect\s*\(\s*\"([^\"]+)\""),
    "post_effect": re.compile(r"\bsetPE(?:bounce2?|absorbed|fadeinoutZSINV|fadeinout)?\s*\(\s*POSTM\.([A-Z0-9_]+)"),
    "particle_file": re.compile(r"\baddAdditionalFile\s*\(\s*\"([^\"]+)\""),
}


def normalize_header(value: str) -> tuple[str, str]:
    value = value.strip()
    if value.startswith("SETTER."):
        return "timeline", value[len("SETTER.") :].strip()
    if value.startswith("AGD."):
        return "attack_ghost", value[len("AGD.") :].strip()
    return "particle", value


def split_sections(path: Path):
    text = path.read_text(encoding="utf-8")
    matches = list(HEADER_RE.finditer(text))
    for index, match in enumerate(matches):
        start = match.end()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        line = text.count("\n", 0, match.start()) + 1
        kind, key = normalize_header(match.group(1))
        if key:
            yield kind, key, line, text[start:end]


def first_token(line: str) -> str | None:
    line = line.strip()
    if not line or line.startswith(("//", "/*", "%", "{", "}", "IF", "ELSE", "SEEK_")):
        return None
    if "=" in line.split(maxsplit=1)[0]:
        return None
    token = line.split(maxsplit=1)[0].lstrip("*~")
    return token if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", token) else None


def analyze_assets(particles_dir: Path):
    raw_sections = []
    for path in sorted(particles_dir.glob("*.particle")):
        for kind, key, line, body in split_sections(path):
            raw_sections.append((path.name, kind, key, line, body))

    keys_by_kind = defaultdict(set)
    for _, kind, key, _, _ in raw_sections:
        keys_by_kind[kind].add(key)
    all_asset_keys = set().union(*keys_by_kind.values())

    rows = []
    for source, kind, key, line, body in raw_sections:
        dependencies = set(CLONE_RE.findall(body))
        for source_line in body.splitlines():
            token = first_token(source_line)
            if token in all_asset_keys and token != key:
                dependencies.add(token)
        effect_calls = sorted(set(re.findall(r"(?mi)^\s*%EFT?\s+([A-Za-z_][A-Za-z0-9_]*)", body)))
        rows.append(
            {
                "kind": kind,
                "key": key,
                "source": source,
                "line": line,
                "render_type": (TYPE_RE.search(body).group(1) if TYPE_RE.search(body) else ""),
                "layer": (LAYER_RE.search(body).group(1) if LAYER_RE.search(body) else ""),
                "parameters": ";".join(sorted(set(PARAM_RE.findall(body)))),
                "directives": ";".join(sorted(set(DIRECTIVE_RE.findall(body)))),
                "dependencies": ";".join(sorted(dependencies)),
                "effect_calls": ";".join(effect_calls),
            }
        )
    return rows


def analyze_code(csharp_roots: list[Path]):
    rows = []
    custom_types = []
    native_particle_refs = []
    assign_type_re = re.compile(r"EfParticle\.assignType\s*\(\s*\"([^\"]+)\"")
    fn_draw_re = re.compile(r"\bfnRunDraw_([A-Za-z_][A-Za-z0-9_]*)\s*\(")

    for root in csharp_roots:
        for path in root.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="replace")
            relative = str(path.relative_to(root)).replace("\\", "/")
            for line_number, line in enumerate(text.splitlines(), 1):
                for call_kind, pattern in CALL_PATTERNS.items():
                    for match in pattern.finditer(line):
                        rows.append(
                            {
                                "kind": call_kind,
                                "key": match.group(1),
                                "source": relative,
                                "line": line_number,
                            }
                        )
                for match in assign_type_re.finditer(line):
                    custom_types.append({"key": match.group(1), "source": relative, "line": line_number})
                for match in fn_draw_re.finditer(line):
                    rows.append(
                        {
                            "kind": "programmatic_drawer",
                            "key": match.group(1),
                            "source": relative,
                            "line": line_number,
                        }
                    )
                if "ParticleSystem" in line or "VisualEffect" in line:
                    native_particle_refs.append({"source": relative, "line": line_number, "text": line.strip()})
    return rows, custom_types, native_particle_refs


def write_csv(path: Path, rows: list[dict], fieldnames: list[str]):
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("particles_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("csharp_root", type=Path, nargs="+")
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)

    assets = analyze_assets(args.particles_dir)
    code, custom_types, native_refs = analyze_code(args.csharp_root)
    write_csv(
        args.output_dir / "AIC029-effect-assets.csv",
        assets,
        ["kind", "key", "source", "line", "render_type", "layer", "parameters", "directives", "dependencies", "effect_calls"],
    )
    write_csv(
        args.output_dir / "AIC029-effect-code-usage.csv",
        code,
        ["kind", "key", "source", "line"],
    )
    write_csv(
        args.output_dir / "AIC029-particle-renderer-types.csv",
        custom_types,
        ["key", "source", "line"],
    )

    summary = {
        "asset_files": len(list(args.particles_dir.glob("*.particle"))),
        "asset_sections": len(assets),
        "asset_kind_counts": dict(sorted(Counter(row["kind"] for row in assets).items())),
        "asset_unique_kind_counts": {
            kind: len({row["key"] for row in assets if row["kind"] == kind})
            for kind in sorted({row["kind"] for row in assets})
        },
        "duplicate_asset_keys": [
            {"kind": kind, "key": key, "count": count}
            for (kind, key), count in sorted(
                Counter((row["kind"], row["key"]) for row in assets).items()
            )
            if count > 1
        ],
        "render_type_counts": dict(sorted(Counter(row["render_type"] or "(inherited/default)" for row in assets if row["kind"] == "particle").items())),
        "code_reference_count": len(code),
        "code_reference_kind_counts": dict(sorted(Counter(row["kind"] for row in code).items())),
        "custom_renderer_types": sorted({row["key"] for row in custom_types}),
        "native_unity_particle_references": native_refs,
    }
    (args.output_dir / "AIC029-effect-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
