CREATE EXTENSION pg_cron;
SELECT cron.schedule('fixture-job', '0 1 * * *', $$select fixture_archive_work$$);
