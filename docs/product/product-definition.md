# Product definition

Status: Accepted  
Last reviewed: 2026-07-25

## Product thesis

Infinium is an evidence-driven pre-playthrough quality-assurance and diagnostic
tool for large Skyrim Special Edition modlists managed with Mod Organizer 2.

It reconstructs the exact effective installation, combines established
deterministic tooling with purpose-built semantic analyzers, and can use an LLM
as an evidence-bound investigator and explainer. Its primary value is finding
consequential problems that raw conflict views, deterministic metadata, or
manual review are likely to miss.

Infinium is not an autonomous mod manager and is not another raw conflict
browser.

## Target user

The initial product is for its creator and is designed primarily for experienced
mod users, not mod developers. Users may understand MO2 and common modding tools
without being able or willing to inspect every plugin record, asset chain,
configuration interaction, or mod page across a list containing hundreds or
thousands of mods.

Later versions may make the workflow more accessible, but initial reliability
for experienced users takes precedence over beginner onboarding.

## Primary problem

Large Skyrim modlists can contain subtle incompatibilities that:

- do not prevent startup;
- are not represented in LOOT or plugin metadata;
- arise only from combinations of records, assets, configuration, runtime
  components, or generated output;
- are documented only in prose;
- silently remove intended features;
- surface only after substantial playtime.

The listed mechanisms are examples of how problems can arise, not an exhaustive
taxonomy of mod types, technical modification surfaces, or affected game areas.
Those classifications depend on
[RQ-036](../research/open-questions.md) and its
[dependency map](../research/taxonomy-dependency-map.md).

Manually reviewing every relevant interaction is impractical. A user needs a
prioritized, inspectable account of likely problems before committing to a real
playthrough.

## Primary workflow

The primary workflow occurs while constructing and testing a modlist, before an
earnest playthrough:

1. Select one MO2 profile.
2. Configure and manually start an analysis.
3. Review time, cost, source, and coverage expectations.
4. Allow local analyzers, established tools, documentation extraction, and
   configured targeted LLM investigations to run.
5. Review a simple scan summary followed by supported findings/cases and a
   separately labeled lead-only investigation queue.
6. Inspect evidence only as deeply as desired.
7. Apply fixes outside Infinium.
8. Run targeted verification or another manual scan.
9. Resolve, accept, or investigate remaining findings, directly or through
   explicit case-level bulk actions; optionally suppress default visibility
   without changing risk state.
10. Reach a categorical state such as "ready with accepted risks" while
    retaining explicit coverage gaps.

Symptom-driven diagnosis and runtime-log correlation complement this workflow
but do not replace preflight analysis as the product's center.

## Core promise

Infinium should:

- find serious and less-serious issues that may matter to the user;
- distinguish measured facts, external claims, hypotheses, and findings;
- explain likely impact, blast radius, and symptoms;
- propose a resolution where supported;
- otherwise propose useful validation or further investigation;
- expose exactly what was and was not analyzed;
- permit the user to decide which risks are acceptable;
- avoid presenting "no findings" as proof that a playthrough is safe.

## Deterministic and LLM boundary

Deterministic systems remain authoritative for:

- MO2/profile state;
- file and archive providers;
- plugin order and record winners;
- binary parsing;
- configuration values;
- local version and runtime data;
- LOOT and other tool outputs;
- cache and installation-snapshot identity.

Applicable author-maintained and curated LOOT sources remain authoritative for
stated intent, instructions, and documented constraints. Deterministic local
state establishes whether those claims apply and what the installation
actually does; neither authority class silently rewrites the other.

The LLM may:

- extract cited claims from prose;
- normalize identity and terminology;
- infer a mod's declared purpose;
- investigate grounded interaction candidates;
- originate novel hypotheses from specific local observations;
- combine supporting and contradicting evidence;
- explain findings;
- propose reversible remediation or validation.

These responsibility lists assign evidence authority and processing boundaries;
they are not a taxonomy of mod purposes, game areas, or consequences. Analyzer
and coverage reporting will map them to the accepted result of RQ-036.

The LLM must not invent local state, decide winners, replace binary parsers, or
turn generic overlap into a problem without supporting evidence.

## Product character

The interface is finding-centric and resembles a modern data/analysis or
observability tool translated for mod users:

- clean and simple at high levels;
- progressively denser when a case or finding is opened;
- responsive during long-running work;
- plain-language conclusion first;
- technical provenance available on demand;
- no unnecessary duplication of MO2's raw conflict tools.

## Authority

Through M4, Infinium is a read-only advisor. It may read state, run approved
non-mutating analyzer/tool operations, open relevant locations, export
artifacts, and maintain its own cache and history.

It must not modify MO2, load order, plugins, mod files, game configuration, or
generated output. Write-capable actions and patch generation are after M4.

## Definition of success

The product becomes trustworthy for a personal playthrough only when:

- profile/provider state agrees with authoritative MO2 behavior;
- parsed record state agrees with xEdit or another established ground truth
  where applicable;
- imported LOOT metadata and diagnostics agree with the invoked LOOT
  configuration and output;
- planted known problems are reliably detected;
- structurally similar harmless cases are not misclassified;
- citations are real, applicable, and inspectable;
- findings explain evidence and uncertainty;
- analyzer failures and coverage gaps are always visible;
- a run with complete retained dependencies replays downstream results, while
  unavailable replay dependencies are explicit and clean LLM reruns remain
  subject to semantic evaluation rather than byte identity;
- remediations or validation steps are useful;
- no new work is authorized past declared deadlines or consumptive limits, with
  any uninterruptible in-flight overrun or provider-side billing variance
  disclosed and reconciled.

## Non-goals

The product through M4 does not attempt to:

- guarantee a stable playthrough;
- replace MO2, LOOT, xEdit, or established generators;
- list every ordinary file or record conflict;
- optimize FPS or benchmark hardware;
- make generic "script-heavy" or texture-performance claims;
- support other mod managers, games, editions, or Skyrim runtimes;
- monitor the filesystem continuously outside a user-initiated operation or use
  changes as automatic analysis triggers;
- instrument Skyrim with a custom runtime component;
- modify the user's setup;
- generate patches;
- provide a general-purpose global chatbot;
- build a community/shared compatibility service.
