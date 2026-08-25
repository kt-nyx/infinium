# ADR-0036: Development provider access and product credential separation

Status: Accepted
Date: 2026-08-25
Accepted: 2026-08-25
Accepted by: Project owner
Last reviewed: 2026-08-25
Supersedes: ADR-0020 only where its project-funded credential exclusion would
otherwise prohibit explicitly authorized development tooling; shipped-product
credential ownership and no-fallback rules remain unchanged
Superseded by: None

## Plain-language decision

Infinium development may use the owner's dedicated, cost-limited OpenAI
project when an accepted implementation or conformance task genuinely needs a
live provider call. That permission belongs to development tooling only. It
does not give the shipped product a shared key, a hidden fallback account, or
permission to make automatic calls.

Ordinary builds and automated tests remain offline. Every live development
call is an explicit, bounded operation whose request, maximum cost, actual
usage, and sanitized outcome can be audited without exposing the key.

## Context

ADR-0013 and ADR-0025 require direct OpenAI Responses integration. ADR-0020
correctly prevents the shipped product from silently using a project-funded or
shared credential. During M1, however, a complete ban on project-controlled
development credentials prevented realistic adapter, error, usage, and
provenance qualification. The owner therefore created a dedicated OpenAI
project with a USD 10.00 project-level limit, supplied a development key, and
later created a service account.

The project-level provider limit is useful but not sufficient by itself. It is
shared by credentials in that project, and provider-side accounting can be
delayed. Infinium still needs local per-operation admission and conservative
settlement.

## Decision drivers

- Provider-dependent code must be testable against the actual service when a
  bounded accepted task requires it.
- Credentials must not enter source control, prompts, command lines, ordinary
  environment configuration, logs, test results, dumps, or archives.
- A development credential must never become a shipped-product fallback.
- Each live operation needs a finite local cost boundary in addition to the
  provider project's USD 10.00 outer boundary.
- Ordinary development and CI must remain deterministic and credential-free.

## Considered options

### Prohibit all project credentials

This has the smallest credential surface but prevents realistic provider
integration and regression qualification. It was too restrictive in practice.

### Let any development process read an environment key

This is convenient but spreads the secret through process environments,
diagnostics, crash dumps, and ad hoc scripts. It also makes billable authority
implicit. This option is rejected.

### Use one governed development-provider path

This preserves realistic testing while keeping the key in an OS-backed exact
target, requiring explicit live invocation and local cost admission, and
separating development identity from product identity. This option is
selected.

## Decision

1. The owner's dedicated OpenAI project is eligible for explicit Infinium
   development and conformance operations. Its provider-side aggregate limit
   is USD 10.00 unless the owner records a later replacement limit.
2. The presently enrolled dedicated-project key may remain in use if it has
   not been exposed. A service-account key is preferred at the next ordinary
   rotation because it has a project-owned identity and independent
   revocation, but rotation is not required merely to accept this ADR.
3. A development secret is stored only through the accepted OS-backed
   credential boundary. It is never committed, copied into an archive,
   accepted from a repository `.env`, printed, fingerprinted, compared, or
   exposed to a general worker or model.
4. One functionally named development-provider command owns live use. It
   requires an explicit live switch and a closed typed manifest containing the
   operation, provider profile, model/capability profile, deadline, finite
   input/output bounds, maximum calls, and maximum local cost.
5. The coordinator-owned budget path reserves the worst credible local cost
   before dispatch and settles actual or conservatively unknown exposure after
   the attempt. The local bound may be lower than, but never inferred to be
   equal to, the provider project's aggregate limit.
6. No branch, commit, filename, milestone state, test name, environment
   variable, or presence of a credential grants live authority.
7. Ordinary build, unit, contract, integration, evaluation, security, fault,
   formatting, and documentation commands do not access the credential or
   start provider/network/billable work. They use the offline provider seam.
8. The shipped product continues to require the user's explicitly selected
   provider/account under ADR-0020. It cannot discover, select, or fall back to
   the development credential.
9. Provider auth, quota, rate, network, scope, or billing failure stops that
   development operation. There is no alternate key, account, model, provider,
   or project fallback.
10. A key found in plaintext is treated as exposed. The file is deleted
    without preserving its value, and provider-side revocation remains an
    explicit owner action.

## Consequences

### Positive

- Provider integration can be developed and qualified realistically.
- Cost and live-effect authority remain explicit and auditable.
- Product and development credential identities cannot silently substitute for
  one another.
- Offline tests remain safe and repeatable.

### Negative

- Development needs a small governed live harness and additional accounting
  evidence.
- A provider project limit is shared and may not stop an already admitted
  request immediately.
- Service-account and project metadata require occasional owner maintenance.

### Risks and mitigations

- **Accidental ordinary-test call:** the live adapter is unavailable unless an
  explicit typed live manifest reaches the final authority gate; ordinary
  tests use a fake transport and prove zero starts.
- **Credential leakage:** exact-target helper access, canary tests, secret-free
  diagnostics, dump cleanup, and archive admission scans keep the secret out of
  broad surfaces.
- **Development credential reaches product:** separate purpose/profile types
  and final dispatch checks reject cross-purpose selection.
- **Unexpected cost:** provider project limit, local reservation, finite
  request bounds, no fallback, durable settlement, and conservative unknown
  exposure form independent layers.

## Requirements affected

- AI-001 through AI-007
- AUTH-002
- OPS-001 through OPS-003
- SEC-001 through SEC-004

## Validation

- Offline tests prove ordinary commands cannot resolve or dispatch the
  development profile.
- Authorization tests reject missing live opt-in, wrong purpose, wrong
  profile/generation, expired deadline, excessive request bounds, and
  insufficient budget.
- Secret canaries remain absent from arguments, environment, IPC,
  persistence, logs, diagnostics, dumps, reports, exports, and archives.
- Fake-provider integration proves reservation, one start, settlement,
  cancellation/failure, and no fallback.
- A live conformance call occurs only under a later accepted task that
  explicitly authorizes that effect; this cleanup makes no live call.

## References

- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md)
- [ADR-0023](ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md)
- [ADR-0025](ADR-0025-m1-openai-model-and-synchronous-responses-profile.md)
- [Security and privacy](../security-and-privacy.md)
- [Post-M1 cleanup transition](../../plans/transitions/post-m1-cleanup/README.md)
