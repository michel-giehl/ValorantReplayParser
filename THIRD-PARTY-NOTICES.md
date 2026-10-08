# Third-party notices

## vrfkit

The VALORANT payload transforms for releases 11.06 through 12.09 in
`src/Replay.Encoding/PayloadEncryption/VersionedTransforms/` and the native
reference vectors in
`tests/Replay.Encoding.Tests/PayloadEncryption/ValorantLegacyTransformTests.Vectors.cs`
are ported from [vrfkit v0.2.0](https://github.com/yakisoba0728/vrfkit/tree/b85e7f954bfe6d315f7a2a1b0d44713bc3d2519f)
(commit `b85e7f954bfe6d315f7a2a1b0d44713bc3d2519f`).

The shared `InitialPrngA` helper is also ported from that release.
The Rust transform code has been translated to C# and uses this project's archive
and transform helpers. Native expected outputs are preserved.

Copyright (c) 2026 vrfkit contributors.
Used under the MIT License; see [third-party/vrfkit-LICENSE](third-party/vrfkit-LICENSE).

## ValCoach

The China 13.05 payload transform in
`src/Replay.Encoding/PayloadEncryption/VersionedTransforms/ValorantSeededTransformChina13_05.cs`
and its recovered vectors in
`tests/Replay.Encoding.Tests/PayloadEncryption/ValorantSeededTransformTests.cs`
are ported from [ValCoach's production patch](https://github.com/STarRyx727/ValCoach/blob/62708dc31d5beac89c588a04502303eade77fbe6/patches/valorant_parser_valcoach_profile.patch)
(commit `62708dc31d5beac89c588a04502303eade77fbe6`).

Copyright (c) 2026 ValCoach contributors.
Used under the MIT License; see [third-party/ValCoach-LICENSE](third-party/ValCoach-LICENSE).
