# Site: Swift API-client evidence article

Status: implemented
Public claim level: demo

## Intent

Publish a public site article that explains why Swift API-client evidence matters for reviewers who need app-to-backend context without pretending TraceMap proved runtime behavior.

## Requirements

- Add a public blog article for the Swift API-client evidence walkthrough.
- Use the title "How TraceMap Reads Swift API Clients Without Pretending They Ran".
- Explain the problem: mobile apps can hide backend dependencies in client code.
- Explain that TraceMap surfaces static Swift API-client candidates.
- Cover the four supported evidence shapes: URLSession, URLRequest, Alamofire, and Moya-style patterns.
- Explain how to read a row through rule ID, evidence tier, coverage label, limitation, and non-claim.
- Connect the article to review, migration planning, endpoint inventory, and change-risk conversations.
- Link to the Swift API-client walkthrough, Swift surface discovery, real-world smoke proof, and claim-language checklist.
- Keep the article at a conservative demo claim level.
- Update sitemap/discovery metadata through the existing site build flow.
- Add focused validation so the article cannot drift into runtime or AI impact-analysis claims.

## Boundaries

- Do not claim endpoint reachability.
- Do not claim backend compatibility.
- Do not claim request success.
- Do not claim auth flow correctness.
- Do not claim production traffic.
- Do not claim API correctness.
- Do not claim runtime tracing.
- Do not call this AI impact analysis.
- Do not mention LLMs, embeddings, or vector databases as part of TraceMap core scanning.

## Source Material

- `/swift/api-client-walkthrough/`
- `/swift/`
- `/swift/story/`
- `/swift/surface-discovery/`
- `/swift/real-world-smoke/`
- `/swift/claim-language/`
- GitHub issue #441
- PR #449
