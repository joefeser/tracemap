# SQL Validation PostgreSQL Integration Requirements

## Goal

Validate the shipped SQL validation harness against a real, disposable
PostgreSQL 16.8 server without connecting to operator or production systems.

## Requirements

1. The smoke SHALL use the official PostgreSQL 16.8 Alpine image pinned by
   digest and expose it only on a random loopback port.
2. The smoke SHALL use a synthetic, ephemeral database with no host volume and
   SHALL remove the container and scratch artifacts on success or failure.
3. The fixture SHALL exercise the six bounded v1 catalog probes without
   executing the fixture function, a scheduled job, migrations, or arbitrary
   operator SQL through the harness.
4. The smoke SHALL prove observed-pass, observed-fail, and not-run outcomes and
   byte-for-byte deterministic output for identical inputs.
5. The smoke SHALL prove identifiers, connection material, and fixture target
   details do not enter the public-safe summary.
6. This validation SHALL NOT claim production state, continuing state, safe
   execution, successful jobs, release approval, or DBA/operator attestation.
