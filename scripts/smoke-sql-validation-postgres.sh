#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="postgres:16.8-alpine@sha256:3b057e1c2c6dfee60a30950096f3fab33be141dbb0fdd7af3d477083de94166c"
CONTAINER_NAME="tracemap-sql-validation-$RANDOM-$$"
SCRATCH_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/tracemap-sql-validation-postgres.XXXXXX")"
CONTAINER_STARTED=false

cleanup() {
  unset TRACEMAP_SQL_VALIDATION_CONNECTION || true
  if [[ "$CONTAINER_STARTED" == true ]]; then
    docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  fi
  if [[ -n "$SCRATCH_ROOT" && -d "$SCRATCH_ROOT" && "$SCRATCH_ROOT" == "${TMPDIR:-/tmp}"/tracemap-sql-validation-postgres.* ]]; then
    find "$SCRATCH_ROOT" -depth -delete >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

command -v docker >/dev/null 2>&1 || { echo "error: DockerUnavailable" >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "error: PythonUnavailable" >&2; exit 1; }
docker info >/dev/null 2>&1 || { echo "error: DockerDaemonUnavailable" >&2; exit 1; }

docker run --detach \
  --name "$CONTAINER_NAME" \
  --label tracemap.smoke=sql-validation \
  --env POSTGRES_HOST_AUTH_METHOD=trust \
  --env POSTGRES_DB=validation_fixture \
  --publish 127.0.0.1::5432 \
  "$IMAGE" >/dev/null
CONTAINER_STARTED=true

for _ in $(seq 1 60); do
  if docker exec "$CONTAINER_NAME" pg_isready --dbname validation_fixture --username postgres >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$CONTAINER_NAME" pg_isready --dbname validation_fixture --username postgres >/dev/null

docker exec --interactive "$CONTAINER_NAME" psql --set ON_ERROR_STOP=1 --quiet --username postgres --dbname validation_fixture >/dev/null <<'SQL'
CREATE SCHEMA archive;
CREATE SCHEMA restricted;
CREATE TABLE archive.schema_migrations (version text PRIMARY KEY);
CREATE FUNCTION archive.move_batch(integer) RETURNS integer
LANGUAGE plpgsql
AS 'BEGIN RETURN $1; END';
CREATE ROLE validator LOGIN;
GRANT CONNECT ON DATABASE validation_fixture TO validator;
GRANT USAGE ON SCHEMA archive TO validator;
REVOKE ALL ON FUNCTION archive.move_batch(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION archive.move_batch(integer) TO validator;
SQL

cat >"$SCRATCH_ROOT/pass-plan.json" <<'JSON'
{
  "schemaVersion": "sql-validation-plan/v1",
  "artifactId": "synthetic-postgres-pass",
  "repository": "synthetic/sql-validation-integration",
  "commitSha": "0123456789abcdef0123456789abcdef01234567",
  "observedAt": "2026-07-22T10:00:00Z",
  "expiresAt": "2026-07-22T18:00:00Z",
  "targetContext": {
    "engine": "postgresql",
    "serverRole": "source",
    "databaseRole": "validation-only",
    "schemaRole": "archive",
    "executionMode": "validation-only"
  },
  "checks": [
    { "code": "postgres.server-version-compatible", "expectedMajor": 16 },
    { "code": "postgres.required-extension-available", "identifiers": ["plpgsql"] },
    { "code": "postgres.migration-schema-present", "identifiers": ["archive.schema_migrations"] },
    { "code": "postgres.permission-probe-authorized", "identifiers": ["archive"] },
    { "code": "postgres.archive-function-callable", "identifiers": ["archive.move_batch(integer)"] },
    { "code": "postgres.scheduled-job-registered", "identifiers": ["synthetic archive job"] }
  ]
}
JSON

cat >"$SCRATCH_ROOT/fail-plan.json" <<'JSON'
{
  "schemaVersion": "sql-validation-plan/v1",
  "artifactId": "synthetic-postgres-fail",
  "repository": "synthetic/sql-validation-integration",
  "commitSha": "0123456789abcdef0123456789abcdef01234567",
  "observedAt": "2026-07-22T10:00:00Z",
  "expiresAt": "2026-07-22T18:00:00Z",
  "targetContext": {
    "engine": "postgresql",
    "serverRole": "source",
    "databaseRole": "validation-only",
    "schemaRole": "archive",
    "executionMode": "validation-only"
  },
  "checks": [
    { "code": "postgres.server-version-compatible", "expectedMajor": 17 },
    { "code": "postgres.required-extension-available", "identifiers": ["dblink"] },
    { "code": "postgres.migration-schema-present", "identifiers": ["archive.missing_relation"] },
    { "code": "postgres.permission-probe-authorized", "identifiers": ["restricted"] },
    { "code": "postgres.archive-function-callable", "identifiers": ["archive.missing(integer)"] },
    { "code": "postgres.scheduled-job-registered", "identifiers": ["synthetic archive job"] }
  ]
}
JSON

PORT="$(docker port "$CONTAINER_NAME" 5432/tcp | sed -n 's/^127\.0\.0\.1:\([0-9][0-9]*\)$/\1/p')"
[[ "$PORT" =~ ^[0-9]+$ ]] || { echo "error: LoopbackPortUnavailable" >&2; exit 1; }
export TRACEMAP_SQL_VALIDATION_CONNECTION="Host=127.0.0.1;Port=$PORT;Username=validator;Database=validation_fixture;SSL Mode=Disable"

dotnet build "$ROOT/src/dotnet/TraceMap.SqlValidation.Cli/TraceMap.SqlValidation.Cli.csproj" --nologo >/dev/null
for suffix in a b; do
  dotnet run --project "$ROOT/src/dotnet/TraceMap.SqlValidation.Cli" --no-build -- validate \
    --plan "$SCRATCH_ROOT/pass-plan.json" \
    --connection-env TRACEMAP_SQL_VALIDATION_CONNECTION \
    --out "$SCRATCH_ROOT/pass-$suffix.json" >/dev/null
done
dotnet run --project "$ROOT/src/dotnet/TraceMap.SqlValidation.Cli" --no-build -- validate \
  --plan "$SCRATCH_ROOT/fail-plan.json" \
  --connection-env TRACEMAP_SQL_VALIDATION_CONNECTION \
  --out "$SCRATCH_ROOT/fail.json" >/dev/null
unset TRACEMAP_SQL_VALIDATION_CONNECTION

cmp --silent "$SCRATCH_ROOT/pass-a.json" "$SCRATCH_ROOT/pass-b.json"

python3 - "$SCRATCH_ROOT/pass-a.json" "$SCRATCH_ROOT/fail.json" <<'PY'
import json
import re
import sys

def require(condition, classification):
    if not condition:
        raise SystemExit(classification)

PASS = {
    "postgres.archive-function-callable": "observed-pass",
    "postgres.archive-link-connectivity": "not-run",
    "postgres.cleanup-probe-observed": "not-run",
    "postgres.migration-schema-present": "observed-pass",
    "postgres.permission-probe-authorized": "observed-pass",
    "postgres.required-extension-available": "observed-pass",
    "postgres.scheduled-job-registered": "observed-fail",
    "postgres.server-version-compatible": "observed-pass",
    "postgres.target-schema-compatible": "not-run",
    "postgres.validation-query-expected-shape": "not-run",
}
FAIL = {code: ("not-run" if status == "not-run" else "observed-fail") for code, status in PASS.items()}
for path, expected, artifact_id in (
    (sys.argv[1], PASS, "synthetic-postgres-pass"),
    (sys.argv[2], FAIL, "synthetic-postgres-fail"),
):
    with open(path, encoding="utf-8") as handle:
        document = json.load(handle)
    actual = {entry["code"]: entry["status"] for entry in document["assertions"]}
    require(actual == expected, "unexpected assertion projection")
    require(document["schemaVersion"] == "sql-validation-summary/v1", "unexpected summary schema")
    require(document["artifactId"] == artifact_id, "unexpected artifact identity")
    require(document["validator"] == {"id": "tracemap.sql-validation-harness", "version": "1.0.0"}, "unexpected validator identity")
    require(document["publicClaimLevel"] == "public-safe", "unexpected public claim level")
    require(re.fullmatch(r"[0-9a-f]{64}", document["artifact"]["digest"]) is not None, "invalid artifact digest")
    serialized = json.dumps(document, sort_keys=True).lower()
    for forbidden in (
        "schema_migrations", "move_batch", "missing_relation", "synthetic archive job",
        "127.0.0.1", "localhost", "validation_fixture", "password", "server=",
        "user id=", "username=", "host=", "port=", "database=", "connectionstring",
    ):
        require(forbidden not in serialized, "public-safe projection leak")
PY

echo "SQL validation PostgreSQL integration smoke passed."
