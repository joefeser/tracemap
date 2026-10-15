"""Read one retained IL body by an exact body or body-linked fact ID.

Console-only diagnostic; no graph admission or database classification claims.
Default output contains counts only. --local-details prints private IL identities.
"""
import argparse
import hashlib
from contextlib import closing
import json
from pathlib import Path
import sqlite3


def inspect(path, fact_id, graph_path=None):
    graph = None
    if graph_path is not None:
        with Path(graph_path).open("rb") as stream:
            encoded = stream.read(16 * 1024 * 1024 + 1)
        if len(encoded) > 16 * 1024 * 1024:
            raise ValueError("Graph size bound exceeded")
        document = json.loads(encoded)
        with Path(path).open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if document.get("schemaVersion") != "retained-method-graph.v1" or document.get("indexSha256") != digest:
            raise ValueError("Graph does not identify these exact index bytes")
        graph = document["graph"]
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
        rows = db.execute("""SELECT combined_fact_id, fact_type, properties_json FROM combined_facts
            WHERE source_index_id=? AND json_extract(properties_json,'$.ilBodyFactId')=?
            ORDER BY combined_fact_id LIMIT 1025""", (body["source_index_id"], body_id)).fetchall()
        if len(rows) > 1024:
            raise ValueError("Body-linked row limit exceeded; counts unavailable")
        calls = []
        call_ids = set()
        counts = dict(calls=0, value_observations=0, command_candidates=0, gaps=0)
        names = {"ManagedIlCallObserved": "calls", "ManagedIlCallValuesObserved": "value_observations",
                 "ManagedIlDatabaseCommandCandidate": "command_candidates", "AnalysisGap": "gaps"}
        for row in rows:
            kind = row["fact_type"]
            if kind in names:
                counts[names[kind]] += 1
            if kind == "ManagedIlCallObserved":
                call_ids.add(row["combined_fact_id"])
                call = json.loads(row["properties_json"])
                if not body_props.get("rawFileSha256") or call.get("rawFileSha256") != body_props["rawFileSha256"]:
                    raise ValueError("Call/body artifact hash missing or mismatched")
                calls.append((int(call["ilOffset"]), call["opcode"], call["targetIdentity"]))
        if graph is not None:
            exact_calls = [call for call in graph["calls"] if call["bodyFactId"] == body["combined_fact_id"]]
            graph_ids = {call["factId"] for call in exact_calls}
            if len(graph_ids) != len(exact_calls) or not graph_ids.issubset(call_ids):
                raise ValueError("Graph call identities inconsistent with exact body")
            counts["exact_body_calls_in_graph"] = len(graph_ids)
            counts["exact_body_calls_outside_graph"] = len(call_ids - graph_ids)
            owner_id = body_props.get("compiledFactId")
            owners = db.execute("""SELECT combined_fact_id, properties_json, target_symbol FROM combined_facts
                WHERE source_index_id=? AND original_fact_id=?
                AND fact_type='ManagedMethodDeclared' LIMIT 2""", (body["source_index_id"], owner_id)).fetchall()
            counts["exact_body_owner_matches"] = len(owners)
            counts["exact_body_owner_nodes"] = 0
            counts["exact_body_owner_symbol_nodes"] = 0
            counts["explicit_bridges_to_exact_owner"] = 0
            counts["owner_symbol_nodes_in_other_indexes"] = 0
            if len(owners) == 1:
                owner = owners[0]
                owner_props = json.loads(owner["properties_json"])
                counts["owner_artifact_hash_matches"] = int(bool(body_props.get("rawFileSha256")) and
                    owner_props.get("rawFileSha256") == body_props["rawFileSha256"])
                counts["exact_body_owner_nodes"] = sum(owner["combined_fact_id"] == node["combinedFactId"] for node in graph["nodes"])
                # Symbol nodes created by GetOrAddSymbolNode intentionally have
                # no representative declaration fact. Match the graph's actual
                # identity pair, never the display label or a method name.
                owner_nodes = {node["nodeId"] for node in graph["nodes"]
                    if owner["target_symbol"] and node["sourceIndexId"] == body["source_index_id"]
                    and node["symbolId"] == owner["target_symbol"]}
                counts["exact_body_owner_symbol_nodes"] = len(owner_nodes)
                counts["owner_symbol_nodes_in_other_indexes"] = sum(
                    bool(owner["target_symbol"]) and node["sourceIndexId"] != body["source_index_id"]
                    and node["symbolId"] == owner["target_symbol"] for node in graph["nodes"])
                counts["explicit_bridges_to_exact_owner"] = sum(
                    edge["edgeKind"] == "explicit-reference-member-candidate" and edge["toNodeId"] in owner_nodes
                    for edge in graph.get("edges", []))
        return counts, sorted(calls)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", required=True)
    parser.add_argument("--fact-id", required=True)
    parser.add_argument("--graph", help="Existing method-graph.local.json; must hash-match this index")
    parser.add_argument("--local-details", action="store_true")
    args = parser.parse_args()
    try:
        counts, calls = inspect(args.index, args.fact_id, args.graph)
    except (OSError, sqlite3.Error, ValueError, KeyError, TypeError):
        parser.exit(2, "Inspection unavailable: invalid/ambiguous evidence, schema, hash, or work bound. No zero-count conclusion.\n")
    print("Exact body found; compiled calls use ManagedIlCallObserved, not CallEdge.")
    for name, count in counts.items():
        print(f"{name}: {count}")
    print("Counts do not establish graph admission, database classification or runtime reachability.")
    if args.graph:
        print("exact_body_owner_matches counts index owner joins, NOT graph identity matches.")
        print("exact_body_owner_nodes counts representative declaration IDs only; zero does NOT mean absent.")
        print("exact_body_owner_symbol_nodes uses exact source-index and symbol identity; no name matching.")
    if args.local_details:
        print("PRIVATE LOCAL DETAILS — do not upload:")
        for offset, opcode, target in calls:
            print(f"{offset}: {opcode} -> {target}")


if __name__ == "__main__":
    main()
