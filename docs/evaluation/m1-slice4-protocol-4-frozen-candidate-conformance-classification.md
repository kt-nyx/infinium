# M1 Slice 4.5 protocol `/4` frozen-candidate conformance classification

Status: Historical complete classification — evaluator `/4` representation gap

Recorded: 2026-08-07

Work ID: `M1/S4.5/PRE-B2/WP5`

Accepted model: `infinium.m1-slice4.protocol-4-evidence-contract/1.2.0`

## Classification

The exact WP5 classification is **evaluator `/4` representation gap**.

The accepted partial `RACE/DATA` disposition requires one retained
`race_contributions` item to publish its common contribution facts and literal
`kind=race` while omitting only `/face_gen_head`. Frozen evaluator `/4`
cannot represent that fact set. Its unchanged `ProjectContributionFacts`
mechanic calls `ProjectRace` for every retained race contribution, and
`ProjectRace` unconditionally reads the required JSON boolean
`face_gen_head` and emits the corresponding fact.

There is no candidate document shape that produces the accepted result:

- retaining the race-contribution object with a boolean emits the prohibited
  higher-layer fact;
- omitting the race-contribution object also omits the required common
  contribution facts and `kind=race`; and
- retaining the object without a valid boolean makes frozen canonicalization
  fail instead of gracefully omitting the fact.

The frozen candidate also has product-level conflicts on the same admitted
state. Those conflicts are recorded below, but product realignment alone
cannot make the accepted fact set representable by frozen `/4`. The evaluator
gap therefore controls the single WP5 classification.

This is a global stop condition. WP6 is not directly authorized, private B2
does not resume, and protocol `/5` must not be created.

## Exact identities

| Identity | Reproduced value |
|---|---|
| Accepted WP4 closeout / WP5 input | `43d54accc1adbafc6ae6d0bb13e8f700461758c4` |
| Accepted totality model version | `1.2.0` |
| Accepted totality model SHA-256 | `09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5` |
| Frozen candidate commit | `a98d648bd0adb2751ee0c09828e0227b1583950f` |
| Candidate closeout commit | `2fc724af9e6cc483e98e48c2163b533a071671fa` |
| Producer | `infinium.bethesda.m1-semantic-index/2.0.0` |
| Product schema | `2.0.0` |
| Frozen evaluator commit | `3693d19563c636cd2879804633ca4ce52448d2c1` |
| Protocol | `infinium.evaluator-v2/4` |
| Projection | `infinium.evaluator-v2.slice4-semantic-projection/3.0.0` |

All named commits existed and were ancestors of the WP5 input. The frozen
candidate was inspected only in a genuinely new detached worktree at its exact
commit. The frozen evaluator and candidate contain the same
`SemanticCanonicalizer.cs` Git blob,
`72f84fb74820796b0501ca7472e50a982091354b`.

## Answer-free comparison method

Before candidate source, tests, or build output were inspected, WP5 reduced the
accepted model to a checklist covering all fifteen fact families, all nine
state classes, all ten coverage populations, all eight gap rules, the eleven
atomic boundaries, and the complete higher-order partial `RACE/DATA`
invariant. The checklist was derived from the accepted model and evidence
contract, not from candidate behavior.

Only after that checklist was fixed did WP5 create the fresh detached candidate
worktree, verify its environment and identity, run public checks, and inspect
the candidate product and frozen public projection mechanics. No candidate
output was used as truth and no expected answer was revised after inspection.

## Fifteen-family comparison

