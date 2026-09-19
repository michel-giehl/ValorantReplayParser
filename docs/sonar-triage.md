# SonarCloud issue triage

## Analysis status

The SonarCloud view inspected on 2026-09-19 showed 64 unresolved code smells. Its new-code quality gate failed only on duplication density: 3.1% against a 3.0% threshold. That analysis revision differs from the local checkout, so rerun analysis after the release changes before comparing issue counts or deciding whether the gate passes.

The repository currently has no SonarScanner workflow or tracked Sonar analysis properties file. The active SonarCloud analysis mode and project-settings access have not been verified in this implementation. If the project uses automatic analysis, apply this single duplication exclusion in the SonarCloud project settings:

```text
sonar.cpd.exclusions=src/Replay.Encoding/PayloadEncryption/VersionedTransforms/**/*.cs
```

Do not add these files to `sonar.exclusions`: Sonar correctness and security analysis must continue to inspect the transforms, and the transform tests must remain analyzed. Do not add an ignored local properties file or create a second analysis pipeline to work around automatic analysis. If analysis later becomes scanner-driven, put the same property in that actual scanner configuration.

**Pending external action:** the duplication exclusion must still be applied in SonarCloud project settings if automatic analysis is active. This repository change documents the setting but does not change SonarCloud or claim that its gate is fixed.

## Triage decisions

| Issue or suggestion | Decision |
| --- | --- |
| S2139: log and rethrow | Fix through contextual exceptions. Do not log and then rethrow the same parse failure. |
| S3881: disposal | Fix deterministic ownership, idempotent disposal, and use-after-dispose behavior for owned resources. |
| S108: empty catches in speculative decoders | Preserve intentional speculative fallback, capture the fallback reason, and report a structured diagnostic. Do not rethrow optional speculative reads only to silence this rule. |
| S3871: private layout-control exceptions | Keep these exceptions internal and verify they cannot escape the speculative decoder to the public reader. Treat their private implementation as accepted by design; do not expand the public API for the rule. |
| Transform naming and duplication | Keep the existing per-version implementations and patch separators. Their similarity is intentional; do not cosmetically rewrite the transform bodies. Exclude only these transform source files from duplication detection. |
| LINQ suggestions, naming, literal extraction, parameter count, and cognitive complexity | Not release blockers. Do not rewrite hot paths or split code solely to satisfy thresholds. |

The duplication exclusion is deliberately narrow: `sonar.cpd.exclusions` disables only copy/paste detection for the versioned transform files. All other Sonar analysis for those files remains enabled.
