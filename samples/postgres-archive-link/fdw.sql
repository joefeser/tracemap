-- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
CREATE EXTENSION postgres_fdw;

-- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=fdw-server-setup capabilities=create-server stops=verify-active-connection
CREATE SERVER fixture_remote FOREIGN DATA WRAPPER postgres_fdw;

-- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
CREATE USER MAPPING FOR fixture_role SERVER fixture_remote
OPTIONS (password '${FIXTURE_REMOTE_PASSWORD}');

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection
IMPORT FOREIGN SCHEMA public FROM SERVER fixture_remote INTO archive;