| Fact family | Frozen-candidate classification evidence |
|---|---|
| `result` | No contradiction found. Snapshot and failure presence follow the frozen result projection. |
| `plugins` | No contradiction found. Order, provider identity, style, and masters retain the accepted exact values. |
| `override_chains` | Conforming on the decisive partial path: structural chain, ordered common contributions, and winner survive the unsupported `DATA` shape. |
| `npc_contributions` | No contradiction found in the accepted scalar, link, null, unresolved, and unsupported-member boundaries. |
| `race_contributions` | **Evaluator gap and candidate conflict.** The candidate retains common facts but materializes undecodable `face_gen_head` as boolean `false`; frozen `/4` cannot retain the common facts while omitting only that boolean. |
| `placed_reference_contributions` | No contradiction found in the independent link, placement, and unsupported-member boundaries. |
| `allowlisted_fields` | **Product conflict.** Structural observation counts `DATA` before classifying its shape unsupported, and the candidate publishes that positive count. The accepted partial rule requires omission unless count evidence is independently admitted at the observed layer. Frozen `/4` can represent the required omission, so this surface alone is a product mismatch. |
| `npcs` | No contradiction found in complete winner publication or link-state handling. |
| `races` | **Product conflict.** The candidate keeps an undecodable winning race in the resolved map with boolean `false`; the accepted model requires the complete resolved race to be omitted. Frozen `/4` can represent map-entry omission. |
| `placed_references` | No contradiction found in complete winner publication or independent optional-member omission. |
| `face_gen` | The candidate's unknown race decision reaches the accepted `unknown_race` applicability path; no separate contradiction was needed for the classification. |
| `taxonomy` | Conforming on the decisive partial path: the retained contribution creates one subject with only `surface.plugin-data` and `delivery.plugin-container`; no `DATA`-derived semantic tuple is added. |
| `coverage` | **Product conflict.** The candidate constructs `race-records` with both denominator and completion equal to the race count. With one partial race it therefore publishes `1/1` and `completed_with_gaps` instead of the accepted `1/0` and `unsupported`. Taxonomy subject coverage remains `1/1`. |
| `gaps` | Conforming on the decisive partial path: one aggregated `unsupported-shapes:race:data` / `allowlisted-record-shape-semantics` gap with affected count one is constructed. |
| `result_gaps` | No separate contradiction found; the same aggregate is published at result scope as required. |

“No contradiction found” records this bounded source, projection, and public
verification pass. It is not a claim that WP5 executed all 110 admitted model
states as product byte fixtures.

## Exact partial `RACE/DATA` disposition

For one admitted `RACE` contribution with structurally present, unsupported,
undecodable `DATA`, the comparison was:

| Obligation | Accepted disposition | Frozen candidate / `/4` result |
|---|---|---|
| Override chain and winner | retain exact common facts | retained |
| Race-contribution common facts and `kind=race` | retain | retained by candidate, but inseparable from the next scalar in frozen `/4` |
| `race_contributions/.../face_gen_head` | omit | candidate supplies `false`; frozen `/4` unconditionally projects it |
| `allowlisted_fields/...:data` | omit | candidate publishes the structural occurrence count |
| Resolved `races/...` item | omit | candidate publishes the winning race with `face_gen_head=false` |
| Generic taxonomy | one subject; exactly two technical assignments | conforms |
| `race-records` coverage | denominator `+1`, completion `+0`, state `unsupported` for this wholly unsupported nonzero population | candidate uses `+1/+1`, state `completed_with_gaps` |
| `taxonomy-subjects` coverage | denominator `+1`, completion `+1` | conforms |
| Owning gap | exactly one `unsupported-shapes:race:data` / `allowlisted-record-shape-semantics`, `snapshot-and-result`, affected `1` | conforms |

The frozen product source establishes these outcomes directly:

- structural scanning counts every subrecord before the unsupported-shape
  check;
- allowlisted-field publication uses that structural count without requiring
  `HasSupportedField`;
- an unsupported race `DATA` creates decision `Unknown` and compatibility
  boolean `false`, then enters both contribution and winner collections;
- race coverage is constructed from `races.Count, races.Count`; and
- taxonomy and gap builders retain the generic technical subject and exact
  unsupported-shape aggregate.

Frozen `SemanticCanonicalizer.ProjectRace` then projects the compatibility
boolean without consulting `face_gen_head_decision`. This is the substantive
representation gap.

## Public verification

Environment:

- Microsoft Windows 11 Home `10.0.26200`, 64-bit;
- .NET SDK `10.0.302`, MSBuild `18.6.11`; and
- detached, clean candidate HEAD
  `a98d648bd0adb2751ee0c09828e0227b1583950f`.

