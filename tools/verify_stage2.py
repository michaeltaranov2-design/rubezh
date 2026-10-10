#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations
import json, os, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
def run(cmd):
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return p.returncode, (p.stdout or "") + (p.stderr or "")
def main():
    failed = []
    code, out = run(["dotnet", "run", "--project", str(ROOT/"tests"/"core"/"Rubezh.Core.Tests.csproj"), "-c", "Release"])
    print(out[-2000:])
    if code != 0 or "провалено 0" not in out: failed.append("stage1-core")
    code, out = run(["dotnet", "run", "--project", str(ROOT/"tests"/"stage2"/"Rubezh.Stage2.Tests.csproj"), "-c", "Release"])
    print(out[-3000:])
    if code != 0 or "провалено 0" not in out: failed.append("stage2")
    (ROOT/"reports").mkdir(exist_ok=True)
    (ROOT/"reports"/"stage2-verify.json").write_text(json.dumps({"ok": not failed, "failed": failed}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ИТОГО:", "OK" if not failed else "FAIL " + ",".join(failed))
    return 0 if not failed else 1
if __name__ == "__main__":
    raise SystemExit(main())
