# Evaluator-private fixture governance

Status: Superseded for evaluator v2; retained as historical evaluator-v1 governance
Last reviewed: 2026-08-01
Authority: [ADR-0026](../architecture/decisions/ADR-0026-evaluator-private-fixture-repository-and-delegated-access.md)

Successor: [Evaluator-private fixture governance v2](evaluator-private-fixture-governance-v2.md)

## Purpose

This was the canonical evaluator-v1 operational policy for versioned validation and held-out
fixtures whose inputs or answers must remain outside ordinary implementation
context. Evaluator-v2 work follows the successor governance document and
ADR-0027. This file remains authoritative only for interpreting retained
evaluator-v1 history.

## Repository boundary

Complete private packages live in a separate private Git repository with an
independent history, normally a sibling checkout named
`infinium-evaluator-fixtures`. It is not an Infinium submodule, subtree, nested
repository, package dependency, or ordinary test-data directory.

Infinium retains schemas, development fixtures, answer-free invocation
contracts, sanitized registries, and sanitized attestations. A registry may
identify the private store and exact immutable revision but must not contain a
remote URL, credential, local path, payload, raw result, oracle locator, or
expected answer.

The private repository retains complete packages, inputs, oracles, construction
source, review evidence, raw results, correction history, access records, and a
pinned public-only contract bundle. Small project-authored binaries use normal
Git. Separate large/restricted blob storage requires another accepted decision.

A checkout-local Git setting may hold the sibling locator. The metadata
bootstrap returns no locator by default; a primary orchestrator may request it
only to construct a fresh-context delegated task. The locator must not enter
tracked files, sanitized registries, or ordinary logs and does not authorize the
primary agent to enumerate private content.

## Autonomous agent access

An implementation agent must not read private material directly. It may
autonomously create a fresh-context delegated evaluator for an allowed purpose:

1. score a trusted exact product artifact at a declared evaluation point;
2. verify identity, completeness, fingerprints, or deterministic replay;
3. audit a suspected fixture/oracle error from independent evidence;
4. author or independently review replacement coverage; or
5. recover corrupt private state without treating production output as truth.

The delegate works only in the private repository and its pinned public
contract bundle, except that a scorer may execute a supplied trusted artifact
as a black box. Input authors and oracle reviewers do not receive production
source/output or predecessor answers. Delegated subtasks must not inherit
answer-bearing implementation conversation when the agent system supports a
fresh-context option.

Every access records fixture identity, role, purpose, supplied production
context/output, permitted disclosure class, answer release, and contamination
state. Access instructions are a process boundary on shared-host agents, not a
claim of OS sandboxing.

## Disclosure and contamination

Allowed disclosure classes are:

- `metadata-only`;
- `sanitized-result`;
- `private-maintenance`; and
- `revealed-development`.

Ordinary validation and held-out operations return only metadata, outcome, an
approved high-level category when needed, fingerprints, attestations, and
contamination state. They do not return raw inputs, exact expected values,
answer-bearing names, offsets, record identities, private paths, oracle text,
or raw output.

If an exact private answer is disclosed to implementation or a private result
directly drives tuning, record the event, transition that fixture version to
development, and add a materially independent private replacement. Storage
location never overrides this rule.

## Authoring, scoring, and correction

- Freeze input bytes before oracle review.
- Keep input author, oracle reviewer, scorer, maintainer, and custodian roles
  distinct.
- Derive expected truth from independent evidence, never production output.
- Require two independent truth methods where the fixture contract says so.
- Keep execution input answer-free and separately fingerprint the oracle.
- Treat sealed versions and tags as immutable.
- Correct errors by adding a new version and append-only error/supersession
  record; invalidate claims that used the bad version.
- Keep raw scoring output private and publish only a sanitized attestation bound
  to the exact product artifact, fixture version, private-store revision, and
  package/oracle fingerprints.

## Stronger held-out enforcement

For final acceptance where shared-host procedural isolation is insufficient,
use a separate evaluator worker, OS identity, VM, or private CI broker that
returns only sanitized results. Ordinary pull-request workflows must not
receive private repository credentials or raw private logs.
