#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Проверка этапа 3: ядро сети без Godot."""
from __future__ import annotations
import json, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "reports" / "stage3-verify.json"

def run(cmd, timeout=180):
    try:
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, "TIMEOUT"
    return p.returncode, (p.stdout or "") + (p.stderr or "")

def main() -> int:
    steps, failed = [], []
    def add(name, ok, log=""):
        steps.append({"name": name, "ok": bool(ok), "log": (log or "")[-4000:]})
        if not ok: failed.append(name)
        print(("OK  " if ok else "FAIL"), name)
    for proj, name in [
        ("tests/core/Rubezh.Core.Tests.csproj", "core-csharp"),
        ("tests/stage2/Rubezh.Stage2.Tests.csproj", "stage2-tests"),
        ("tests/stage3/Rubezh.Stage3.Tests.csproj", "stage3-tests"),
    ]:
        code, out = run(["dotnet", "run", "--project", str(ROOT / proj), "-c", "Release"])
        add(name, code == 0 and "провалено 0" in out, out)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"ok": not failed, "failed": failed, "steps": steps}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ИТОГО:", "OK" if not failed else "FAIL " + ",".join(failed))
    return 0 if not failed else 1
if __name__ == "__main__":
    raise SystemExit(main())
