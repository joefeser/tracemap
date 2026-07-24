CREATE TABLE archive.records (
  id bigint,
  archive_key text,
  source_id bigint,
  CONSTRAINT records_pkey PRIMARY KEY (id),
  CONSTRAINT records_archive_key_unique UNIQUE (archive_key),
  CONSTRAINT records_source_fk FOREIGN KEY (source_id) REFERENCES archive.sources (id)
);

CREATE UNIQUE INDEX records_archive_key_idx
  ON archive.records USING btree (archive_key);
