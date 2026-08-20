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
- `python-lock-fixture/`: PR4 Python `uv.lock` fixture (a root project with a
  direct `requests`, a direct `flask`, and a transitive `urllib3`) plus
  `decision-python.json`. The revoke record's sha256 digest deliberately equals
  the lockfile's synthetic source-distribution hash: uv.lock/poetry.lock hashes
  are wheel/sdist artifact-form specific, so they are never emitted as artifact
  digests and correlation stays possible-only.
- `gradle-lock-fixture/`: PR4 Gradle `gradle.lockfile` fixture plus
  `decision-gradle.json`. gradle.lockfile provides resolved versions only; the
  reject record's sha256 digest still correlates possible-only because the
  format and the decision record cannot prove the same artifact form.
