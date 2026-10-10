#!/usr/bin/env python3
"""Local verification entry point. Uses only Python's standard library and .NET 8."""

import argparse
from collections import Counter
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import signal
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
TARGETS = {
    "quick": "src/OpenSage.Game.Tests/OpenSage.Game.Tests.csproj",
    "full": "src/OpenSage.sln",
    "assets": "src/OpenSage.Tools.ContentInventory/OpenSage.Tools.ContentInventory.csproj",
}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


@contextmanager
def verification_lock(directory):
    """Never let two runner invocations write the same bin/obj trees concurrently."""
    directory.parent.mkdir(parents=True, exist_ok=True)
    try:
        directory.mkdir()
    except FileExistsError:
        raise RuntimeError(
            f"Another verification run holds {directory}. Check owner.json; "
            "remove the lock directory only if its process has stopped."
        ) from None
    token = uuid.uuid4().hex
    try:
        write_json(directory / "owner.json", {"pid": os.getpid(), "token": token})
        yield
    finally:
        owner = directory / "owner.json"
        if owner.exists() and json.loads(owner.read_text(encoding="utf-8"))["token"] == token:
            shutil.rmtree(directory)


def project_graph(root, target):
    if target.suffix == ".sln":
        # All solution projects are covered, including tools not referenced by tests.
        pending = list((root / "src").glob("*/*.csproj"))
    else:
        pending = [target]
    projects = set()
    while pending:
        project = pending.pop().resolve()
        if project in projects:
            continue
        projects.add(project)
        for reference in ET.parse(project).iter("ProjectReference"):
            include = reference.attrib["Include"].replace("\\", "/")
            pending.append(project.parent / include)
    return sorted(projects)


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def restore_inputs(root, sdk, configuration, audit):
    inputs = []
    for directory, children, files in os.walk(root):
        children[:] = [name for name in children if name not in
                       {"bin", "obj", ".git", ".artifacts", ".agents", ".codex", ".aws"}]
        for name in files:
            path = Path(directory) / name
            if (path.suffix in {".csproj", ".props", ".targets", ".sln"}
                    or name.lower() in {"global.json", "nuget.config", "packages.lock.json"}):
                inputs.append((str(path.relative_to(root)), file_hash(path)))
    # Include user-level NuGet configuration as well as project configuration.
    for path in {Path.home() / ".nuget/NuGet/NuGet.Config",
                 Path(os.environ.get("APPDATA", str(Path.home()))) / "NuGet/NuGet.Config"}:
        if path.is_file():
            inputs.append((str(path), file_hash(path)))
    payload = [sorted(inputs), sdk, configuration, audit, shutil.which("dotnet"),
               os.environ.get("NUGET_PACKAGES"), os.environ.get("DOTNET_ROOT")]
    return hashlib.sha256(json.dumps(payload).encode()).hexdigest()


def assets_hashes(projects):
    return {str(project.parent / "obj/project.assets.json"): file_hash(project.parent / "obj/project.assets.json")
            for project in projects}


def restore_is_current(stamp, fingerprint, projects):
    try:
        cached = json.loads(stamp.read_text(encoding="utf-8"))
        return cached["inputs"] == fingerprint and cached["assets"] == assets_hashes(projects)
    except (OSError, ValueError, KeyError):
        return False


def read_test_results(directory):
    totals = Counter()
    failures = []
    skipped = []
    reports = sorted(directory.glob("*.trx"))
    if not reports:
        raise ValueError("No TRX reports were produced; test verification did not complete.")
    for path in reports:
        tree = ET.parse(path)
        counters = tree.find(".//{*}Counters")
        if counters is None:
            raise ValueError(f"Missing test counters in {path}")
        for name in ("total", "executed", "passed", "failed"):
            totals[name] += int(counters.attrib.get(name, 0))
        skipped_in_report = 0
        for result in tree.findall(".//{*}UnitTestResult"):
            outcome = result.attrib.get("outcome")
            if outcome == "NotExecuted":
                skipped_in_report += 1
                skipped.append({"test": result.attrib.get("testName"),
                                "reason": result.findtext(".//{*}Message", ""), "report": str(path)})
            elif outcome != "Passed":
                failures.append({
                    "test": result.attrib.get("testName"),
                    "outcome": outcome,
                    "message": result.findtext(".//{*}Message", ""),
                    "stackTrace": result.findtext(".//{*}StackTrace", ""),
                    "report": str(path),
                })
        # The xUnit adapter can emit notExecuted="0" despite NotExecuted results.
        totals["skipped"] += max(int(counters.attrib.get("notExecuted", 0)), skipped_in_report)
    if totals["executed"] == 0:
        raise ValueError("Zero tests executed. Check the filter; this is not a successful verification.")
    return {"counts": dict(totals), "failures": failures, "skipped": skipped,
            "reports": [str(path) for path in reports]}


