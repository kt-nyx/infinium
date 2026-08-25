# Post-M1 cleanup transition

Status: Accepted
Disposition: All cleanup work packages are complete; this does not reopen M1
or activate M2

Last reviewed: 2026-08-25
Owner: Project owner
Accepted: 2026-08-25

In plain language, this transition removes the temporary development machinery
that was needed to prove M1, keeps the reusable product pieces, and gives M2 a
smaller and more understandable backend. It also establishes a permanent rule
that implementation names describe what code does rather than where it appeared
in a development plan.

The complete accepted work is in the
[post-M1 cleanup implementation plan](plan.md). The plan covers:

- standing, budgeted development access to the OpenAI API without making a
  project credential a shipped-product fallback;
- retirement and external archival of the completed Slice 6 provider campaign,
  its local retained-state regression, historical provider fixtures/registries,
  completed M0/M1 chronology, and other historical-only assets;
- secure cleanup of plaintext secrets and reproducible archive junk, plus about
  4.1 GiB of repository-local build, test, package-cache, dump, database, log,
  and ignored run output found by the expanded hygiene pass;
- replacement of cumulative historical fixture discovery with one current-only
  public-fixture registry;
- extraction and modularization of the reusable credential, provider, budget,
  semantic-admission, persistence, and replay components;
- an explicit provisional severity/confidence policy;
- a backend report projection suitable for a future M2 interface;
- a repository-wide functional naming pass; and
- automated governance that prevents plan-stage naming from returning.

The owner accepted the plan, its exact WP1 scope, and implementation through
all remaining work packages on 2026-08-25. The completed evidence, archive
coordinates, verification counts, review findings, and limitations are in the
[implementation record](implementation-record.md). The cleanup made no live
provider call and did not access the private fixture repository. M2
implementation, merge, push, and publication remain outside this handoff.
