# Evaluation tools

The evaluation harness is not implemented by Slice 0. This directory is
reserved for the accepted later-slice evaluation tooling.

Evaluator-private payloads and oracles do not belong here. They are retained in
the separate private Git store selected by ADR-0026. Tools placed here may
define answer-free invocation, sanitized registry verification, and a narrow
local-config bootstrap that supplies a locator to a fresh-context delegate.
Ordinary Infinium tooling must not enumerate or read private fixture content,
and the bootstrap locator must not enter tracked files, registry data, or
ordinary logs. Private scoring and maintenance occur through the delegated protocol in
`docs/evaluation/evaluator-private-fixture-governance.md`.
