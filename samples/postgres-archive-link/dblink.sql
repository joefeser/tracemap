CREATE EXTENSION dblink;
SELECT dblink('${FIXTURE_DBLINK_CONNECTION}', 'select fixture_archive_work');
