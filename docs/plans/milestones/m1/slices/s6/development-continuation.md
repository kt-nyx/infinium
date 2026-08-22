# Slice 6 practical development continuation

Status: Owner-authorized

Authorized: 2026-08-21

## Objective

Complete Slice 6 through the shortest reliable development path: store the
confirmed 164-character OpenAI credential in Windows Credential Manager, prove
the product reads and transmits the bytes unchanged without exposing them,
obtain the first structurally valid WP9, WP10, and WP11 results, and close C3.

The immutable ledger-v4 prefix through sequence 44 remains authoritative. It
records USD 1.02064 committed exposure, no outstanding reservation, and USD
8.97936 remaining under the aggregate USD 10.00 hard limit.

## Superseded development restrictions

For remaining Slice 6 development, this direction supersedes requirements to:

- create a new credential generation, campaign version, ledger version,
  recovery protocol, evidence-schema version, or independently reviewed
  authority after each ordinary correction;
- stop or redesign for recoverable UI, timestamp, schema, manifest, validator,
  persistence, transport, or provider defects;
- rebind Release hashes or obtain independent review after every local fix; or
- treat an earlier one-shot ceremony as prohibiting a diagnosed, sequential
  retry on the corrected working candidate.

The active credential path reuses the already-reserved generation 3 and the
existing masked native helper. The UI shows only a character count and accepts
submission only at exactly 164 characters. The coordinator may reuse the
retained generation-3 manifest as a helper input, but the owner's current task
direction—not that historical manifest's ceremony restrictions—authorizes the
development continuation. Each manual invocation receives a fresh nonsecret
attempt identifier after the preceding attempt has retained terminal evidence;
an unfinished attempt retains its identifier for effect-free replay or
fail-closed recovery. A fresh eleven-minute technical window bounds the helper
effect without reviving the obsolete manifest clock. The exact Release
coordinator and helper apphosts must both identify the currently executing Git
commit, without a per-correction review or hash-rebinding ceremony. Sanitized evidence records identities, counts,
outcome, state, and cost only; it never records credential bytes or a derived
credential hash.

## Mandatory boundaries

- Never print, echo, log, commit, configure, pass by command line or
  environment, or otherwise expose the credential.
- Keep the credential only in transient protected helper input memory and
  Windows Credential Manager. Clear transient buffers after use.
- Verify exact 164-character/164-byte canonical input and exact read-back byte
  equality inside the protected path without emitting the value or a public
  digest.
- Preserve all historical evidence and the sequence-44 ledger prefix.
- Preserve the aggregate USD 10.00 hard limit with conservative accounting.
- Provider calls are sequential. There is no uncontrolled automatic retry.
- The first structurally valid semantic result for a stage is authoritative;
  do not select among multiple valid results or tune against private answers.
- Private fixtures, archives, push, unrelated destructive work, and Slice 7
  remain prohibited.

## Development flow

1. Run the one-shot development credential enrollment command. The key enters
   only the masked helper dialog; the safe counter must show `164 / 164` before
   Submit is admitted.
2. Require Credential Manager write plus exact protected read-back, generation
   3 active-verified/available state, cleared UI/buffers, and zero external
   provider effect during enrollment.
3. Read through the actual product credential path and send one small WP9
   request. Authentication error is failure; a non-authentication-error,
   structurally valid response completes credential acceptance and WP9.
4. Continue sequentially through WP10 and WP11 within remaining aggregate
   budget, then compose C3 from retained history and the new development
   evidence.
5. Use focused tests while correcting. Perform one consolidated review at a
   meaningful stable boundary and the complete verification floor before the
   final owner-ready closeout.
