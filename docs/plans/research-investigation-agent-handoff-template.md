# Research investigation agent handoff template

Status: Template  
Last reviewed: 2026-07-25

Use one fresh agent per bounded primary research question. Replace every value
in braces before sending the prompt. Do not combine an entire M0 wave into one
investigation prompt merely because the questions share a gate.

```text
You are performing one bounded research investigation for the Infinium project.

Repository:
Z:\Development\Large Projects\Skyrim\infinium

Primary research question:
{RQ-ID} — {EXACT QUESTION}

M0 wave:
{WAVE}

Required output:
docs/research/investigations/{RESEARCH-NNNN-SHORT-TITLE}.md

Allowed additional writes:
{NONE, OR AN EXACT LIST OF APPROVED RESEARCH-ONLY FILES}

Prior investigation inputs:
{EXACT PATHS, OR "None"}

Specific scope emphasis:
{BOUNDED EMPHASIS OR "Use the accepted RQ scope without expansion"}

Before doing any research:

1. Read AGENTS.md and follow its complete required reading order.
2. Read the accepted M0 plan:
   docs/plans/milestones/M0-research-foundation.md
3. Read the {RQ-ID} entry in:
   docs/research/open-questions.md
4. Read:
   docs/research/investigations/README.md
5. Read every prior investigation path listed above.
6. For source, architecture, integration, security, or evaluation work, read
   every task-specific document required by AGENTS.md.
7. Inspect the current git status. Preserve unrelated and pre-existing work.

Authority and scope rules:

- Treat accepted product documents and ADRs as authoritative.
- Do not inspect or restore the external abandoned-implementation archive
  unless the user explicitly requests archaeological review. A legacy choice,
  test, or implementation is never evidence that an approach is correct.
- Research the current real technology, interface, policy, or format. Do not
  answer from memory when a current primary source or local experiment can
  verify it.
- Prefer official documentation, specifications, maintained source
  repositories, and other primary sources. Record direct URLs, exact
  versions/revisions, retrieval dates, and which claim each source supports.
- When technical claims can be checked locally, perform safe, bounded,
  read-only experiments rather than relying only on documentation.
- Keep observations, external claims, inferences, recommendations, and
  unresolved uncertainty distinct.
- Do not select or accept architecture implicitly. The investigation may
  recommend an option and identify a proposed ADR, but the recommendation is
  not an accepted decision.
- Do not implement production code, modify the user's modding setup, or turn a
  research probe into a production path.
- Do not modify product requirements, accepted ADRs, the RQ registry, source
  registry, taxonomy documents, evaluation catalog, or milestone plan unless
  the allowed-write list explicitly names that file. Instead, include proposed
  follow-up edits in the investigation report for the coordinator to review.
- Do not create a new tracked raw-artifact store. Put small permitted evidence
  directly in the investigation document or an explicitly allowed file.
  Describe private, sensitive, large, copyrighted, or non-redistributable
  artifacts through sanitized manifests/fingerprints and handling notes.
- Do not include secrets, credentials, unnecessary usernames, or unrelated
  absolute paths in documents, prompts, logs, or artifacts.
- Stop and document the blocker if proceeding would require prohibited
  scraping, unknown authorization, paid/authenticated access that was not
  authorized, setup mutation, unsafe tool execution, or a material expansion
  of accepted scope.
- Work as the single investigator for this bounded question. Do not delegate
  parts of the authoritative reading or conclusion to subagents.

Investigation document requirements:

Use Status: Proposed, the current date, and the researcher identity
"Codex agent". Follow the repository investigation outline and include:

1. Status, date, researcher, primary RQ, M0 wave, and decision enabled.
2. Question and linked accepted requirements/ADRs.
3. Scope and explicit non-scope.
4. Sources with URLs, versions/revisions, retrieval dates, authority, and
   claim-level relevance.
5. Experiments with exact environment/tool versions, reproducible steps,
   safe side effects, and artifact manifests.
6. Findings that distinguish verified facts from interpretation.
7. Realistic alternatives evaluated against the same criteria.
8. Contrary evidence, uncertainty, limitations, and unsupported cases.
9. A recommendation whose confidence and preconditions are explicit.
10. The exact ADR, product specification, evaluation case, registry update, or
    follow-up research enabled.
11. Suggested status/update for {RQ-ID}; do not apply it yourself.
12. A requirements-and-evidence traceability table.

Quality and anti-bias requirements:

- Do not merely gather support for the current leading candidate.
- Include meaningful rejection criteria and negative/boundary evidence.
- Do not invent certainty when an interface, policy, or behavior is
  undocumented.
- Do not generalize from one mod, fixture, provider, tool version, or local
  environment without labeling the limitation.
- Preserve unknown and unsupported outcomes as results.
- If this investigation touches taxonomy-bound data, read and use the accepted
  `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`. Keep declared purpose and
  intended target, technical modification surface, affected area,
  consequence, severity, symptom, and effect extent distinct. Treat taxonomy
  mappings as coverage/routing data rather than causal truth, and propose a
  versioned taxonomy change instead of inventing local labels.
- If this investigation touches an integration or external tool, identify all
  observed writes/cache/temp behavior and whether the operation preserves
  AUTH-001 through AUTH-003.
- If it touches paid/provider work, identify authorization, cancellation,
  reservation, usage/cost, retention, and capability gaps without making an
  unverified hard-limit claim.

Before finishing:

1. Re-read the completed investigation against the primary RQ and accepted M0
   exit/deliverable criteria.
2. Perform a semantic review for contradictions, overclaiming, missing
   alternatives, source/applicability errors, and accidental product or
   architecture decisions.
3. Validate all local Markdown links and cited identifiers you added.
4. Run git diff --check.
5. Inspect the final diff and confirm that only allowed files changed.
6. Leave the investigation Status as Proposed for independent review.

Final response:

- Link the investigation file.
- State the recommended answer in a short paragraph.
- List the strongest evidence, material uncertainty, and blocking issues.
- List proposed ADR/product/evaluation/registry follow-ups without applying
  them.
- Report the exact validation performed.
```

## Wave integration/review prompt

After every scheduled investigation in a wave has been independently reviewed,
use a fresh agent with this separate prompt:

```text
Review and integrate M0 Wave {WAVE} for the Infinium project at:
Z:\Development\Large Projects\Skyrim\infinium

This is a review/integration task, not a new primary investigation.

Read AGENTS.md and its complete required reading order, the accepted M0 plan,
and every Wave {WAVE} investigation listed below:

{INVESTIGATION PATHS}

Then:

1. Verify that each report answers its RQ using applicable current primary
   sources and reproducible/local evidence.
2. Check contradictions, dependency gaps, duplicated conclusions, unsafe
   assumptions, source-policy problems, and drift from accepted requirements
   or ADRs.
3. Check whether the Wave {WAVE} gate in the accepted M0 plan is actually met.
4. Fix clear documentation errors inside the investigation files. Do not
   manufacture missing research or soften an unmet gate.
5. Produce:
   docs/research/investigations/{WAVE-INTEGRATION-REPORT}.md
   with Status: Proposed, evidence-backed gate results, residual risks, and
   exact downstream proposals.
6. Propose—but do not accept—RQ status changes, source/taxonomy/evaluation
   updates, ADRs, plan amendments, and the next wave's prerequisite inputs.
7. Validate links and identifiers, run git diff --check, and inspect the final
   diff.

Do not implement production code or treat research recommendations as accepted
architecture. End with an explicit gate result: Met, Met with documented
non-blocking gaps, or Not met.
```
