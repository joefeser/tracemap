# DNN SQL validation controls

Independently authored C#/VB caller/wrapper and SQL fixtures for #908. Never run
the application methods; scanner tests only. No upstream DNN code is vendored.

See [the bounded source/expectation matrix and optional runbook](../../docs/validation/DNN_SQL_VALIDATION.md).
The optional container lane is **NOT RUN**. `image-resolution.json` verifies
public registry metadata only, with generator and projected-input provenance.
No schema comparison, DNN runtime acceptance or deployed parity is claimed.
