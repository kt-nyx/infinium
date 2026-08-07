# M1 Slice 4 protocol `/5` WP1V proof-closure hard stop

Status: Hard stop

Date: 2026-08-07

Work ID: `M1/S4.5/PRE-B2/V5/WP1V`

Starting branch: `codex/m1-slice-4.5-protocol-5-successor`

Starting commit: `cd23a96be50820326db1f1247edb11c3c86f230b`

## Outcome

WP1V did not converge within its authorized one-review, one-correction budget.
The final independent public re-review returned `REJECT`. WP1 is not
proof-closed, WP2 has not started, and no later `/5`, evaluator, candidate,
private, B2, C2, or scoring role is authorized by this work.

The public proof artifacts and passing runtime outputs are preserved as failed
evidence. They must not be treated as an accepted representation authority or
as proof of 63 exact admitted witnesses.

## Confirmed starting defects

The inherited global validator replaced admitted `coverage`, `gaps`, and
`result_gaps` rule effects with empty projection-only effects. It could report
aggregate success without exact witnesses for rules including
`P4-COVERAGE-COMPLETE`, `P4-GAPS-EMIT`, and
`P4-RESULTGAPS-NO-SNAPSHOT`. The successor model also identified itself as
`1.0.1` while its authority pointer and schema still named contract `1.0.0`.

WP1V corrected those defects, created a 77-rule ledger, retained the support-
family effects in global composition, aligned the current authority identity,
and added exact document/ledger comparisons and mutations. Those corrections
were necessary but not sufficient for acceptance.

## Review and exhausted correction budget

The sole independent reviewer rejected the initial implementation because
constructor-wide placeholder values did not prove property-specific values,
FaceGen unknown outcomes were misrepresented, identity records were stale,
some mutations were not routed through complete proof checks, and the ledger
builder produced runtime-dependent bytes.

The one permitted correction pass repaired the reported FaceGen semantics,
property primitive types, support-family objects, reusable ledger/document
closure checks, mutation routing, derived counters, identity pins, and ordinal
runtime-independent ledger generation. The complete public validators were
then run twice under Windows PowerShell 5.1 and twice under PowerShell 7.

The same reviewer performed the required final re-review and found a material
new semantic false-acceptance. Four admitted resolved-link witnesses declare
resolved canonical states but publish placeholder link values:

- `P4-NPCCONTRIB-RESOLVED`: 20 invalid link properties;
- `P4-NPCS-RESOLVED`: 20 invalid link properties;
- `P4-REFRCONTRIB-RESOLVED`: 16 invalid link properties; and
- `P4-REFRS-RESOLVED`: 16 invalid link properties.

Across those witnesses, nested properties such as `template/state`,
`race/field`, `base/component`, and `template/target_form_key` fall through the
ledger builder's generic string path and receive literal `"x"`. Accepted public
authority instead requires exact link states, a fixed field vocabulary, typed
component semantics, and canonical non-null FormKey targets for present links.

The projection validator checks primitive type and constructor-wide allowed
types but does not independently enforce the link vocabulary, per-property
canonical value, component rule, or FormKey normalization. It constructs the
projection document from the same ledger values and compares the document back
to that ledger. The equality check therefore accepts the same invented value
on both sides. No link-state, link-field, link-component, or noncanonical-
target mutation closes that bypass.

This is 72 invalid properties across four of the claimed 63 exact admitted
witnesses. It invalidates the claimed admitted-witness closure, zero-issue
result, and complete rule closure even though both validators return success.

## Preserved deterministic failed evidence

- Rule ledger: 77 rules, 63 classified admitted, 14 classified terminal,
  15 families, 10 admitted support-family rules; 537,465 bytes; SHA-256
  `8d88061dbcd7d206533c2ed245861c3c823f6576cd9ade727050d75bb8e22904`.
- Global summary: 1,870 bytes; SHA-256
  `f137c39302db01a4d348f4ca5a8b9626cc38e604012d353fe4c25cc2e9e38b95`;
  10 retained projection-rule effect witnesses, zero reported bypasses, and
  35/35 rejected mutations.
- Projection summary: 2,378 bytes; SHA-256
  `920fe2ef10f8c066dc81c20b2e93e00d3166591b10f3580e3f71886174ee58ba`;
  77 reported closed rules, 63 reported exact/schema witnesses, 14 terminal
  witnesses, 10 support-family witnesses, and 50/50 rejected mutations.
- Two Windows PowerShell 5.1 runs and two PowerShell 7 runs produced byte-
  identical global summaries and byte-identical projection summaries at those
  hashes.
- Independent ledger generation under both runtimes produced byte-identical
  537,465-byte output at the ledger hash above.

These deterministic green results reproduce the false acceptance; they do not
cure it.

## Failed finite acceptance conditions

WP1V fails the requirements that every admitted rule have an exact complete
witness, success derive from authority-valid expected-versus-produced values,
mutations detect material substitutions, zero self-authorizing acceptance
paths remain, and the final independent reviewer return `ACCEPT`.

The proof architecture did not converge within the authorized budget. The
failure is a finite public-validator defect rather than a newly identified
semantic-authority contradiction, but this task has no authority to repair it.
Resumption requires an explicit owner-authorized new recovery package and
correction budget that names independent link-value authority validation and
link-specific adversarial mutations as mandatory acceptance gates. A new task
must preserve this hard stop and may not treat the current ledger or document
as its own semantic oracle.

## Boundary

No private fixture or repository, frozen candidate, candidate or product
output, product execution, oracle answer, B2/C2 material, scoring result,
protocol `/6`, live/billable call, legacy archive, history rewrite, or push was
accessed or performed. No further reviewer or correction was initiated. No
focused commit was made because the success condition was not met.
