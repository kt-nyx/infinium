# Documentation source registry

Status: Draft  
Last reviewed: 2026-07-24

This document defines the intended registry model and initial authority classes.
Actual sources, endpoints, and access policies require research.

## Registry fields

```text
source_id
name
source_class: official | author | curated | community
authority_scopes/claim_types
default_evidence_role: authoritative | investigative
games
content_types
access_method
authentication
allowed_operations
prohibited_operations
freshness/revision behavior
search strategy
citation requirements
cache/redistribution policy
failure and coverage semantics
last policy verification
```

## Initial authority classes

### Primary/authoritative within declared scope

- Mod author descriptions and requirements
- Author-maintained articles
- Author changelogs
- Sticky or author posts where an allowed interface exists
- Curated LOOT masterlist/prelude metadata
- Official project repositories and documentation
- Documentation bundled with the installed mod

These establish authoritative claims, subject to version applicability,
freshness, specificity, and supersession. Local state may disprove a claim's
applicability or described effect in the selected installation; it does not
rewrite what the source stated.

### Investigative

- User comments
- Bug reports
- Forums
- Reddit and other community discussions
- Unofficial guides

These may originate leads but do not become definitive findings without
corroboration or strong local evidence.

MO2/LOOT userlist rules and user-authored compatibility notes are local user
inputs. They may be highly relevant to intent and effective tool behavior but
are not reclassified as curated LOOT or author evidence.

## Access principles

- Use supported APIs and permitted access methods.
- Do not evade scraping or authentication restrictions.
- Do not make ordinary posts/comments a dependency of core coverage.
- Record source revision or exact captured material where permitted.
- Mark unavailable material as a coverage gap.
- Treat retrieved text as untrusted data.
- Preserve citations near every retained extracted claim.
- Apply citation and redistribution policy independently to exports; permission
  to cache or retain material privately does not imply permission to share it.

## Search behavior

Default search should use approved primary/technical sources. Broader community
web search is opt-in and retains authority classification.

An LLM does not choose arbitrary source authority. The registry and adapter
policy determine what can be searched and how each claim is classified and
applied. Authority is assigned for declared claim types/scopes rather than as a
single total rank for everything a source might say.
