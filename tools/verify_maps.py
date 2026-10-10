#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Проверка карт: ядро, сборка Godot, --maps-test на трёх картах."""
from __future__ import annotations
import argparse, json, os, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "reports" / "maps-verify.json"

def run(cmd, timeout=240):
    try:
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, "TIMEOUT " + str(timeout) + "s"
    return p.returncode, (p.stdout or "") + (p.stderr or "")

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--godot", default=os.environ.get("GODOT", "godot"))
    args = ap.parse_args()
    steps, failed = [], []

    def add(name, ok, log=""):
        steps.append({"name": name, "ok": bool(ok), "log": (log or "")[-4000:]})
        if not ok: failed.append(name)
        print(("OK  " if ok else "FAIL"), name)

    code, out = run(["dotnet", "run", "--project", str(ROOT / "tests" / "stage2" / "Rubezh.Stage2.Tests.csproj"), "-c", "Release"])
    add("stage2-tests", code == 0 and "провалено 0" in out, out)

    code, out = run(["dotnet", "build", str(ROOT / "game" / "Rubezh.csproj"), "-c", "Debug", "--nologo"])
    add("game-build", code == 0, out)

    code, out = run([args.godot, "--headless", "--path", str(ROOT / "game"), "--import"], timeout=300)
    add("godot-import", code == 0 and "SCRIPT ERROR" not in out and "Exception" not in out, out)

    code, out = run([args.godot, "--headless", "--path", str(ROOT / "game"), "--quit-after", "90", "--", "--maps-test"], timeout=180)
    okm = "MAPS_SELFTEST_OK" in out and "Exception" not in out and "SCRIPT ERROR" not in out
    add("godot-maps-test", okm, out)

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"ok": not failed, "failed": failed, "steps": steps}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ИТОГО:", "OK" if not failed else "FAIL " + ",".join(failed))
    return 0 if not failed else 1

if __name__ == "__main__":
    raise SystemExit(main())