Results:

- locked restore: passed;
- Release build: passed, 0 warnings and 0 errors;
- focused semantic/product/public-protocol verification: 61 passed, 1 expected
  platform-capability skip, 0 failed;
- `M1Unit`: 89 passed, 1 expected platform-capability skip;
- `M1Contract`: 31 passed;
- `M1Integration`: 33 passed;
- `M1Evaluation`: 54 passed, 9 expected platform or machine-identity skips;
- `M1Security`: 9 passed;
- `M1Fault`: 13 passed;
- full suite: 268 passed, 10 expected skips, 0 failed;
- `dotnet format --verify-no-changes`: passed;
- `git diff --check`: passed; and
- detached candidate tracked state remained clean.

The existing tests reproduce the frozen behavior. They do not exercise or
resolve the newly accepted partial-state requirement.

### Artifact reproduction

`src/Infinium.Bethesda/bin/Release/net10.0/Infinium.Bethesda.dll` reproduced
exactly:

- byte length: `171520`; and
- SHA-256:
  `017de3a40a2d3b6a268bb7c024f3e053bdcaff5da7622da0fdd14dd3693d2c7d`.

The evaluation runtime contained exactly the frozen 65 DLL names. Every DLL's
byte length and SHA-256 matched the freeze record; there were no missing or
unexpected DLLs.

The recorded aggregate
`aa207221286b8c66d4e432c560b673e4fc5ae78e5d388f7a6bdaac8878985a7a`
reproduces when the listed freeze-record order is joined with LF. The freeze
record describes that order as “ordinal”; applying literal
`StringComparer.Ordinal` to the mixed-case filenames instead produces
`b0896d02eb7b1b82bfe51e26d82266134aa42221638d43c26edd7824fc6e9396`.
Because all 65 individual identities match, this is a public aggregate
canonicalization-description limitation, not candidate byte drift.

### Dependency-manifest check

The required Windows PowerShell check returned exit code `1` with
`Dependency manifest is stale. Run eng/update-dependency-manifest.ps1.` The
candidate was not repaired. An isolated Windows PowerShell round-trip of the
tracked JSON was identical after line-ending normalization: the tracked file
has 826 LF line endings, while `ConvertTo-Json` plus `Environment.NewLine`
generated the same 826 lines as CRLF. The diagnostic is consistent with the
known check-script/`.gitattributes` compatibility defect; the required check
itself nevertheless remains an actual failure and is not concealed as a pass.

## Boundaries preserved

- The accepted contract/model and answer-free checklist were not changed.
- The frozen candidate, evaluator, protocol, projection, freeze record,
  product source, and tests were not changed.
- No repair, tuning, retry, refreeze, replacement candidate, or protocol `/5`
  was created.
- No private fixture, private expected output, predecessor oracle answer,
  evaluator-private repository content, or legacy archive was read.
- No `adapt`, `score`, `score-corpus`, private candidate execution, B2, C2,
  Stage D, Slice 5, live call, or billable call ran.
- No sub-agent or replacement reviewer was launched.
- Nothing was pushed.

## Historical required next disposition

At this WP5 checkpoint, completion did not unblock WP6 directly. The accepted
plan required an explicit owner-authorized successor disposition.

Product realignment is also needed if this candidate is ever reconsidered:
the partial `DATA` count, resolved race, and race completion arithmetic conflict
with the accepted model. That work cannot resolve the controlling frozen `/4`
representation gap by itself and is not authorized here. Protocol `/5`, a new
evaluator, and another private pass remain prohibited by the existing M1
boundary.

## Current disposition

ADR-0032 supplies the required successor disposition: protocol `/4` remains
frozen historical evidence with bounded regression use and the representation
gap excluded; protocol `/5` is retired unqualified; private held-out evaluation
is deferred without a product verdict; and no WP6/private scoring path is
authorized. Evaluator-deferral closeout is accepted, Slice 4.5 is closed, and
Slice 5 is eligible under the M1 continuation verification profile.
