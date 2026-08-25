# Functional implementation naming

Status: Accepted
Disposition: Active repository governance

Last reviewed: 2026-08-25
Owner: Project owner

## Plain-language rule

Names in the working product should explain what something does. They should
not require a reader to know which milestone, slice, work package, campaign,
attempt, or temporary development step originally produced it.

For example, `ProviderUsageAccounting` explains a responsibility.
`M1Slice6CampaignAccounting` only explains when the code was written. The first
name remains meaningful during M2 and later maintenance; the second becomes
historical clutter as soon as its plan ends.

This rule applies to active implementation. Historical plans and archive
material may retain their original names because chronology is their purpose.

## Required naming basis

An active name must primarily describe at least one of:

- a domain object or rule;
- a behavior or operation;
- an owning responsibility;
- an architectural boundary;
- a supported external system or protocol; or
- a genuine compatibility/version distinction.

Names must not primarily encode:

- milestone, slice, work-package, wave, evaluator-stage, or planning-gate
  position;
- campaign, attempt, correction, continuation, successor, replacement, or
  recovery chronology;
- an agent, branch, commit, date, or pass/fail result; or
- vague temporal labels such as `old`, `new`, `current`, `next`, or `final`
  when a functional distinction exists.

## Surfaces covered

The rule covers active:

- directories and filenames;
- namespaces, types, members, parameters, and test names;
- commands, switches, environment keys, configuration fields, log/event names,
  and errors;
- JSON schema IDs/properties, protobuf names, SQL objects, migration-facing
  code aliases, and persisted artifact kinds;
- fixture identities, registry entries, and engineering tools; and
- comments or documentation that advertise a historical identifier as a
  current entry point.

The automated checker scans filenames and code-like declaration/command lines.
The final manual audit covers meaning that cannot be recognized reliably from
tokens alone.

## Terms that require scrutiny

The checker treats direct forms such as `M1`, `Slice6`, `S6`, `WP9`, `WaveE`,
`PRE-B2`, `campaign`, `successor`, `continuation`, `pre-live`, `post-success`,
`replacement-candidate`, and `approach` as planning-language findings.

Words including `stage`, `phase`, `development`, `candidate`, `recovery`,
`generation`, and numeric versions are not universally forbidden. They are
valid when they name a real product concept—for example, staging an atomic
file write, a credential generation, a development fixture partition, or a
versioned wire contract. Reviewers must record that functional meaning rather
than exempting the word globally.

## Compatibility exceptions

Frozen serialized identifiers, database migrations, historical evidence IDs,
and externally consumed compatibility names are not rewritten in place merely
to improve style. Active code should expose a functional alias around them when
possible.

Every temporary or compatibility exception is an exact entry in
`eng/functional-naming-allowlist.json` containing:

- the exact repository-relative path;
- whether the finding is in the path or content;
- the exact token;
- a symbol or context;
- the reason it must remain;
- the retained consumer;
- its classification; and
- a concrete removal or review condition.

Directory-wide and token-wide exemptions are prohibited. New entries require
explicit review. Cleanup-debt entries are removed when their owning cleanup
package archives, extracts, or renames the path.

## Review questions

For each suspicious name, ask:

1. Would the name still explain the thing if the project plan disappeared?
2. Does it distinguish behavior, or merely distinguish chronology?
3. Is the term part of a frozen external/persisted identity?
4. Which current producer, consumer, migration, or refusal rule needs it?
5. Can the current code use a functional alias while retaining only the frozen
   byte identity at the compatibility edge?

If there is no current consumer and the name exists only to preserve how work
happened, the material belongs in the development-history archive rather than
the active implementation.

## Enforcement

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1
```

The command fails on any finding not covered by an exact reviewed entry and on
any stale allowlist entry. Its self-test proves representative forbidden names
are rejected and exact exemptions are accepted. WP7 of the post-M1 cleanup
must reduce cleanup-debt entries to zero; afterward the checker prevents the
same organizational naming from returning.

## Relationship to plans and history

Planning IDs such as `TRANSITION/POST-M1-CLEANUP/WP1` remain appropriate in
plans, current-state handoffs, and implementation records. They do not become
namespaces, runtime commands, source types, fixture identities, or storage
objects. Historical archive material retains its original bytes and names.

## References

- [Development execution policy](../execution-policy.md)
- [Work-breakdown notation](../plans/work-breakdown-notation.md)
- [Post-M1 cleanup transition](../plans/transitions/post-m1-cleanup/README.md)
