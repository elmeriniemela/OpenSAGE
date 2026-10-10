#!/usr/bin/env python3
"""Query an existing content inventory without restoring, building or reading game assets."""

import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
STATUSES = ("MissingHandler", "EmptyHandler", "RegisteredUnverified")


def query(manifests, game="", map_name="", feature="", status=None, failures=False, limit=20):
    rows = []
    grouped = defaultdict(list)
    counts = Counter()
    for manifest in manifests:
        if manifest.get("FormatVersion") != 1:
            raise ValueError("Unsupported inventory format version")
        if game.casefold() not in manifest["Game"].casefold():
            continue
        if failures:
            for failure in manifest["Failures"]:
                if (feature.casefold() in failure["Feature"].casefold()
                        and map_name.casefold() in failure["Source"].casefold()):
                    rows.append({"game": manifest["Game"], **failure})
            continue
        for item in manifest["Maps"]:
            if map_name.casefold() not in item["Path"].casefold():
                continue
            for usage in item["ScriptFeatures"]:
                if feature.casefold() not in usage["Feature"].casefold():
                    continue
                if usage["Status"] not in ((status,) if status else STATUSES[:2]):
                    continue
                row = {"game": manifest["Game"], "map": item["Path"], **usage}
                counts[usage["Status"]] += 1
                if feature or map_name:
                    rows.append(row)
                else:
                    grouped[(usage["Kind"], usage["Feature"], usage["Status"])].append(row)
    if grouped:
        rows = [{"kind": kind, "feature": name, "status": state, "usages": len(examples),
                 "maps": len({(example["game"], example["map"]) for example in examples}),
                 "examples": list({(example["game"], example["map"], example["Script"], example.get("Branch")):
                                    example for example in examples}.values())[:3]}
                for (kind, name, state), examples in sorted(grouped.items(), key=lambda pair: (-len(pair[1]), pair[0]))]
    return {"completionGatePassed": False, "matchingRows": len(rows), "scriptUsages": dict(counts),
            "truncated": len(rows) > limit, "results": rows[:limit]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, help="Default: manifest from the latest assets verification")
    parser.add_argument("--game", default="", help="Game name substring")
    parser.add_argument("--map", default="", dest="map_name", help="Map/source substring")
    parser.add_argument("--feature", default="", help="Feature name substring")
    parser.add_argument("--status", choices=STATUSES, help="Default: missing and empty handlers")
    parser.add_argument("--failures", action="store_true", help="Show missing file/definition/movie and parse failures")
    parser.add_argument("--limit", type=int, default=20)
    options = parser.parse_args()
    if options.limit < 1:
        parser.error("--limit must be positive")
    try:
        path = options.manifest
        if path is None:
            latest = json.loads((ROOT / ".artifacts/verification/latest-assets.json").read_text(encoding="utf-8"))
            path = Path(latest["manifest"])
        manifests = json.loads(path.read_text(encoding="utf-8"))
        result = query(manifests, options.game, options.map_name, options.feature, options.status,
                       options.failures, options.limit)
        result["manifest"] = str(path.resolve())
        print(json.dumps(result, indent=2))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as exception:
        print(f"Cannot query inventory: {exception}. Run scripts/verify.sh assets or supply --manifest.", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
