# ADR-0026: Evaluator-private fixture repository and delegated access

Status: Accepted
Date: 2026-08-01
Accepted: 2026-08-01
Accepted by: Project owner
Last reviewed: 2026-08-01
Supersedes: None
Superseded by: None

## Context

Infinium's accepted evaluation policy separates development, validation, and
held-out data. The initial Slice 3.5 implementation kept one complete private
replacement under ignored `artifacts/` storage and accidentally committed two
answer-bearing validation packages publicly without recorded owner approval.
Ignored storage is not durable version control, while public answers weaken
independent evidence against fixture-specific implementation.

The project owner requires private fixtures to remain versioned and
maintainable, and also requires agents to access them autonomously for proper
scoring, integrity audit, correction, and independent construction without
manual creation of a separate user task for each operation.

## Decision drivers

- Preserve unseen validation and held-out evidence against overfitting.
- Retain immutable, auditable Git history for private inputs and answers.
- Avoid routine private acquisition into an ordinary Infinium checkout.
- Allow bounded autonomous agent work without leaking raw answers into
  implementation context.
- Keep fixture correction grounded in independent evidence rather than
  production output.
- Preserve exact identities, replay, disclosure, and contamination history.

## Considered options

### Separate private sibling Git repository with delegated access

Provides independent history and ordinary Git maintenance. Public identifiers,
revision IDs, and fingerprints bind the repositories without exposing a
locator or automatically acquiring private content. Fresh-context evaluator
roles allow autonomous access with sanitized returns.

### Private Git submodule

Provides an exact parent-to-child commit pointer, but the URL and submodule path
are product-repository metadata and recursive clone/update workflows make
private acquisition into the ordinary workspace routine.

### Nested ignored repository or unversioned artifact store

Keeps material out of product Git but leaves it inside ordinary workspace and
cleanup boundaries, and does not by itself provide durable remote history.

### Encrypted private payloads in Infinium

Retains one history, but impairs review and makes decryption-key availability
the real access boundary. Any implementation agent with the key can inspect the
answers.

## Decision

Infinium evaluator-private fixtures shall live in a separate private Git
repository, normally checked out as a sibling named
`infinium-evaluator-fixtures`. It shall not be a submodule, subtree, nested
ignored repository, package dependency, or ordinary test-data source of
Infinium.

Infinium may retain only:

- fixture ID/version, partition, purpose, evaluation IDs, classification, and
  sealed review state;
- private-store identity and immutable Git revision without a remote or local
  locator;
- exact document lengths and SHA-256 fingerprints;
- independence, custodian, access, contamination, and sanitized evaluation
  attestations; and
- schemas, development fixtures, and answer-free invocation contracts.

Private packages retain inputs, oracles, generators, evidence, correction
history, access records, raw scoring output, and the pinned public contract
bundle. Sealed versions are immutable; corrections create a new version.

An ordinary implementation agent must not read private files directly. It may
autonomously delegate a bounded operation to a fresh-context evaluator agent
for scoring, identity/replay verification, oracle audit, replacement
construction/review, or corruption recovery. The delegated role receives only
the minimum pinned public contract context required and returns only the
authorized disclosure class. Default validation/held-out return is sanitized
metadata plus outcome, never raw bytes or expected values.

Input authors, oracle reviewers, scorers, maintainers, and custodians remain
distinct roles. Oracle audit must not use production output as expected truth.
A scorer may execute an exact trusted product artifact as a black box, but raw
results remain private.

If answer-bearing private information enters implementation context or directly
drives production change, the access record marks contamination, that fixture
version transitions to development, and materially independent private
replacement coverage is required.

Repository instructions and delegation are acknowledged as process isolation,
not hostile-code sandboxing. Final held-out acceptance may require a separate
worker, OS identity, VM, or private CI broker when stronger enforcement is
needed.

## Consequences

### Positive

- Private fixtures receive durable version history without entering Infinium
  history or routine workspaces.
- Agents can score and maintain fixtures autonomously under one stable
  documented protocol.
- Implementation context receives useful outcomes without answer leakage.
- Every reveal has an explicit, testable contamination and replacement rule.

### Negative

- Two repositories and two commits must be reconciled through fingerprints and
  revision metadata.
- Shared-host agents remain governed rather than technically sandboxed.
- Private repository backup/remote creation and credentials require separate
  owner-controlled setup.
- Failed private validation can require replacement coverage before another
  independent measurement.

### Risks and mitigations

- **Agent reads private data directly:** root instructions default-deny direct
  reads and require purpose-bound fresh-context delegation and access records.
- **Sanitized report leaks answers:** disclosure schemas forbid raw values,
  private paths, answer-bearing names, and raw output.
- **Bad oracle is changed to match production:** maintenance excludes production
  output as truth and requires new independent evidence plus a new version.
- **Cross-repository drift:** public registry entries bind exact private-store
  revisions and document fingerprints; private packages bind a public contract
  commit.
- **Private CI exposes data:** private scoring runs only trusted artifacts and
  keeps raw logs private; ordinary pull-request jobs receive no private access.

## Requirements affected

- EVID-001 through EVID-007
- AI-003 and AI-006
- OPS-002 and OPS-003
- SEC-001, SEC-003, and SEC-004
- RQ-025 and RQ-040
- EVAL-0052 and EVAL-0086

## Validation

- Infinium tests reject tracked private packages, private locators, answer
  fields, and unbound private registry entries.
- Private-store tests verify repository identity, immutable version structure,
  package/document fingerprints, answer-free execution input, access-record
  shape, and clean deterministic reconstruction.
- Delegated audits record role, purpose, supplied context, disclosure class,
  answer-release state, and contamination state.
- Public predecessor packages exposed during development are reclassified and
  bound to materially independent sealed replacements.
- Documentation review confirms one canonical policy with consistent links from
  agent, evaluation, milestone, slice, and implementation records.

## References

- [RESEARCH-0052](../../research/investigations/RESEARCH-0052-evaluator-private-fixture-repository-and-agent-access.md)
- [Evaluation strategy](../../evaluation/evaluation-strategy.md)
- [Fixture guidelines](../../evaluation/fixture-guidelines.md)
- [Anti-overfitting rules](../../evaluation/anti-overfitting-rules.md)
- Git project, [Git Tools - Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules), retrieved 2026-08-01
- GitHub Docs, [Repository roles for an organization](https://docs.github.com/en/organizations/managing-user-access-to-your-organizations-repositories/managing-repository-roles/repository-roles-for-an-organization), retrieved 2026-08-01
- GitHub Docs, [Sharing actions and workflows from your private repository](https://docs.github.com/en/actions/how-tos/reuse-automations/share-across-private-repositories), retrieved 2026-08-01