def summarize_inventory(path):
    summaries = []
    for manifest in json.loads(path.read_text(encoding="utf-8")):
        if manifest.get("FormatVersion") != 1:
            raise ValueError("Unsupported inventory format version")
        usages = Counter(feature["Status"] for item in manifest["Maps"] for feature in item["ScriptFeatures"])
        summaries.append({
            "game": manifest["Game"],
            "maps": len(manifest["Maps"]),
            "parsedMaps": sum(item["Parsed"] for item in manifest["Maps"]),
            "scriptUsages": dict(usages),
            "failures": manifest["Failures"],
            "pendingCoverage": manifest["PendingCoverage"],
            "completionGatePassed": manifest["CompletionGatePassed"],
        })
    return summaries


class VerificationFailure(Exception):
    def __init__(self, message, exit_code=1):
        super().__init__(message)
        self.exit_code = exit_code


class Verifier:
    def __init__(self, root, options):
        self.root = root
        self.options = options
        self.base = root / ".artifacts/verification"
        run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
        self.directory = self.base / "runs" / run_id
        self.report = {
            "formatVersion": 1, "mode": options.mode, "configuration": options.configuration,
            "status": "running", "completionGatePassed": False, "dependencyAudit": options.audit,
            "directory": str(self.directory), "steps": [],
            "reproduce": [sys.executable, str(root / "scripts/verify.py"), *sys.argv[1:]],
        }

    def command(self, name, command, check=True):
        log = self.directory / f"{name}.log"
        print(f"[{name}] {shlex.join(command)}", flush=True)
        started = time.monotonic()
        timed_out = False
        with log.open("w", encoding="utf-8") as output:
            process = subprocess.Popen(command, cwd=self.root, stdout=output, stderr=subprocess.STDOUT,
                                       start_new_session=os.name == "posix")
            try:
                code = process.wait(timeout=self.options.timeout)
            except (subprocess.TimeoutExpired, KeyboardInterrupt) as exception:
                if os.name == "posix":
                    os.killpg(process.pid, signal.SIGTERM)
                else:
                    process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    if os.name == "posix":
                        os.killpg(process.pid, signal.SIGKILL)
                    else:
                        process.kill()
                    process.wait()
                if isinstance(exception, KeyboardInterrupt):
                    raise
                code, timed_out = 124, True
        elapsed = round(time.monotonic() - started, 2)
        self.report["steps"].append({"name": name, "command": command, "exitCode": code,
                                     "seconds": elapsed, "log": str(log), "timedOut": timed_out})
        print(f"[{name}] exit {code}, {elapsed}s", flush=True)
        if code and check:
            print("\n".join(log.read_text(encoding="utf-8", errors="replace").splitlines()[-50:]), file=sys.stderr)
            raise VerificationFailure(f"{name} failed; see {log}")
        return code

    def asset_paths(self):
        config_path = self.root / "scripts/verification.local.json"
        config = json.loads(config_path.read_text(encoding="utf-8")) if config_path.exists() else {}
        generals = self.options.generals or os.environ.get("CNC_GENERALS_PATH") or config.get("generalsPath")
        zero_hour = self.options.zero_hour or os.environ.get("CNC_GENERALS_ZH_PATH") or config.get("zeroHourPath")
        missing = [name for name, path in (("Generals", generals), ("Zero Hour", zero_hour))
                   if not path or not Path(path).is_dir()]
        if missing:
            self.report["status"] = "skipped"
            raise VerificationFailure("Asset validation skipped: configure valid installation paths for "
                                      + ", ".join(missing), 2)
        return [str(Path(path).resolve()) for path in (generals, zero_hour)]

    def restore(self, target):
        sdk = (self.directory / "sdk.log").read_text(encoding="utf-8").strip()
        self.report["sdkVersion"] = sdk
        fingerprint = restore_inputs(self.root, sdk, self.options.configuration, self.options.audit)
        projects = project_graph(self.root, target)
        stamp = self.base / "cache" / f"restore-{self.options.mode}-{self.options.configuration}.json"
        if not self.options.restore and restore_is_current(stamp, fingerprint, projects):
            self.report["steps"].append({"name": "restore", "status": "cached", "seconds": 0})
            print("[restore] cached; project/package inputs and assets unchanged", flush=True)
            return
        self.command("restore", ["dotnet", "restore", str(target), "--disable-parallel", "-m:1",
                                  f"-p:Configuration={self.options.configuration}",
                                  f"-p:NuGetAudit={str(self.options.audit).lower()}", "-v", "minimal"])
        write_json(stamp, {"inputs": fingerprint, "assets": assets_hashes(projects)})

    def execute(self):
        started = time.monotonic()
        self.directory.mkdir(parents=True)
        exit_code = 0
        try:
            installations = self.asset_paths() if self.options.mode == "assets" else None
            if self.options.mode != "assets":
                self.command("tooling-tests", [sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests", "-v"])
            self.command("sdk", ["dotnet", "--version"])
            target = self.root / TARGETS[self.options.mode]
            self.restore(target)
            self.command("build", ["dotnet", "build", str(target), "--configuration", self.options.configuration,
                                    "--no-restore", f"-m:{self.options.jobs}", "-v", "minimal"])
            if installations is not None:
                manifest = self.directory / "content-manifest.json"
                dll = target.parent / "bin" / self.options.configuration / "net8.0/OpenSage.Tools.ContentInventory.dll"
                code = self.command("inventory", ["dotnet", str(dll), *installations, str(manifest)], check=False)
                if manifest.exists():
                    self.report["inventory"] = summarize_inventory(manifest)
                    self.report["manifest"] = str(manifest)
                    write_json(self.base / "latest-assets.json", {"manifest": str(manifest)})
                    for game in self.report["inventory"]:
                        print(f"{game['game']}: {game['parsedMaps']}/{game['maps']} maps parsed; "
                              f"{len(game['failures'])} failures; script usages {game['scriptUsages']}")
                if code or not manifest.exists():
                    raise VerificationFailure("Asset inventory has failures; inspect report.json and inventory.log",
                                              2 if code == 2 else 1)
            else:
                test_filter = "Category!=Interactive"
                if self.options.filter:
                    test_filter += f"&({self.options.filter})"
                code = self.command("tests", ["dotnet", "test", str(target), "--configuration", self.options.configuration,
                                               "--no-build", "--no-restore", f"-m:{self.options.jobs}",
                                               "--filter", test_filter, "--logger", "trx", "--results-directory",
                                               str(self.directory), "-v", "minimal"], check=False)
                # Even a failed test process can have useful structured test failures.
                if code:
                    log = self.directory / "tests.log"
                    print("\n".join(log.read_text(encoding="utf-8", errors="replace").splitlines()[-40:]), file=sys.stderr)
                self.report["tests"] = read_test_results(self.directory)
                counts = self.report["tests"]["counts"]
                print(f"Tests: {counts}", flush=True)
                if code or counts.get("failed", 0) or self.report["tests"]["failures"]:
                    raise VerificationFailure("Tests failed; inspect report.json and tests.log")
            self.report["status"] = "passed"
        except (VerificationFailure, OSError, ValueError, KeyError, TypeError, ET.ParseError) as exception:
            exit_code = getattr(exception, "exit_code", 1)
            if self.report["status"] != "skipped":
                self.report["status"] = "failed"
            self.report["error"] = str(exception)
            print(str(exception), file=sys.stderr)
        except KeyboardInterrupt:
            exit_code = 130
            self.report["status"] = "interrupted"
        finally:
            self.report["seconds"] = round(time.monotonic() - started, 2)
            self.report["exitCode"] = exit_code
            write_json(self.directory / "report.json", self.report)
            write_json(self.base / "latest.json", self.report)
            print(f"Report: {self.directory / 'report.json'}", flush=True)
        return exit_code


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=TARGETS)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--filter", help="Additional VSTest filter for quick/full; Interactive tests remain excluded")
    parser.add_argument("--restore", action="store_true", help="Force restore even if its input cache is current")
    parser.add_argument("--audit", action="store_true", help="Enable NuGet vulnerability audit during restore")
    parser.add_argument("--jobs", type=int, default=1, help="MSBuild worker count (default: 1 for restricted environments)")
    parser.add_argument("--timeout", type=int, default=600, help="Maximum seconds per command (default: 600)")
    parser.add_argument("--generals", help="Generals installation path (assets mode)")
    parser.add_argument("--zero-hour", help="Zero Hour installation path (assets mode)")
    options = parser.parse_args(argv)
    if options.jobs < 1 or options.timeout < 1:
        parser.error("--jobs and --timeout must be positive")
    if options.mode == "assets" and options.filter:
        parser.error("--filter applies to quick/full; query a generated manifest with scripts/content_report.py")
    return options


def main():
    options = parse_args()
    try:
        with verification_lock(ROOT / ".artifacts/verification/.lock"):
            return Verifier(ROOT, options).execute()
    except (RuntimeError, OSError) as exception:
        print(str(exception), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
