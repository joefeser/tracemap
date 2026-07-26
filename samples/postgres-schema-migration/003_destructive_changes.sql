ALTER TABLE archive_fixture.records RENAME COLUMN retention_state TO archive_state;

ALTER TABLE archive_fixture.records DROP COLUMN IF EXISTS legacy_payload RESTRICT;

ALTER TABLE archive_fixture.records RENAME TO archived_records;

DROP TABLE IF EXISTS archive_fixture.retired_records CASCADE;
