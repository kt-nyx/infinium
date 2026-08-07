# Work-breakdown notation

Status: Accepted
Last reviewed: 2026-08-07

## Purpose

Infinium work sometimes needs a finer boundary than a milestone or slice, but
the evaluator lifecycle already uses Stage A, B, C, and D and numbered steps
such as B2 and C2. This notation gives small public work units stable,
readable names without inventing new evaluator stages or extending slice
decimals indefinitely.

## Options considered

| Option | Disposition | Reason |
|---|---|---|
| Prose-only labels such as “the next pre-B2 pass” | Rejected | They are hard to search, compare, order, and hand to a fresh agent. |
| More slice decimals or evaluator stages such as 4.5.1 or B1.5 | Rejected | They blur product decomposition with the frozen evaluator lifecycle and become difficult to interpret. |
| Hierarchical work-breakdown path | Selected | It is stable, sortable, human-readable, and keeps each kind of boundary explicit. |

## Canonical form

Use:

```text
{milestone}/{slice}/{phase}/{work-package}
```

For the current effort:

```text
M1/S4.5/PRE-B2/WP1
```

The components mean:

- `M1`: accepted milestone;
- `S4.5`: milestone slice;
- `PRE-B2`: plan-local phase containing the public prerequisites for a future
  B2 authorization; and
- `WP1`: one bounded, assignable work package.

The matching human label is written in full on first use, for example:
“M1 Slice 4.5 — Pre-B2 evidence-contract totality — WP1: Evidence-state
model.” The canonical ID may be used alone after that.

## Reserved meanings

- **Milestone** and **slice** retain the meanings in the accepted milestone
  plan.
- **Stage** is reserved for the evaluator lifecycle (`A`, `B`, `C`, `D` and
  named operations such as `B2` or `C2`).
- **Phase** is a plan-local grouping or gate. `PRE-B2` is not a new evaluator
  stage, protocol, retry, or authorization to run B2.
- **Work package** is the smallest unit intended for implementation by one
  fresh agent without an owner decision during normal execution.

If a work package must be subdivided, append a task component only in its own
plan, for example `M1/S4.5/PRE-B2/WP2/T1`. Prefer another work package when the
unit has a separate deliverable, authority boundary, reviewer, or stop
condition.

## Stability rules

The canonical ID describes scope, not execution history. Do not encode these
in it:

- status (`planned`, `blocked`, `complete`);
- agent or model;
- branch or commit;
- attempt, run, or review number;
- calendar date; or
- pass/fail result.

Record those as metadata. Never renumber an accepted work package merely
because another package is inserted or abandoned. Mark it superseded and add
a new ID if its scope materially changes.

## Required plan metadata

Every plan that uses this notation records:

```text
Work ID: M1/S4.5/PRE-B2
Parent: M1/S4.5
Depends on: <canonical IDs or accepted artifacts>
Next work package: M1/S4.5/PRE-B2/WP1
```

Each work-package section records its canonical ID, objective, inputs,
allowed paths/actions, deliverables, verification, stop conditions, and the
next package it unblocks. Implementation and review records use the same ID so
fresh-agent prompts and later status reports can be traced without relying on
chat history.

## Current Slice 4.5 map

```text
M1/S4.5
├── PRE-B2  Historical public prerequisites; completed through WP5
│   ├── WP1 Evidence-state and fact-dependency model
│   ├── WP2 Executable totality validator
│   ├── WP3 Generated synthetic state coverage
│   ├── WP4 Independent product-blind totality review
│   ├── WP5 Frozen candidate conformance classification
│   └── WP6 Not directly executed; superseded by owner dispositions
├── PRE-B2/V5  Historical retired successor attempt
│   ├── WP0 Disposition and plan complete
│   ├── WP1/WP1R/WP1V Historical proof and hard-stop records
│   └── WP2-WP4 Never started; not resumable
└── EVAL-CLOSEOUT  Current evaluator deferral and M1 continuation
    ├── WP0 Preserve failed evidence
    ├── WP1 Accept deferral and migrate durable semantics
    ├── WP2 Retire /5 and establish bounded /4 regression
    ├── WP3 Replace the M1 gate and reconcile documentation
    ├── WP4 Fresh evaluator-boundary and documentation audits
    └── WP5 Final closeout and Slice 5 handoff
```

This map names work only. Its execution authority and gates live in the
accepted Slice 4.5 plans. No `PRE-B2/V5` entry authorizes active work. Slice 5
is eligible because `EVAL-CLOSEOUT/WP5` received final acceptance.
