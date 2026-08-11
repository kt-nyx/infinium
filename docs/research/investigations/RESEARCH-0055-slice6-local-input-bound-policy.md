# RESEARCH-0055: Slice 6 repository-local input-bound policy

Status: Completed

Disposition: Exact owner-approved WP1 policy implemented on 2026-08-11 under
the conservative-bound alternative already accepted by ADR-0023 and the
accepted Slice 6 plan; no plan amendment or new ADR is required

Date: 2026-08-11

Last reviewed: 2026-08-11

Decision enabled: M1/S6/WP1 local input-bound closure without a provider
token-count request

## Question and requirements

Can Infinium prove the accepted Slice 6 input-token ceilings entirely offline
over each exact canonical Responses request, using an existing maintained
tokenizer while conservatively covering provider-only structural framing?

The accepted plan requires either an exact local tokenizer or canonical UTF-8
bytes plus a fixed structural-token margin. ADR-0023 requires a qualified
finite bound over the exact immutable request. RESEARCH-0054 forbids silently
adding the provider input-token-count endpoint to admission.

## Scope and non-scope

This investigation covers only the WP1 contract and offline proof shape. It
does not authorize a credential operation, helper execution, network request,
provider token-count call, dispatch, reservation coordinator, settlement, or
live qualification. It does not claim that local tokenizer output is the
provider's exact billed input count.

## Sources and exact versions

- Microsoft, [Use Microsoft.ML.Tokenizers for text tokenization](https://learn.microsoft.com/dotnet/ai/how-to/use-tokenizers),
  retrieved 2026-08-11. Microsoft documents the tokenizer package, the
  separate `O200kBase` data package, token-ID encoding/counting, and local
  construction.
- NuGet, [Microsoft.ML.Tokenizers 2.0.0](https://www.nuget.org/packages/Microsoft.ML.Tokenizers/2.0.0),
  retrieved 2026-08-11. The exact package is MIT, identifies Microsoft as
  owner, and records the source repository.
- NuGet, [Microsoft.ML.Tokenizers.Data.O200kBase 2.0.0](https://www.nuget.org/packages/Microsoft.ML.Tokenizers.Data.O200kBase/2.0.0),
  retrieved 2026-08-11. The exact MIT package carries the `o200k_base`
  vocabulary asset used by the tokenizer.
- OpenAI, [tiktoken](https://github.com/openai/tiktoken), retrieved
  2026-08-11. The primary implementation documents `o200k_base`, reversible
  byte-pair encoding, and the ordinary-text tokenizer boundary.
- OpenAI, [Counting tokens](https://developers.openai.com/api/docs/guides/token-counting),
  rechecked through RESEARCH-0054. Provider counting includes provider framing
  that an ordinary local tokenizer may not observe; it remains excluded from
  WP1 admission.
- Microsoft, [.NET denial-of-service advisory CVE-2026-26127](https://github.com/advisories/GHSA-73j8-2gch-69rq),
  retrieved 2026-08-11. `Microsoft.Bcl.Memory` 9.0.4, declared by the data
  package, is affected; 9.0.14 is the patched 9.x version.

The NuGet lock identities are:

- `Microsoft.ML.Tokenizers/2.0.0`, content hash
  `+b8lT4cLLO/sBR2hjvE/qG6qrZG15h7/PBvnIrzTh4xDaAxdHUY6449rC+1pHzQUsBiCHZVbj+VMn+xS0sL7TA==`;
- `Microsoft.ML.Tokenizers.Data.O200kBase/2.0.0`, content hash
  `19G0KWrRnUZmc8vGdPNuBJqTruhAjzPLRY2nn6a/HiBXbEnE/Lx9L223jGlDzg1oAcCggo/8GlWw3ZLVuS76Ow==`;
  and
- the security override `Microsoft.Bcl.Memory/9.0.14`, content hash retained in
  the committed lockfiles.

All three packages declare MIT. The tokenizer and vocabulary packages identify
source commit `efefa92f4486a43047c5b47618885a71bf7f0967`; the patched memory
package identifies runtime commit `19c07820cb72aafc554c3bc8fe3c54010f5123f0`.

## Experiments and artifacts

The implementation constructs
`TiktokenTokenizer.CreateForEncoding("o200k_base")`; it never uses mutable
model-name lookup. The vocabulary is the embedded resource
`o200k_base.tiktoken.deflate`, so runtime construction is offline after locked
NuGet restore.

Tests retain:

- the OpenAI ordinary `hello world` round trip with the pinned o200k token IDs
  `24912, 2375`;
- strict UTF-8, combining-character, emoji, JSON escaping, and newline cases;
- a 502-byte qualification-request golden with 120 o200k tokens, canonical
  SHA-256
  `26d04987ee43cb1ff581ccb32de900419515c4c8f99019e135cc0c44a2740a57`,
  and little-endian token-ID SHA-256
  `7af38412f5fb1630d984e59a252e6b5fba06f38ca94e800a6838cec05048cd52`;
- exact byte-envelope boundary and one-over rejection for all three operations;
  and
- model, policy, package, vocabulary, malformed-input, and unsupported input-
  class drift rejection.

## Accepted policy and proof

The versioned identity is
`openai-responses-o200k-byte-envelope/v1`. It pins:

- model `gpt-5.6-sol` to encoding `o200k_base`;
- the two exact tokenizer packages and their locked content identities;
- strict canonical UTF-8 request bytes and SHA-256;
- exact ordinary o200k token IDs, count, and token-ID fingerprint; and
- the closed M1 request shape containing every tokenizable instruction, input,
  inline strict output schema, and profile field.

The policy rejects tools, files, images, multi-turn input, previous-response
state, and any unknown or out-of-band provider field. No provider input can
exist outside the canonical byte string under this policy.

For canonical UTF-8 byte count `B`, exact ordinary o200k token count `T`, fixed
structural allowance `A`, and conservative provider-inclusive upper `U`:

```text
T <= B
U = B + A
qualification: A = 4,096 and U <= 20,480
semantic operations: A = 8,192 and U <= 73,728
```

The allowances are the owner-approved fixed structural margins represented by
the accepted plan's difference between its canonical byte ceilings and local
input-token ceilings. The proof intentionally uses `B`, not `T`, as its body
term. The exact tokenizer count is retained drift and diagnostic evidence; it
does not reduce the conservative reservation.

## Findings and alternatives

This closes ADR-0023's separately qualified conservative-bound alternative.
It does not select the alternative provider input-token-count capability. A
provider count would add a network operation and remains outside WP1 authority.

A mutable `CreateForModel` lookup was rejected because a package update could
silently change the model-to-encoding mapping. A token-count-only policy was
rejected because it would omit provider structural framing. Disabling NuGet
audit was rejected; the vulnerable transitive memory package is instead pinned
to Microsoft's patched 9.0.14 release.

## Uncertainty and limitations

`T` is exact for the pinned local o200k vocabulary and canonical request bytes,
not an assertion of the provider's final billed input count. `A` is an accepted
conservative M1 policy allowance, not a recovered private provider grammar.
Any future provider input class, request field, tokenizer/vocabulary change,
model remapping, or need to exceed either allowance is capability drift and
requires a new qualified policy identity rather than weakening `/v1`.

## Recommendation and authority effect

Use the policy for WP1 proof and StateTotality. Keep contracts at their
package-controlled maturity until the final convergence review; do not advance
`docs/current-state.md` from this research result alone. No plan amendment or
new ADR is needed because the owner selected one of the exact alternatives
already accepted by the Slice 6 plan and ADR-0023.
