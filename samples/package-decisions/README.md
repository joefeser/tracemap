# Package decision fixtures

These synthetic, offline fixtures exercise the `package-decision.v1` reader and
the PR2/PR3 adapter evidence composition. They contain only safe example
identities and no registry responses, credentials, customer data, or package
content.

- `possible-admit.json`, `quarantine.json`: PR1 reader fixtures.
- `npm-lock-fixture/`: PR2 npm lockfile evidence fixture.
- `portfolio-pr2.example.json`: PR2 combined/portfolio input example.
- `nuget-lock-fixture/`: PR3 NuGet `packages.lock.json` fixture (a direct and a
  transitive entry across two target frameworks) plus `decision-nuget.json`.
  The lockfile `contentHash` values are deliberately fake: NuGet lockfiles
  never yield an artifact digest, so correlation stays possible-only.
- `swift-possible.json`: PR3 Swift decision records matched against
  `samples/swift-dependency-surfaces` scan output (SwiftPM `alamofire` pin and
  CocoaPods `Alamofire` pod). Swift evidence never yields an artifact digest,
  so correlation stays possible-only.
