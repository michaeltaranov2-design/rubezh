#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Копирует проверенные GLB/PNG оружия в game/assets/weapons."""
from __future__ import annotations
import re, shutil, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SAFE = re.compile(r"^[A-Za-z0-9_\\-]+$")

def main() -> int:
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "generated" / "refresh-iter5"
    dst = ROOT / "game" / "assets" / "weapons"
    dst.mkdir(parents=True, exist_ok=True)
    if not src.is_dir():
        print("нет каталога экспорта:", src)
        return 1
    n = 0
    for p in sorted(src.rglob("*")):
        if p.suffix.lower() not in {".glb", ".png"}: continue
        if not SAFE.match(p.stem):
            print("пропуск небезопасного имени:", p.name)
            continue
        shutil.copy2(p, dst / p.name)
        n += 1
        print("скопирован", p.name)
    print("скопировано файлов:", n)
    return 0 if n else 1

if __name__ == "__main__":
    raise SystemExit(main())
