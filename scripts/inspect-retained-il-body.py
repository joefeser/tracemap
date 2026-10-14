"""Read one retained IL body by an exact body or body-linked fact ID.

Console-only diagnostic; no graph admission or database classification claims.
Default output contains counts only. --local-details prints private IL identities.
"""
import argparse
from contextlib import closing
import json
from pathlib import Path
import sqlite3


def inspect(path, fact_id):
    with closing(sqlite3.connect(Path(path).resolve().as_uri() + "?mode=ro", uri=True)) as db:
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA query_only=ON")
        # Bound VM work as well as returned rows; never silently return partial counts.
        ticks = 0
        def progress():
            nonlocal ticks
            ticks += 1
            return int(ticks > 10000)
        db.set_progress_handler(progress, 1000)
        rows = db.execute("""SELECT * FROM combined_facts
            WHERE combined_fact_id=? OR original_fact_id=? LIMIT 2""", (fact_id, fact_id)).fetchall()
        if len(rows) != 1:
            raise ValueError("Fact selector absent or ambiguous; use exact combined_fact_id")
        anchor = rows[0]
        props = json.loads(anchor["properties_json"])
        body_id = anchor["original_fact_id"] if anchor["fact_type"] == "ManagedIlBodyDeclared" else props.get("ilBodyFactId")
        if not body_id:
            raise ValueError("Selected fact has no explicit IL body link")
        bodies = db.execute("""SELECT * FROM combined_facts WHERE source_index_id=?
            AND original_fact_id=? AND fact_type='ManagedIlBodyDeclared' LIMIT 2""",
            (anchor["source_index_id"], body_id)).fetchall()
        if len(bodies) != 1:
            raise ValueError("Exact source-index-local body absent or ambiguous")
        body = bodies[0]
        body_props = json.loads(body["properties_json"])
        rows = db.execute("""SELECT fact_type, properties_json FROM combined_facts
            WHERE source_index_id=? AND json_extract(properties_json,'$.ilBodyFactId')=?
            ORDER BY combined_fact_id LIMIT 1025""", (body["source_index_id"], body_id)).fetchall()
        if len(rows) > 1024:
            raise ValueError("Body-linked row limit exceeded; counts unavailable")
        calls = []
        counts = dict(calls=0, value_observations=0, command_candidates=0, gaps=0)
        names = {"ManagedIlCallObserved": "calls", "ManagedIlCallValuesObserved": "value_observations",
                 "ManagedIlDatabaseCommandCandidate": "command_candidates", "AnalysisGap": "gaps"}
        for row in rows:
            kind = row["fact_type"]
            if kind in names:
                counts[names[kind]] += 1
            if kind == "ManagedIlCallObserved":
                call = json.loads(row["properties_json"])
                if not body_props.get("rawFileSha256") or call.get("rawFileSha256") != body_props["rawFileSha256"]:
                    raise ValueError("Call/body artifact hash missing or mismatched")
                calls.append((int(call["ilOffset"]), call["opcode"], call["targetIdentity"]))
        return counts, sorted(calls)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", required=True)
    parser.add_argument("--fact-id", required=True)
    parser.add_argument("--local-details", action="store_true")
    args = parser.parse_args()
    try:
        counts, calls = inspect(args.index, args.fact_id)
    except (sqlite3.Error, ValueError, KeyError, TypeError):
        parser.exit(2, "Inspection unavailable: invalid/ambiguous evidence, schema, hash, or work bound. No zero-count conclusion.\n")
    print("Exact body found; compiled calls use ManagedIlCallObserved, not CallEdge.")
    for name, count in counts.items():
        print(f"{name}: {count}")
    print("Counts do not establish graph admission, database classification or runtime reachability.")
    if args.local_details:
        print("PRIVATE LOCAL DETAILS — do not upload:")
        for offset, opcode, target in calls:
            print(f"{offset}: {opcode} -> {target}")


if __name__ == "__main__":
    main()
