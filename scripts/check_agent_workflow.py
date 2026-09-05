#!/usr/bin/env python3
"""Reject stale branch-based instructions in the single-maintainer workflow."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


FORBIDDEN_INSTRUCTIONS = (
    "Create a **feature branch** for every change",
    "Do not commit directly to `main`",
    "Require a pull request before merging",
    "merged to `main` via a reviewed pull request",
    "on a **feature branch** named after the change",
)


def check(root: Path) -> list[str]:
    errors: list[str] = []
    for name in ("Agents.md", "CONTRIBUTING.md"):
        path = root / name
        if not path.is_file():
            errors.append(f"missing normative workflow file: {name}")
            continue
        contents = path.read_text(encoding="utf-8")
        for instruction in FORBIDDEN_INSTRUCTIONS:
            if instruction in contents:
                errors.append(
                    f"contradictory branch instruction in {name}: {instruction}"
                )
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    errors = check(args.root)
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    print("agent workflow is main-only")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
