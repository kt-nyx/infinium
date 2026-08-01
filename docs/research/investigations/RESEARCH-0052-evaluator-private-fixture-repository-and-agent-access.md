# RESEARCH-0052: Evaluator-private fixture repository and agent access

Status: Completed; recommendation accepted
Date: 2026-08-01
Last reviewed: 2026-08-01
Researcher: Codex with project-owner disposition
Accepted by: Project owner

## Question and requirements

How should Infinium retain validation and held-out fixtures in version control
without exposing their inputs and answers to ordinary implementation work, while
still allowing agents to score, audit, correct, and extend the corpus without a
manual task-setup ceremony for every access?

The answer must preserve the accepted evaluation partition, independent oracle,
replay, provenance, redistribution, protected-root, and no-live-provider rules.

## Scope and non-scope

In scope are repository topology, version binding, delegated agent access,
disclosure, contamination, correction, and local/CI evaluation boundaries.
Production analyzers, Slice 4 execution, hosted CI installation, a remote
repository creation, and hostile-code sandboxing are not selected here.

## Sources and exact versions

- Infinium anti-overfitting, fixture, evaluation, semantic-ground-truth, and
  Slice 3.5 documents at commit
  `1d9b006bb66021a76d4e3171f5abae3836741896`;
- Git project, *Git Tools - Submodules*, retrieved 2026-08-01;
- GitHub Docs, *Repository roles for an organization*, retrieved 2026-08-01;
- GitHub Docs, *Sharing actions and workflows from your private repository*,
  retrieved 2026-08-01; and
- the local ignored evaluator store and tracked sanitized registry as inspected
  on 2026-08-01.

## Experiments and artifacts

The existing evaluator-private malformed replacement occupied 1,546,090 bytes
across 75 files under the root-ignored `artifacts/` tree. It had a complete
sealed package and public fingerprints but no independent Git history. The
Infinium repository had no submodules and tracked only its sanitized
`evaluator-private-registry.json` entry.

## Findings

1. A separate Git repository provides durable, reviewable history without
   placing answer-bearing material in ordinary Infinium clones or history.
2. A submodule preserves an exact commit pointer but also advertises the
   repository relationship and makes recursive acquisition into the product
   workspace routine. That is the wrong default for answer isolation.
3. A nested ignored Git repository remains inside ordinary workspace discovery
   and cleanup boundaries. Encrypted blobs in Infinium impair review and merely
   move the boundary to key access.
4. For this small project-authored corpus, ordinary Git is sufficient; LFS or
   object storage is not justified by size.
5. Agents sharing a host filesystem are not strongly sandboxed merely by using
   sibling repositories. Independence therefore requires both a default-deny
   direct-read rule and fresh-context purpose-bound delegation with sanitized
   return values.
6. An evaluator agent can inspect raw material without contaminating an
   implementation agent if production context is withheld and answer-bearing
   details do not cross the delegation boundary.
7. When exact private information drives a production change, the fixture is
   development data regardless of how it was stored. It must be reclassified
   and independently replaced.

## Alternatives

### Private sibling Git repository

Selected. It separates history and normal workspace context while retaining
ordinary Git review, correction, tagging, and backup behavior.

### Private Git submodule

Rejected. Reproducible pinning is useful, but normal clone/update workflows and
the in-worktree location weaken the intended default access boundary. Public
hash and revision metadata supplies the required binding without a submodule.

### Nested ignored repository

Rejected as the durable design. It is convenient locally but remains inside the
product workspace and the existing root `artifacts/` tree is intentionally
disposable.

### Encrypted archive in Infinium

Rejected. It obscures review and creates a key-distribution problem without
protecting answers from any agent that receives the key.

### Separate task for every access

Rejected as mandatory ceremony. Fresh-context delegated evaluator agents can be
launched autonomously under a stable role and disclosure contract.

## Uncertainty and limitations

This is process and context isolation, not an adversarial security sandbox.
Stronger held-out enforcement may later use a separate OS identity, VM, private
CI worker, or broker that exposes only sanitized scoring results. A future
large or redistribution-restricted corpus requires its own blob-storage and
licensing decision.

## Recommendation

Use a separately versioned private sibling repository. Infinium retains only
public schemas, development fixtures, sanitized registry identities, and
sanitized evaluation attestations. Permit autonomous access only through
fresh-context delegated evaluator roles with an append-only access record and
bounded disclosure. Keep exact private repository locators and credentials out
of Infinium. Reclassify and replace any fixture whose answer is revealed to
guide production.

## ADR or follow-up enabled

The project owner accepted this recommendation. ADR-0026 records the durable
repository, authority, delegation, disclosure, and contamination decision.
