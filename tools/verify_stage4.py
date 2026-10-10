#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Проверка этапа 4: ядро, раунды, сеть, консоль/bhop."""
from __future__ import annotations
import json, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "reports" / "stage4-verify.json"

def run(cmd, timeout=240):
    try:
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, "TIMEOUT " + str(timeout) + "s"
    return p.returncode, (p.stdout or "") + (p.stderr or "")

def main() -> int:
    steps, failed = [], []
    def add(name, ok, log=""):
        steps.append({"name": name, "ok": bool(ok), "log": (log or "")[-4000:]})
        if not ok: failed.append(name)
        print(("OK  " if ok else "FAIL"), name)
    for name, proj in (
        ("core-csharp", ROOT / "tests" / "core" / "Rubezh.Core.Tests.csproj"),
        ("stage2-tests", ROOT / "tests" / "stage2" / "Rubezh.Stage2.Tests.csproj"),
        ("stage3-tests", ROOT / "tests" / "stage3" / "Rubezh.Stage3.Tests.csproj"),
        ("stage4-tests", ROOT / "tests" / "stage4" / "Rubezh.Stage4.Tests.csproj"),
    ):
        code, out = run(["dotnet", "run", "--project", str(proj), "-c", "Release"])
        add(name, code == 0 and "провалено 0" in out, out)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"ok": not failed, "failed": failed, "steps": steps}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ИТОГО:", "OK" if not failed else "FAIL " + ",".join(failed))
    return 0 if not failed else 1
if __name__ == "__main__":
    raise SystemExit(main())
