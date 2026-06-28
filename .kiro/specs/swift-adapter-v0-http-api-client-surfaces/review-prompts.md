# Swift Adapter v0 HTTP And API Client Surfaces Review Prompts

## Opus Review

Review `.kiro/specs/swift-adapter-v0-http-api-client-surfaces/` for merge
readiness as a TraceMap Kiro spec.

Focus on correctness, implementability, safety, and whether the spec avoids
runtime overclaiming.

Questions:

1. Is the scope small enough for one normal Feature Delivery Loop?
2. Are URLSession/URLRequest/Foundation patterns specified narrowly enough?
3. Are Alamofire and Moya patterns useful without requiring package restore?
4. Are raw URL/host/query/header/body values safely omitted or hashed?
5. Are dynamic URL/method/path cases gaps rather than inferred evidence?
6. Are rule IDs and limitations complete?
7. Are report/shared-reader requirements precise enough?
8. Are validation commands sufficient?
9. What is blocking before merge?

Return blocking issues, important non-blocking issues, suggested fixes, and
whether the spec is ready to implement.

## Sonnet Review

Review `.kiro/specs/swift-adapter-v0-http-api-client-surfaces/` for practical
implementation readiness.

Check whether the requirements, design, tasks, and implementation-state files
give an implementation worker enough detail to build the slice without
overclaiming runtime behavior.

Return:

- Blocking issues.
- Medium issues.
- Missing tests.
- Suggested scope cuts.
- Merge readiness.

