-- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=publication-setup capabilities=create-publication stops=verify-active-connection
CREATE PUBLICATION fixture_publication FOR TABLE fixture_events;

-- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=subscription-setup capabilities=create-subscription stops=secret-owner-review,verify-active-connection
CREATE SUBSCRIPTION fixture_subscription
CONNECTION '${FIXTURE_SUBSCRIPTION_CONNECTION}'
PUBLICATION fixture_publication;
