CREATE TABLE audit_events (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL
);

CREATE INDEX ix_audit_events_name ON audit_events(name);
