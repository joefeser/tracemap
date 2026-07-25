-- tracemap-postgres-schema-snapshot: v1

CREATE TABLE archive_fixture.snapshot_records (
  id bigint,
  archived_at timestamp
);

CREATE INDEX snapshot_records_archived_idx
ON archive_fixture.snapshot_records (archived_at);

CREATE SEQUENCE archive_fixture.snapshot_sequence;
