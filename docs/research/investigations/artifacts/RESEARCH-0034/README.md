# RESEARCH-0034 artifacts

Status: Completed
Captured: 2026-07-28
Last reviewed: 2026-07-28

This directory contains the independent-input qualification package for the
loose-only NPC-to-FaceGen identity and provider contract.

- `facegen-inputs.json` defines twenty synthetic full/light, applicability,
  provider, negative, malformed, and unsupported cases without expected
  answers.
- `facegen-expected.json` separately pre-registers the expected result subsets.
- `qualify-facegen.mjs` implements the bounded decision and provider-resolution
  probe, then compares its output to the separate expectations.
- `facegen-qualification-results.json` records the executed result.
- `real-path-corroboration.json` records hash-pinned observations from one full
  origin, one `.esl` origin, and one ESL-flagged `.esp` origin. These private
  local observations corroborate the path convention but are not shipped
  fixtures or universal correctness oracles.

Run:

```powershell
node qualify-facegen.mjs
```

The probe accepts only a pre-resolved record/applicability input and a captured
provider list. It does not qualify Mutagen parsing, MO2 effective-state
reconstruction, archive activation/precedence, NIF/DDS semantic correctness,
or Skyrim rendering. Those boundaries remain governed by their separate
evaluation cases. Archive-dependent resolution remains a visible gap.
