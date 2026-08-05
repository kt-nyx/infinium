# Evaluator-v2 successor Stage B2 contract gap

Status: Accepted sanitized incident record

Recorded: 2026-08-04

Source: Project-owner-supplied sanitized Stage B2 review summary

## Boundary

This public record contains the complete authorized disclosure from the
successor private-corpus Stage B2 attempt. It records no private member name,
filename, record identity, value, offset, path, input byte, oracle value, or
candidate output. The public repository and frozen Slice 4 candidate were not
used to inspect the private repository in preparing this record.

## Bound identities

- public successor protocol: `infinium.evaluator-v2/3`;
- public evaluator freeze:
  `34ed0c84165e9a49f44a88ecd87cac967132ebd7`;
- private input-freeze commit:
  `534373b6ef0c676f794941b0787513ed187e16d3`;
- private blocked-review commit:
  `4f6b0fbacc2c7b991201870d9aeb6d5f5b67b0c3`;
- intended corpus identity: `infinium.m1.slice4.heldout` version `2.0.0`.

## Sanitized Stage B2 state

Two successor input cases were constructed and independently byte-reviewed.
No input correction was required and the corpus author used zero correction
cycles. Expected outputs were not created. No prepared comparison ran. No
corpus fingerprint, freeze, or tag was created. The candidate was not
executed, and no product source or output was used. Contamination remained
`clean`.

Stage C2 remained blocked and Stage D did not start.

## Public-authority gaps

The independent reviewer found three facts required by projection `2.0.0`
that could not be authored authoritatively from the public contract and hidden
input bytes alone:

1. The public boundary establishes that the selected malformed input is
   invalid, but does not authorize one exact product failure-code string.
2. The accepted byte authority establishes raw 20-byte `AIDT` presence and
   shape but deliberately does not authorize the typed subfield mapping used
   by the Mutagen-backed product contract.
3. The public product contract does not define an independent construction
   algorithm for internal taxonomy assignment IDs.

These are public projection-authority gaps, not input defects and not product
corrections. Protocol `/3` remains qualified public evidence but is
superseded before a valid successor corpus because its projection required
non-independently-authorable oracle values.

## Disposition

The owner authorized one final public successor, protocol
`infinium.evaluator-v2/4`, with projection `3.0.0`. It separates
independently authorable held-out semantics from implementation-specific
public conformance. Private oracle construction, corpus qualification, and
scoring may resume only after the public `/4` evaluator is qualified and
frozen.
