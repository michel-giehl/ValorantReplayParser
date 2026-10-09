# SonarCloud issue triage

## Analysis status

The SonarCloud analysis inspected on 2026-10-09 covers release commit `d05038eef424a943b8da3cf9c8b9db49e032dd86`. It reports 90 unresolved code smells. The new-code quality gate fails only on duplication density: 4.4% against a 3.0% threshold. Reliability, security, maintainability, and security-hotspot review conditions pass.

The project uses automatic .NET analysis (`autoscanEnabled=true`, `ciName=Autoscan for DotNet`). The tracked root `.sonarcloud.properties` file configures this single duplication exclusion:

```text
sonar.cpd.exclusions=src/Replay.Encoding/PayloadEncryption/VersionedTransforms/**/*.cs
```

Do not add these files to `sonar.exclusions`: Sonar correctness and security analysis must continue to inspect the transforms, and the transform tests must remain analyzed. `.sonarcloud.properties` is the [supported configuration file for automatic analysis](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/automatic-analysis#additional-analysis-configuration); `sonar-project.properties` is ignored in this mode. Do not create a second analysis pipeline. If analysis later becomes scanner-driven, put the same property in that actual scanner configuration.

The two CLI tools now compile one shared Serilog logger provider instead of maintaining identical copies. The byte archive reader also calls its base disposal implementation; the field binding helper is static, and a redundant null-forgiving operator was removed.

**Pending validation:** after these local changes are pushed, automatic analysis must confirm the new duplication density and quality-gate result. Local tests and builds cannot establish that the remote gate passes.

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
