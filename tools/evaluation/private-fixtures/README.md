# Evaluator-private store discovery

ADR-0032 defers the current M1 private held-out evaluator. No private store
discovery, corpus work, B2, C2, Stage D, adaptation, comparison, or scoring is
authorized by the current closeout or M1 continuation profile. This retained
tooling description is historical/capability documentation, not an active
workflow.

The private store is not tracked as a submodule and its locator is not stored in
Infinium. A maintainer configures the sibling checkout once in this checkout's
private `.git/config`:

```powershell
git config --local infinium.evaluatorPrivateStorePath '<absolute private store path>'
```

`Get-PrivateStoreDescriptor.ps1` validates that the configured checkout is a
separate Git history with the accepted store identity. By default it returns
only store identity, governance version, and revision. A primary orchestrator
may use `-IncludeLocatorForDelegation` only while constructing a bounded
fresh-context delegated evaluator task; the returned local locator must not be
persisted in tracked files, registry data, or ordinary logs. The script does not
enumerate or read fixture packages. The caller must not use the locator to
inspect private content directly; access follows
`docs/evaluation/evaluator-private-fixture-governance.md`.
Current policy is
`docs/evaluation/evaluator-private-fixture-governance-v2.md`; any future use
also requires a new accepted evaluator ADR and plan after Slice 9 during M3
planning.
