# ADR-0007: Exclude xEdit from Infinium

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: The xEdit-specific provisions of ADR-0006  
Superseded by: None

Subsequent decision:

- 2026-07-25 —
  [ADR-0009](ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
  accepts `Mutagen.Bethesda.Skyrim` `0.54.2` as the initial bounded semantic
  dependency. References below to Mutagen as a leading or proposed candidate
  are retained as ADR-0007's decision-time history; ADR-0009 is now operative.

## Context

[RESEARCH-0008](../../research/investigations/RESEARCH-0008-mutagen-bethesda-semantic-capability.md)
established that `Mutagen.Bethesda.Skyrim` can provide the programmatic plugin,
record, override-chain, FormKey, link, and winner semantics Infinium needs,
subject to field-level qualification and explicit archive/string gaps.
[RESEARCH-0010](../../research/investigations/RESEARCH-0010-xedit-ground-truth-and-invocation.md)
then evaluated xEdit as a possible independent oracle or optional analyzer.
It found no stable structured export boundary, broad script write authority,
tool-owned settings/log/temp behavior, version-specific automation, and no
successfully qualified invocation.

ADR-0006 nevertheless retained xEdit as a user-installed capability dependency
and possible ground-truth tool while that research was open. After reviewing
the Wave B results, the project owner decided that xEdit provides no product
capability worth the additional dependency, integration, security, setup,
provenance, and evaluation surface. A user may independently use xEdit outside
Infinium, but that activity is not part of the product.

This ADR records that owner decision and preserves why the earlier candidate
was rejected. It does not itself accept Mutagen's exact package/version or
claim that every Mutagen record shape is qualified.

## Decision drivers

- Infinium needs a programmatic semantic layer, not an interactive plugin
  editor or general external scripting host.
- Mutagen covers the product purposes for which xEdit was considered.
- A second tool must add a necessary capability, not merely theoretical
  redundancy.
- Initial setup should not ask users to install or configure an application
  that Infinium does not need.
- Read-only authority is easier to prove when record analysis remains inside a
  bounded library/worker contract over exact captured bytes.
- Evaluation must not become circular merely because xEdit is removed.
- Accepted history must show that xEdit was researched and deliberately
  rejected rather than silently disappearing.

## Considered options

### Option A — Integrate xEdit as a user-facing analyzer

This could expose mature checks but would require executable discovery,
version validation, input staging, settings/log/temp containment, output
parsing, failure interpretation, and operation-specific non-mutation proof.
The research found no necessary product capability that justifies that
surface.

### Option B — Retain xEdit only as a required development/evaluation oracle

This provides implementation diversity, but still makes xEdit part of project
infrastructure and release qualification. The checked command/script boundary
is version-specific, write-capable in important modes, and was not
successfully executed in the research environment. The owner does not want
Infinium correctness or development progress to depend on it.

### Option C — Exclude xEdit and validate Mutagen through first-party,
parser-independent fixture truth

Infinium uses Mutagen for the programmatic semantics that pass an accepted
field/shape allowlist. Expected fixture results are specified independently
through hand-audited atomic binary fixtures, direct byte/structure assertions,
format invariants, matched negative and malformed cases, metamorphic
transformations, known official-master invariants, and retained manual
adjudication. Mutagen round trips and upstream tests may add evidence but may
not be the sole authority for expectations generated through the same code
path.

This option is selected.

## Decision

1. **xEdit has no Infinium product, development, or evaluation role.**
2. Infinium shall not detect, configure, validate, invoke, stage, copy, bundle,
   download, install, update, parse output from, or report capability based on
   xEdit.
3. xEdit is not a user dependency, optional analyzer, fallback parser,
   ground-truth oracle, release gate, fixture dependency, or supported
   integration.
4. A user's independent manual use of xEdit occurs wholly outside Infinium.
   Infinium neither requires nor records it as scan evidence merely because it
   happened.
5. Mutagen remains the leading bundled Bethesda semantic-library candidate.
   Its exact package/version, worker boundary, supported record/field/shape
   allowlist, archive/string exclusions, and upgrade rules still require their
   own accepted semantic-layer decision.
6. EVAL-0052 or its accepted successor shall validate Bethesda record
   semantics without xEdit. It must use independently specified expected
   results, including:
   - minimal hand-audited binary fixtures with direct byte/structure
     assertions;
   - explicit plugin order, override chains, winners, FormKeys, links, record
     states, and expected field values;
   - matched negatives, malformed/unsupported cases, and metamorphic changes;
   - official-master or other retained invariants where licensing permits;
   - provenance for fixture authorship and manual adjudication; and
   - a prohibition on accepting expected results solely because the same
     Mutagen path produced them.
7. If future evidence finds a necessary semantic capability that Mutagen and
   reasonable first-party code cannot supply, it creates a new research
   question and ADR. xEdit does not silently re-enter the project as a
   fallback.
8. RQ-006 is resolved by exclusion. RESEARCH-0010 remains completed historical
   research, but its recommendation to use xEdit as an oracle is rejected and
   superseded by this ADR.
9. This ADR supersedes only the xEdit-specific parts of ADR-0006. ADR-0006
   continues to govern GPLv3-family licensing, MO2/LOOT application posture,
   Mutagen/libloot/USVFS candidates, managed LOOT data, and distribution
   compliance.

## Consequences

### Positive

- Users need not install, locate, or configure xEdit for Infinium.
- The application has one fewer privileged executable, script, temp/cache,
  version, failure, and provenance boundary.
- Mutagen becomes the single proposed programmatic Bethesda semantic layer,
  with explicit supported-shape qualification.
- Product coverage does not vary based on xEdit availability.
- RQ-006 and its operation-specific EVAL-0046 branch are closed by exclusion.

### Negative

- Infinium loses a mature independent implementation for automatic
  cross-checking.
- First-party fixture construction and manual byte/semantic adjudication
  require more care.
- Mutagen-specific defects may be harder to detect if evaluation expectations
  are accidentally derived from Mutagen itself.
- Some checks familiar to xEdit users may need first-party implementations if
  they later become demonstrated product requirements.

### Risks and mitigations

- **Circular validation:** fixture expectations may not be generated solely
  through the Mutagen path being tested; require direct binary assertions,
  hand-reviewed expectations, matched negative/metamorphic cases, and
  provenance.
- **Unsupported Mutagen semantics:** use an explicit record/field/shape
  allowlist and emit coverage gaps outside it.
- **Silent archive/string assumptions:** retain separate provider-aware archive
  and localized-string research gates; low-level parsing does not establish
  effective precedence.
- **Future capability pressure:** require new evidence and an accepted ADR
  before adding any external semantic tool.
- **Historical confusion:** mark xEdit-specific prior recommendations and
  requirements as superseded by this ADR while retaining their research
  evidence.

## Requirements affected

- TOOL-001 through TOOL-003
- ANALYSIS-002 and ANALYSIS-003
- AUTH-003
- EVID-002
- COVER-001 through COVER-003
- DIST-002 and DIST-003
- RQ-004 and RQ-006
- EVAL-0046 and EVAL-0052

## Validation

- No product setup, settings, CLI, UI, capability registry, scan configuration,
  worker, package, runtime path, or release artifact refers to xEdit.
- No M1 or later acceptance criterion requires xEdit installation, execution,
  output, or provenance.
- EVAL-0052 uses independently specified fixture expectations and fails if
  those expectations are produced only by the Mutagen code path under test.
- Removing xEdit from a developer or user machine changes no Infinium
  capability or coverage result.
- Dependency, notice, SBOM, and packaging manifests contain no xEdit payload or
  integration component.
- Documentation retains RESEARCH-0010 and the superseded ADR-0006 provisions
  solely as historical decision provenance.

## References

- [ADR-0006 — GPL product and tool-dependency boundary](ADR-0006-gpl-product-and-tool-dependency-boundary.md)
- [RESEARCH-0008 — Mutagen.Bethesda semantic capability](../../research/investigations/RESEARCH-0008-mutagen-bethesda-semantic-capability.md)
- [RESEARCH-0010 — xEdit ground truth and invocation](../../research/investigations/RESEARCH-0010-xedit-ground-truth-and-invocation.md)
- [RESEARCH-0013 — Wave B authoritative local-state integration](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
