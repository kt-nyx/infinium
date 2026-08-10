# M1 Slice 5 pre-closeout repository normalization

Status: Accepted

Owner: Project owner

Accepted: 2026-08-10

Last reviewed: 2026-08-10

Superseded in part: ADR-0033 and the project owner's 2026-08-10 protocol `/4`
retirement supersede only this amendment's `/4` path-preservation and bounded-
regression requirements. The completed normalization evidence remains valid.

## Authority and purpose

The project owner accepted this bounded pre-closeout amendment on 2026-08-10.
It authorizes repository normalization before Slice 5 owner acceptance so that
implementation-active contracts, fixtures, tools, verification, and current
documentation use functional names and a maintainable layout.

This amendment supersedes the exact terminal-review candidate only as a
closeout candidate. It does not reject the accepted implementation evidence,
accept Slice 5, freeze its contracts, or authorize successor-slice work. The
renamed and reorganized exact candidate must pass the complete Slice 5 floor,
the applicable public continuation profile, and fresh terminal review
before a revised owner-acceptance packet is presented.

## Authorized work

1. Replace nonhistorical milestone, slice, and work-package names in product
   code, tests, active fixture identities, producer identities, and ordinary
   verification entry points with functional names. Split broad Slice 5
   contract and codec files by architectural responsibility. Update every
   producer, consumer, schema, fixture, persistence seam, test, and document
   together without compatibility aliases.
2. Consolidate active public fixtures and their tooling under functional
   fixture roots. Move executable fixture packages out of documentation,
   reseal path- or identity-dependent public metadata, and independently
   verify that expected truth and product semantics did not change.
3. Reorganize planning material as milestone -> slice -> work package, place
   records beside their owning plans, separate current authority from history,
   and consolidate superseded evaluator attempts and incidents into compact
   history plus exact Git-identity inventories.
4. Historical at execution time: preserve the frozen protocol `/4` reusable
   core. ADR-0033 later superseded this item and moved the exact closure to the
   excluded sibling archive.
5. Remove proven temporary or superseded generated material, remove clean
   obsolete detached sibling worktrees through Git, and consolidate the
   approved small evaluator-development directories into one excluded sibling
   archive. Stop on unexpected dirty or uniquely authoritative external state.

## Exclusions

- no access to the evaluator-private fixture repository or private material;
- no access to or modification of `../infinium-legacy-archive/`;
- no protocol `/5` resumption, new evaluator semantics, private scoring, live
  provider operation, external product write, or successor-slice work;
- no change to product behavior or expected semantic truth except a separately
  classified defect found by the ordinary review loop;
- no push.

Ignored dependency caches and the separate ignored `human-guide/` surface are
not cleanup targets.

## Execution and acceptance

The parent agent coordinates read-only inventories and fresh independent
reviews. Overlapping repository edits are integrated sequentially. The work
continues through implementation, focused checks, review, correction, and
re-review until all in-scope findings are closed or an execution-policy
escalation condition occurs.

Acceptance requires:

- locked restore and build, formatting, and the complete Release test floor;
- every functional Slice 5 gate and the comprehensive public corpus;
- schema, fixture, fingerprint, repository-authority, dependency, strict-JSON,
  link, stale-reference, and diff checks;
- repository proof that archived evaluator paths and commands are absent;
- fresh code/fixture, documentation/authority, and terminal whole-slice review;
  and
- a revised implementation record and owner-acceptance packet that retain all
  existing claim boundaries and explicitly state that no private held-out
  verdict exists.

## Later owner-approved protocol retirement

ADR-0033 completed the superseding retirement at Infinium commit
`60fad172abef402a2469015064b069020777aaea`. The exact final public closure is
preserved in the excluded sibling Git archive at commit
`c490de9689d8e9f8dfc7eccb3d056ab5b083e9fd`; protocol `/4` has no active
repository path, command, test, review gate, or authority role.
