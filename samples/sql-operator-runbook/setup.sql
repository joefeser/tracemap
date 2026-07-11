-- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
CREATE EXTENSION postgres_fdw;
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=fdw-server-setup capabilities=create-server stops=verify-active-connection
CREATE SERVER fixture_archive_link FOREIGN DATA WRAPPER postgres_fdw OPTIONS (host 'private-host-leak-sentinel.invalid');
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
GRANT USAGE ON FOREIGN SERVER fixture_archive_link TO fixture_operator;
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
CREATE USER MAPPING FOR fixture_operator SERVER fixture_archive_link OPTIONS (password 'private-password-leak-sentinel');
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection
IMPORT FOREIGN SCHEMA public FROM SERVER fixture_archive_link INTO archive;
-- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
CREATE EXTENSION pg_cron;
-- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=publication-setup capabilities=create-publication stops=owner-review,verify-active-connection
CREATE PUBLICATION fixture_archive_publication FOR TABLE fixture_events;
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=scheduled step=scheduled-job capabilities=schedule-job stops=owner-review
SELECT cron.schedule('fixture-job', '0 1 * * *', $$select 'raw-scheduled-command-leak-sentinel'$$);
-- tracemap-sql-context: engine=postgresql server=archive-target database=validation-only schema=archive mode=validation-only step=validation-query capabilities=validate-state stops=verify-active-connection
SELECT count(*) FROM archive.fixture_events;
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=destructive-operation capabilities=destructive-operation-review stops=owner-review,verify-active-connection
DROP SERVER fixture_archive_link;
