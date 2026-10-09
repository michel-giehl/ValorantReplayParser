# Viper Pit recorded wire fixtures

Source: user-provided replay `6cc2a4f5-2a73-4e69-a67a-8d1f1c560560.vrf`,
VALORANT release 13.06, SHA-256
`0811ff4c721afb017b0534dd81687dd8aa080ea115abc7a9a0a67cdea44ce718`.

`cast-1.bin` through `cast-8.bin` are complete, decrypted GroundVolume component
RepLayout payloads in cast order. Exact valid bit lengths are in the descriptor
test cases; unused high bits of each final byte are padding. They contain the
regular properties followed by the native fragment fast-array delta.

`packet-2069.bin` through `packet-2078.bin` are raw packets carrying the first
Pit volume. They exercise the extra VALORANT flag before the partial-initial and
partial-final flags. They complete one 148,453-bit bunch without a partial error.

These contain geometry and replication IDs only; the full replay is not included.
