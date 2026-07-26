CREATE TYPE archive_fixture.retention_state AS ENUM ('ready', 'archived');

CREATE FUNCTION archive_fixture.move_batch(batch_size integer)
RETURNS integer
LANGUAGE sql
AS $$ SELECT batch_size $$;

CREATE PROCEDURE archive_fixture.refresh_archive()
LANGUAGE sql
AS $$ SELECT 1 $$;
