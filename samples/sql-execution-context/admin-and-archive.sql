-- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
CREATE EXTENSION IF NOT EXISTS postgres_fdw;

CREATE SERVER archive_source FOREIGN DATA WRAPPER postgres_fdw;

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection,verify-database-context
IMPORT FOREIGN SCHEMA public FROM SERVER archive_source INTO archive;

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=validation-only step=validation-query capabilities=validate-state stops=verify-active-connection
SELECT count(*) FROM archive.example_records;
