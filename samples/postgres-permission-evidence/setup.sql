CREATE EXTENSION postgres_fdw;
CREATE SERVER PRIVATE_SERVER_SENTINEL FOREIGN DATA WRAPPER postgres_fdw;

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
GRANT USAGE ON FOREIGN SERVER PRIVATE_SERVER_SENTINEL TO PRIVATE_ROLE_SENTINEL;
-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
GRANT CREATE, USAGE ON SCHEMA PRIVATE_SCHEMA_SENTINEL TO PRIVATE_ROLE_SENTINEL;

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
CREATE USER MAPPING FOR PRIVATE_ROLE_SENTINEL SERVER PRIVATE_SERVER_SENTINEL
OPTIONS (password '${FIXTURE_PASSWORD}');

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection
IMPORT FOREIGN SCHEMA public FROM SERVER PRIVATE_SERVER_SENTINEL INTO PRIVATE_SCHEMA_SENTINEL;

ALTER TABLE fixture_table OWNER TO PRIVATE_ROLE_SENTINEL;
ALTER DEFAULT PRIVILEGES GRANT SELECT ON TABLES TO PRIVATE_ROLE_SENTINEL;
GRANT fixture_parent_role TO PRIVATE_ROLE_SENTINEL;
