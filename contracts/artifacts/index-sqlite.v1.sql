-- Canonical minimum TraceMap adapter index contract.
-- Adapters may add compatible tables and columns, but these columns must exist.
create table scan_manifest (
  scan_id text primary key,
  repo text not null,
  commit_sha text not null,
  scanner_version text not null,
  scanned_at text not null,
  analysis_level text not null,
  build_status text not null,
  manifest_json text not null
);

create table facts (
  fact_id text primary key,
  scan_id text not null,
  repo text not null,
  commit_sha text not null,
  project_path text,
  fact_type text not null,
  rule_id text not null,
  evidence_tier text not null,
  source_symbol text,
  target_symbol text,
  contract_element text,
  file_path text not null,
  start_line integer not null,
  end_line integer not null,
  snippet_hash text,
  extractor_id text,
  extractor_version text,
  properties_json text not null
);
