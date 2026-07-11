-- Public-safe fixture: all apparent values are placeholders, never credentials.
CREATE USER MAPPING FOR fixture_operator
SERVER fixture_archive
OPTIONS (user '${FIXTURE_REMOTE_USER}', password '${FIXTURE_REMOTE_PASSWORD}');

CREATE SUBSCRIPTION fixture_subscription
CONNECTION '${FIXTURE_SUBSCRIPTION_CONNECTION}'
PUBLICATION fixture_publication;

SELECT dblink('${FIXTURE_DBLINK_CONNECTION}', 'select 1');

SELECT cron.schedule(
  'fixture-validation',
  '0 1 * * *',
  $$select current_setting('fixture.secret_reference')$$
);
