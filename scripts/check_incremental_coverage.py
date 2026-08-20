#!/usr/bin/env python3
"""Incremental coverage gate for pull requests.

Compares the executable lines added by a PR (git diff) against the OpenCover
coverage reports produced by `dotnet test --collect:"XPlat Code Coverage"` and
fails when the covered fraction of NEW lines is below a threshold. Overall
coverage is printed for information but never gates.

Excluded from the gate: test projects, EF migrations, and generated code.

Usage:
    python3 scripts/check_incremental_coverage.py \
        --base origin/main --threshold 0.80 \
        --reports "TestResults/**/coverage.opencover.xml"

Exit codes: 0 = pass (or base ref unavailable), 1 = gate red.
"""

from __future__ import annotations

import argparse
import glob
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

EXCLUDE_SUBSTRINGS = (
    "/tests/",
    "\\tests\\",
    "/Migrations/",
    "\\Migrations\\",
    "/obj/",
    "\\obj\\",
)
EXCLUDE_SUFFIXES = (".Designer.cs", ".g.cs")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="origin/main", help="git ref the diff is computed against")
    parser.add_argument("--threshold", type=float, default=0.80, help="minimum new-line coverage ratio")
    parser.add_argument("--reports", default="TestResults/**/coverage.opencover.xml",
                        help="glob of OpenCover XML reports to merge")
    return parser.parse_args()


def is_excluded(path: str) -> bool:
    normalized = path.replace("\\", "/")
    if any(s in normalized for s in ("/tests/", "/Migrations/", "/obj/")):
        return True
    return normalized.endswith((".Designer.cs", ".g.cs"))


def added_lines(base: str) -> dict[str, set[int]] | None:
    """Map file path -> set of added line numbers from the PR diff.

    Returns None when the base ref is not available (e.g. no remote), in which
    case the gate is skipped rather than failed.
    """
    if subprocess.run(["git", "rev-parse", "--verify", "--quiet", base],
                      check=False).returncode != 0:
        return None

    proc = subprocess.run(
        ["git", "diff", "--unified=0", f"{base}...HEAD", "--", "*.cs"],
        capture_output=True, text=True, check=True)

    changed: dict[str, set[int]] = {}
    current_file: str | None = None
    current_new: int | None = None
    for line in proc.stdout.splitlines():
        if line.startswith("+++"):
            current_file = line[6:]
            changed.setdefault(current_file, set())
        elif line.startswith("@@") and current_file is not None:
            header = line.split("@@")[1].strip()
            new_part = header.split(" ")[1].lstrip("+")
            current_new = int(new_part.split(",")[0])
        elif line.startswith("+") and current_file is not None and current_new is not None:
            changed[current_file].add(current_new)
            current_new += 1
        elif line.startswith(" ") and current_file is not None and current_new is not None:
            current_new += 1
        # '-' and '\' lines do not advance the new-file counter.
    return changed


def parse_reports(patterns: str) -> tuple[dict[str, dict[int, bool]], int, int]:
    """Return (file -> {line: covered}, totalLines, coveredLines) merged across reports."""
    files_by_uid: dict[str, str] = {}
    per_file: dict[str, dict[int, bool]] = {}
    total = 0
    covered = 0
    for pattern in patterns.split(";"):
        for path in glob.glob(pattern, recursive=True):
            tree = ET.parse(path)
            root = tree.getroot()
            for file_el in root.iter("File"):
                files_by_uid[file_el.get("uid")] = file_el.get("fullPath", "")
            for sp in root.iter("SequencePoint"):
                file_uid = sp.get("fileid")
                line = int(sp.get("sl"))
                vc = int(sp.get("vc"))
                full_path = files_by_uid.get(file_uid, "")
                if not full_path:
                    continue
                bucket = per_file.setdefault(full_path, {})
                bucket[line] = bucket.get(line, False) or vc > 0
                total += 1
                if vc > 0:
                    covered += 1
    return per_file, total, covered


def main() -> int:
    args = parse_args()
    changed = added_lines(args.base)
    if changed is None:
        print(f"[coverage-gate] base ref '{args.base}' not found; incremental gate skipped.")
        return 0

    per_file, total_lines, covered_lines = parse_reports(args.reports)

    executable = 0
    covered_new = 0
    for path, lines in changed.items():
        if is_excluded(path):
            continue
        report = per_file.get(os.path.abspath(path)) or per_file.get(path)
        if report is None:
            continue
        for line in lines:
            if line in report:
                executable += 1
                if report[line]:
                    covered_new += 1

    overall = (covered_lines / total_lines * 100) if total_lines else 0.0
    new_ratio = (covered_new / executable) if executable else 1.0

    print(f"[coverage-gate] new executable lines: {executable}, covered: {covered_new}, "
          f"new-line coverage: {new_ratio * 100:.1f}% (threshold {args.threshold * 100:.0f}%)")
    print(f"[coverage-gate] overall coverage (informational): {overall:.1f}%")

    if executable == 0:
        print("[coverage-gate] no new executable lines in diff; gate passes.")
        return 0

    if new_ratio < args.threshold:
        print(f"[coverage-gate] FAIL: new-line coverage below {args.threshold * 100:.0f}%. "
              "Add tests for the new code before merging.")
        return 1

    print("[coverage-gate] PASS.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
