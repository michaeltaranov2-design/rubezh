#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Проверка этапа 1: Python, ядро C#, импорт Godot, самотест, опционально профиль."""
from __future__ import annotations
import argparse, json, os, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "reports" / "stage1-verify.json"

def run(cmd, timeout=240):
    try:
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, "TIMEOUT " + str(timeout) + "s"
    return p.returncode, (p.stdout or "") + (p.stderr or "")

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--godot", default=os.environ.get("GODOT", "godot"))
    ap.add_argument("--profile", action="store_true")
    args = ap.parse_args()
    steps, failed = [], []

    def add(name, ok, log=""):
        steps.append({"name": name, "ok": bool(ok), "log": (log or "")[-4000:]})
        if not ok: failed.append(name)
        print(("OK  " if ok else "FAIL"), name)

    code, out = run([sys.executable, "-m", "unittest", "discover", "-s", str(ROOT / "tests"), "-p", "test*.py"])
    add("python-tests", code == 0, out)

    code, out = run(["dotnet", "--version"])
    ver = out.strip().split(".", 1)[0]
    add("dotnet-sdk", code == 0 and ver.isdigit() and int(ver) >= 8, out)

    code, out = run(["dotnet", "run", "--project", str(ROOT / "tests" / "core" / "Rubezh.Core.Tests.csproj"), "-c", "Release"])
    tail = out.split("Итого")[-1] if "Итого" in out else out
    add("core-csharp", code == 0 and "провалено 0" in tail, out)

    code, out = run(["dotnet", "build", str(ROOT / "game" / "Rubezh.csproj"), "-c", "Debug", "--nologo"])
    add("game-build", code == 0, out)

    code, out = run([args.godot, "--headless", "--path", str(ROOT / "game"), "--import"], timeout=300)
    add("godot-import", code == 0 and "SCRIPT ERROR" not in out and "Exception" not in out, out)

    code, out = run([args.godot, "--headless", "--path", str(ROOT / "game"), "--quit-after", "90", "--", "--self-test"], timeout=180)
    okst = "STAGE1_SELFTEST_OK" in out and "Exception" not in out and "SCRIPT ERROR" not in out
    add("godot-selftest", okst, out)

    if args.profile:
        prof = ROOT / "reports" / "profile-stage1.json"
        prof.parent.mkdir(parents=True, exist_ok=True)
        if prof.exists(): prof.unlink()
        code, out = run([args.godot, "--headless", "--path", str(ROOT / "game"), "--", "--profile", "--profile-out=" + str(prof)], timeout=120)
        okp = prof.is_file() and "Exception" not in out
        note = out
        if okp:
            data = json.loads(prof.read_text(encoding="utf-8"))
            if int(data.get("drawCallsMax", 0)) > 300: okp = False; note += "\ndrawCalls"
            if int(data.get("memoryStaticMax", 0)) > 512 * 1024 * 1024: okp = False; note += "\nmemory"
            note += "\n60 FPS на UHD 620 подтверждается только на целевом ПК."
        add("godot-profile", okp, note)

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"ok": not failed, "failed": failed, "steps": steps}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ИТОГО:", "OK" if not failed else "FAIL " + ", ".join(failed))
    return 0 if not failed else 1

if __name__ == "__main__":
    raise SystemExit(main())
