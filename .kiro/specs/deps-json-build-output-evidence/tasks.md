# Build-output dependency evidence

- [x] Reconcile current dev and open #847; isolate implementation from pending snapshot-guard work.
- [x] Verify actual deps.json targets/library-key format and document limits of graph relations.
- [x] Add opt-in CLI/Core option and separate bounded bin discovery; retain source snapshot exclusions.
- [x] Add typed package filtering, deterministic per-file/target identities, exact generator/input hashes and unknown build freshness.
- [x] Add valid, malformed, ambiguous, duplicate, limit, scope and snapshot regression cases.
- [ ] Complete focused and full Debug validation and sample CLI smoke.
- [ ] Deliver bounded PR to dev and complete live ACK review.
