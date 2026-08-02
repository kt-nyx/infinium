#!/usr/bin/env python3
"""Build exhaustive public EVAL-0086 projections from independent byte facts.

This tool intentionally does not load Infinium production assemblies or output.  It
uses the accepted public subject-ID contract, the accepted-order receipt, and the
independently decoded byte oracle.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path
from typing import Any


FIXTURE_VERSION = "1.2.0"
ORACLE_ARTIFACT_VERSION = "1.2.0"
REVIEWED_AT = "2026-08-02T12:00:00.0000000+00:00"
TAXONOMY_ID = "infinium.skyrim-se.mod-impact-taxonomy"
TAXONOMY_VERSION = "0.1.0"
CAPTURED_AT = "2026-07-30T00:00:00.0000000+00:00"
SEMANTIC = {"NPC_", "RACE", "REFR"}
IDENTITY_ONLY = {"CELL", "CLAS", "PACK", "CLFM", "FACT", "HDPT", "KYWD", "LCTN", "STAT"}

SURFACE_REASON = "The frozen major-record fact establishes plugin-carried record data."
AI_REASON = "Separate frozen AIDT and resolved PKID facts establish AI-package semantics; the NPC_ signature does not."
APPEARANCE_REASON = "Separate frozen RNAM and resolved RACE-link facts establish appearance and identity semantics; the NPC_ signature does not."
PLACED_REASON = "Separate frozen placement, linked-reference, location, and resolved-link facts establish the placed-object area; the REFR signature does not."


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_sha(value: Any) -> str:
    encoded = json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("ascii")
    return hashlib.sha256(encoded).hexdigest()


def assignment(
    axis: str,
    facet: str,
    applicability: str,
    role: str,
    evidence: list[str],
    reason: str,
    code: str | None = None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "axis": axis,
        "facet": facet,
    }
    if code is not None:
        result["code"] = code
    result.update(
        {
            "applicability_state": applicability,
            "classification_role": role,
            "evidence_references": sorted(set(evidence)),
            "reason": reason,
        }
    )
    return result


def forbidden(axis: str, reason: str, code: str | None = None) -> dict[str, str]:
    result = {"axis": axis}
    if code is not None:
        result["code"] = code
    result["reason"] = reason
    return result


def general_forbidden(*other_area_codes: str) -> list[dict[str, str]]:
    values = [
        forbidden(
            "declared-purpose-and-intended-feature-area",
            "No author-purpose evidence is present.",
        ),
        forbidden("consequence-type", "The frozen facts establish no consequence."),
        forbidden("effect-extent", "The frozen facts establish no effect extent."),
    ]
    values.extend(
        forbidden(
            "affected-game-system-or-content-area",
            "A different independently evidenced semantic area cannot be copied to this subject.",
            code,
        )
        for code in other_area_codes
    )
    return values


def stable_snapshot_id(ordered_plugins: list[dict[str, Any]]) -> str:
    parts: list[str] = []
    for plugin in ordered_plugins:
        order = int(plugin["load_order"])
        parts.append(
            f"{plugin['file_name']}|{order}|fixture-provider-{order:03d}|{plugin['sha256']}"
        )
    structural = hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()
    occurrence = hashlib.sha256(f"{structural}|{CAPTURED_AT}".encode("utf-8")).hexdigest()
    return f"snapshot-{occurrence[:24]}"


def build(package: Path) -> None:
    fixture_id = package.name
    byte_oracle_path = package / "oracle" / "independent-byte-facts.json"
    byte_oracle = read_json(byte_oracle_path)
    receipt_path = package / "inputs" / "snapshot" / "accepted-order.json"
    receipt = read_json(receipt_path)
    ordered_plugins = sorted(receipt["plugin_order"], key=lambda item: item["load_order"])
    selected_artifacts = {item["artifact_id"]: item for item in ordered_plugins}
    facts = byte_oracle["facts"]

    sealed_files = {item["artifact_id"]: item for item in byte_oracle["files"]}
    if set(selected_artifacts) - set(sealed_files):
        raise ValueError(f"{fixture_id}: accepted plugin is absent from independent byte facts")
    for artifact, plugin in selected_artifacts.items():
        sealed = sealed_files[artifact]
        if plugin["sha256"] != sealed["sha256"]:
            raise ValueError(f"{fixture_id}: accepted-order and byte-fact seals disagree for {artifact}")

    record_facts: dict[str, list[dict[str, Any]]] = {}
    for fact in facts:
        value = fact["canonical_value"]
        artifact = value.get("artifact_id")
        if fact["fact_kind"] == "record" and artifact in selected_artifacts:
            record_facts.setdefault(artifact, []).append(fact)

    entries: list[tuple[str, str, dict[str, Any]]] = []

    def add(target: str, preferred_id: str | None, canonical: dict[str, Any]) -> None:
        entries.append((target, preferred_id or "", canonical))

    all_area_codes = [
        "area.actors.ai-packages",
        "area.actors.appearance-identity",
        "area.world.placed-objects-activation",
    ]

    for artifact, plugin in selected_artifacts.items():
        plugin_name = plugin["file_name"]
        load_order = int(plugin["load_order"])
        for record_fact in sorted(
            record_facts.get(artifact, []),
            key=lambda item: (
                int(item["canonical_value"].get("offset", 0)),
                item["fact_id"],
            ),
        ):
            record = record_fact["canonical_value"]
            signature = record["signature"]
            if signature == "TES4":
                continue
            if record.get("resolution_state") != "resolved" or not record.get("form_key"):
                continue
            form_key = record["form_key"]
            record_fact_id = record_fact["fact_id"]
            contribution = (
                f"contribution:{load_order:04d}:{plugin_name.lower()}:{form_key.lower()}"
            )

            if signature in SEMANTIC:
                add(
                    contribution,
                    None,
                    {
                        "subject_type": "record-contribution",
                        "source_package_id": fixture_id,
                        "source_evidence_references": [record_fact_id],
                        "independent_semantic_evidence_references": [],
                        "expected_assignments": [
                            assignment(
                                "technical-modification-surface",
                                "semantic-mechanism",
                                "assigned",
                                "observed",
                                [record_fact_id],
                                SURFACE_REASON,
                                "surface.plugin-data",
                            )
                        ],
                        "forbidden_assignments": general_forbidden(*all_area_codes),
                    },
                )

                record_children = [
                    fact
                    for fact in facts
                    if fact["subject_id"].startswith(record_fact["subject_id"] + ".")
                ]
                subrecord_by_signature: dict[str, list[str]] = {}
                resolved_links_by_field: dict[str, list[str]] = {}
                for fact in record_children:
                    value = fact["canonical_value"]
                    if fact["fact_kind"] == "subrecord" and value.get("kind") == "allowlisted-subrecord":
                        subrecord_by_signature.setdefault(value["signature"], []).append(fact["fact_id"])
                    if (
                        fact["fact_kind"] == "link"
                        and value.get("kind") == "canonical-link"
                        and value.get("resolution_state") == "resolved"
                    ):
                        field = value["classification"].removesuffix("-value")
                        resolved_links_by_field.setdefault(field, []).append(fact["fact_id"])

                semantic_area: str | None = None
                semantic_evidence: list[str] = []
                preferred: str | None = None
                reason: str | None = None
                if signature == "NPC_" and (
                    subrecord_by_signature.get("AIDT")
                    or resolved_links_by_field.get("PKID")
                ):
                    semantic_area = "area.actors.ai-packages"
                    semantic_evidence = (
                        subrecord_by_signature.get("AIDT", [])
                        + resolved_links_by_field.get("PKID", [])
                    )
                    reason = AI_REASON
                    if contribution.endswith("01-actors.esm:00000800:01-actors.esm"):
                        preferred = "TAX-12B"
                    elif contribution.endswith("02-behavior.esp:00000800:01-actors.esm"):
                        preferred = "TAX-03A"
                if semantic_area:
                    target = f"{contribution}:semantic:{semantic_area}"
                    add(
                        target,
                        preferred,
                        semantic_canonical(
                            fixture_id,
                            record_fact_id,
                            semantic_evidence,
                            semantic_area,
                            reason,
                            all_area_codes,
                        ),
                    )

                if signature == "NPC_" and resolved_links_by_field.get("RNAM"):
                    semantic_area = "area.actors.appearance-identity"
                    semantic_evidence = resolved_links_by_field["RNAM"]
                    target = f"{contribution}:semantic:{semantic_area}"
                    preferred = (
                        "TAX-12A"
                        if contribution.endswith("01-actors.esm:00000850:01-actors.esm")
                        else None
                    )
                    add(
                        target,
                        preferred,
                        semantic_canonical(
                            fixture_id,
                            record_fact_id,
                            semantic_evidence,
                            semantic_area,
                            APPEARANCE_REASON,
                            all_area_codes,
                        ),
                    )

                if signature == "REFR":
                    semantic_evidence = subrecord_by_signature.get("DATA", [])
                    for field in ("NAME", "XLRL", "XOWN", "XLKR"):
                        semantic_evidence += resolved_links_by_field.get(field, [])
                    if semantic_evidence:
                        semantic_area = "area.world.placed-objects-activation"
                        target = f"{contribution}:semantic:{semantic_area}"
                        add(
                            target,
                            "TAX-03B" if preferred_refr(record_fact) else None,
                            semantic_canonical(
                                fixture_id,
                                record_fact_id,
                                semantic_evidence,
                                semantic_area,
                                PLACED_REASON,
                                all_area_codes,
                            ),
                        )
            elif signature not in IDENTITY_ONLY:
                target = f"unsupported-record:{plugin_name.lower()}:{signature.lower()}:{form_key.lower()}"
                add(target, "TAX-06" if fixture_id == "BETH-UNSUPPORTED-VAL" else None, unsupported_canonical(fixture_id, record_fact_id))

    if len(ordered_plugins) > 1:
        source_references = [
            "inputs/snapshot/accepted-order.json",
            *[item["provider_id"] for item in receipt["provider_order"]],
        ]
        snapshot_id = stable_snapshot_id(ordered_plugins)
        add(
            f"provider-topology:{snapshot_id}",
            "TAX-08" if fixture_id == "BETH-NPC-DEV" else None,
            provider_canonical(fixture_id, source_references),
        )

    entries.sort(key=lambda item: item[0])
    used_ids = {preferred for _, preferred, _ in entries if preferred}
    next_id = 1
    subjects = []
    bindings = []
    for target, preferred, canonical in entries:
        if preferred:
            subject_id = preferred
        else:
            while f"TAX-CLOSURE-{next_id:04d}" in used_ids:
                next_id += 1
            subject_id = f"TAX-CLOSURE-{next_id:04d}"
            next_id += 1
        used_ids.add(subject_id)
        canonical = {"subject_id": subject_id, **canonical}
        subjects.append(
            {
                "subject_id": subject_id,
                "canonical_value": canonical,
                "canonical_value_fingerprint": canonical_sha(canonical),
            }
        )
        bindings.append(
            {
                "sealed_subject_id": subject_id,
                "production_subject_participant_id": target,
            }
        )

    if len({item[0] for item in entries}) != len(entries):
        raise ValueError(f"{fixture_id}: duplicate production taxonomy subject")
    if len({item["subject_id"] for item in subjects}) != len(subjects):
        raise ValueError(f"{fixture_id}: duplicate sealed taxonomy subject")

    projection = {
        "schema_id": "infinium.evaluation.taxonomy-projections/v1",
        "schema_version": "1",
        "fixture_id": fixture_id,
        "fixture_version": FIXTURE_VERSION,
        "taxonomy_id": TAXONOMY_ID,
        "taxonomy_version": TAXONOMY_VERSION,
        "reviewer_id": "taxonomy-reviewer",
        "reviewed_at": REVIEWED_AT,
        "source_artifacts": [
            {
                "artifact_id": "oracle/independent-byte-facts.json",
                "artifact_version": ORACLE_ARTIFACT_VERSION,
                "fingerprint": sha(byte_oracle_path),
                "availability": "retained",
            },
            {
                "artifact_id": "inputs/snapshot/accepted-order.json",
                "artifact_version": FIXTURE_VERSION,
                "fingerprint": sha(receipt_path),
                "availability": "retained",
            },
        ],
        "subjects": subjects,
        "review_assertions": [
            "The sealed subject set exhaustively covers the accepted Slice 4 production taxonomy subject-ID contract for this package.",
            "Every sealed subject has one literal exact production subject binding and no target is reused.",
            "Technical surfaces are observed only from frozen local byte facts.",
            "Affected areas are established only from separate field or resolved-link evidence.",
            "Provider topology is not converted into a technical surface or gameplay area.",
            "Purpose, consequence, and effect extent are not inferred where evidence does not establish them.",
            "No production output, Mutagen answer, xEdit answer, held-out answer, filename heuristic, area heuristic, or record-family shortcut authored the bindings or expected assignments.",
        ],
    }
    binding_document = {
        "schema_id": "infinium.evaluation.taxonomy-subject-bindings/v1",
        "schema_version": "1",
        "fixture_id": fixture_id,
        "fixture_version": FIXTURE_VERSION,
        "taxonomy_id": TAXONOMY_ID,
        "taxonomy_version": TAXONOMY_VERSION,
        "bindings": bindings,
    }
    write_json(package / "oracle" / "taxonomy-projections.json", projection)
    write_json(package / "inputs" / "taxonomy-subject-bindings.json", binding_document)
    print(f"{fixture_id}: {len(subjects)} exhaustive taxonomy subjects")


def semantic_canonical(
    fixture_id: str,
    record_fact_id: str,
    semantic_evidence: list[str],
    area: str,
    reason: str,
    all_areas: list[str],
) -> dict[str, Any]:
    return {
        "subject_type": "record-semantic-subject",
        "source_package_id": fixture_id,
        "source_evidence_references": [record_fact_id, *sorted(set(semantic_evidence))],
        "independent_semantic_evidence_references": sorted(set(semantic_evidence)),
        "expected_assignments": [
            assignment(
                "technical-modification-surface",
                "semantic-mechanism",
                "assigned",
                "observed",
                [record_fact_id],
                SURFACE_REASON,
                "surface.plugin-data",
            ),
            assignment(
                "affected-game-system-or-content-area",
                "affected-area",
                "assigned",
                "established",
                semantic_evidence,
                reason,
                area,
            ),
        ],
        "forbidden_assignments": general_forbidden(
            *[candidate for candidate in all_areas if candidate != area]
        ),
    }


def provider_canonical(fixture_id: str, evidence: list[str]) -> dict[str, Any]:
    return {
        "subject_type": "provider-topology",
        "source_package_id": fixture_id,
        "source_evidence_references": evidence,
        "independent_semantic_evidence_references": [],
        "expected_assignments": [
            assignment("declared-purpose-and-intended-feature-area", "purpose-kind", "not-applicable", "established", evidence, "Provider ordering is topology, not declared purpose."),
            assignment("affected-game-system-or-content-area", "affected-area", "not-applicable", "established", evidence, "Provider ordering alone does not describe an affected game area."),
            assignment("consequence-type", "consequence-type", "not-applicable", "established", evidence, "Provider ordering alone establishes no consequence."),
            assignment("effect-extent", "direct-subject-breadth", "not-applicable", "established", evidence, "Provider ordering alone establishes no direct-subject effect."),
            assignment("effect-extent", "spatial-breadth", "not-applicable", "established", evidence, "Provider ordering alone establishes no spatial effect."),
            assignment("effect-extent", "persistence-and-lifecycle-breadth", "not-applicable", "established", evidence, "Provider ordering alone establishes no persistence effect."),
            assignment("effect-extent", "causal-propagation-or-blast-radius", "not-applicable", "established", evidence, "Provider ordering alone establishes no propagation effect."),
        ],
        "forbidden_assignments": [
            forbidden("technical-modification-surface", "Provider and winner topology cannot establish a semantic surface or delivery code without separate content evidence.")
        ],
    }


def unsupported_canonical(fixture_id: str, record_fact_id: str) -> dict[str, Any]:
    gap = record_fact_id
    return {
        "subject_type": "unsupported-record",
        "source_package_id": fixture_id,
        "source_evidence_references": [record_fact_id],
        "independent_semantic_evidence_references": [gap],
        "expected_assignments": [
            assignment("technical-modification-surface", "semantic-mechanism", "assigned", "observed", [record_fact_id], "The frozen major-record fact establishes plugin-carried record data even though its family semantics are unsupported.", "surface.plugin-data"),
            assignment("technical-modification-surface", "realization-and-delivery", "assigned", "observed", [record_fact_id], "The frozen file and record facts establish delivery inside a plugin container.", "delivery.plugin-container"),
            assignment("affected-game-system-or-content-area", "affected-area", "unsupported", "established", [gap], "The frozen allowlist gap establishes that Slice 4 cannot determine affected-area semantics for this record family."),
            assignment("consequence-type", "consequence-type", "unknown", "predicted", [gap], "The surface is present, but unsupported semantics leave any consequence unknown."),
            assignment("effect-extent", "direct-subject-breadth", "not-applicable", "established", [gap], "No effect is established, so direct-subject breadth is not applicable."),
            assignment("effect-extent", "spatial-breadth", "not-applicable", "established", [gap], "No effect is established, so spatial breadth is not applicable."),
            assignment("effect-extent", "persistence-and-lifecycle-breadth", "not-applicable", "established", [gap], "No effect is established, so persistence breadth is not applicable."),
            assignment("effect-extent", "causal-propagation-or-blast-radius", "not-applicable", "established", [gap], "No effect is established, so propagation breadth is not applicable."),
        ],
        "forbidden_assignments": [
            forbidden("declared-purpose-and-intended-feature-area", "No author-purpose evidence is present."),
            forbidden("affected-game-system-or-content-area", "Unsupported record-family semantics cannot be guessed from the signature or filename."),
            forbidden("consequence-type", "A present unsupported surface does not establish breakage.", "consequence.incorrect-functional-behavior"),
        ],
    }


def preferred_refr(record_fact: dict[str, Any]) -> bool:
    value = record_fact["canonical_value"]
    return (
        value.get("artifact_id") == "inputs/plugins/01-World.esm"
        and value.get("form_key") == "00000840:01-World.esm"
    )


def main() -> int:
    if len(sys.argv) < 2:
        raise SystemExit("usage: build_taxonomy_projections.py PACKAGE [PACKAGE ...]")
    for argument in sys.argv[1:]:
        build(Path(argument).resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
