#!/usr/bin/env python3
"""Build Slice 3.5 oracle artifacts from two independent frozen-byte reports."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

METHODS = [
    "manual-annotated-hex-worksheet-v1",
    "independent-bounded-raw-reader-v1",
]
FIXTURE_VERSION = "1.1.0"
ORACLE_VERSION = "1.1.0"
ORACLE_CHANGED_AT = "2026-08-02T12:00:00.0000000+00:00"
PLUGIN_SUFFIXES = {".esm", ".esp", ".esl"}
SUPPORTED = {
    "TES4": {"MAST", "DATA"},
    "NPC_": {"ACBS", "TPLT", "RNAM", "AIDT", "PKID", "PNAM", "HCLF"},
    "RACE": {"DATA"},
    "REFR": {"NAME", "XLKR", "XLRL", "XOWN", "DATA"},
}
EMPTY_COLLECTIONS = [
    "expected_deterministic_results",
    "expected_external_claims",
    "expected_application_links",
    "expected_discovery_leads",
    "expected_model_proposals",
    "expected_proposal_admissions",
    "expected_candidates",
    "expected_hypotheses",
    "expected_findings",
    "expected_recommendations",
    "expected_supported_cases",
    "expected_lead_only_cases",
    "expected_abstentions",
    "expected_failures",
]


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_fingerprint(value: dict[str, Any]) -> str:
    encoded = json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def safe(value: str) -> str:
    value = re.sub(r"[^A-Za-z0-9._:/-]", "-", value)
    value = value.strip(".:/-")
    return value[:100] or "item"


SCAN_MUTATION_BASELINES = {
    "npc-repeated-pkid-order": "plugins/02-Behavior.esp",
    "npc-repeated-pnam-order": "plugins/03-Appearance.esp",
    "refr-repeated-xlkr-order": "plugins/06-Boundaries.esp",
    "light-native-below-range": "plugins/01-Native.esl",
    "light-native-above-range": "plugins/01-Native.esl",
    "light-flagged-below-range": "plugins/02-Flagged.esp",
    "light-flagged-above-range": "plugins/02-Flagged.esp",
    "light-extension-header-mismatch": "plugins/01-Native.esl",
    "light-reference-out-of-range": "plugins/03-Consumer.esp",
}


def scenario_map(package: Path, matrix: dict[str, Any]) -> dict[str, list[dict[str, Any]]]:
    memberships: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for case in matrix["cases"]:
        case_id = safe(case["case_id"])
        paths = [
            item for item in case["input_paths"] if Path(item).suffix.lower() in PLUGIN_SUFFIXES
        ]
        if case["operation"] == "scan":
            for order, path in enumerate(paths):
                memberships[path].append({"scenario_id": case_id, "plugin_order": order})
        elif case["operation"] == "compare":
            for index, path in enumerate(paths):
                memberships[path].append(
                    {"scenario_id": f"{case_id}.variant-{index}", "plugin_order": 0}
                )
        elif case["operation"] == "orchestrated-read":
            request = read_json(package / "inputs" / case["input_paths"][0])
            for role, key in (("initial", "initial_path"), ("replacement", "replacement_path")):
                memberships[request[key]].append(
                    {"scenario_id": f"{case_id}.{role}", "plugin_order": 0}
                )
        elif case["operation"] == "request":
            request = read_json(package / "inputs" / case["input_paths"][0])
            plugin = request.get("plugin")
            if plugin:
                path = plugin.removeprefix("inputs/")
                memberships[path].append({"scenario_id": case_id, "plugin_order": 0})
    return memberships


def malformed_key(value: dict[str, Any] | str | None) -> tuple[str, int] | None:
    if value is None:
        return None
    if isinstance(value, str):
        code, raw_offset = value.split("@", 1)
        return code, int(raw_offset)
    return value["code"], int(value["offset"])


def record_semantics(record: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {
        "signature": record["signature"],
        "offset": record["offset"],
        "length": record["length"],
        "data_length": record["data_length"],
        "flags_hex": record["flags_hex"],
        "raw_form_id_hex": record["raw_form_id_hex"],
        "compressed": record["compressed"],
        "subrecords": [
            {
                "signature": item["signature"],
                "header_offset": item["header_offset"],
                "data_offset": item["data_offset"],
                "length": item["length"],
                "raw_hex": item["raw_hex"],
            }
            for item in record["subrecords"]
        ],
    }
    if "allowlisted_payload" in record:
        result["allowlisted_payload"] = record["allowlisted_payload"]
    if "identity" in record:
        result["identity"] = record["identity"]
    if "links" in record:
        result["links"] = record["links"]
    return result


def file_semantics(file: dict[str, Any]) -> dict[str, Any]:
    return {
        "tes4": file.get("tes4"),
        "extension_header_mismatch": file.get("extension_header_mismatch", False),
        "groups": [
            {
                "offset": group["offset"],
                "length": group["length"],
                "label_hex": group["label_hex"],
                "group_type": group["group_type"],
            }
            for group in file.get("groups", [])
        ],
        "records": [record_semantics(record) for record in file.get("records", [])],
        "malformed": malformed_key(file.get("malformed")),
    }


def reader_scenario_semantics(
    reader: dict[str, Any], matrix: dict[str, Any]
) -> list[dict[str, Any]]:
    files = {item["path"]: item for item in reader["files"]}
    scenarios: list[dict[str, Any]] = []
    for case in matrix["cases"]:
        plugin_paths = [
            path
            for path in case["input_paths"]
            if Path(path).suffix.lower() in PLUGIN_SUFFIXES
        ]
        definitions: list[tuple[str, list[str]]] = []
        if case["operation"] == "scan" and plugin_paths:
            definitions.append((case["case_id"], plugin_paths))
        elif case["operation"] == "compare":
            definitions.extend(
                (f"{case['case_id']}.variant-{index}", [path])
                for index, path in enumerate(plugin_paths)
            )
        elif case["operation"] == "orchestrated-read":
            requests = {
                item["path"]: item.get("json_value")
                for item in reader.get("supplemental_inputs", [])
            }
            request = requests[case["input_paths"][0]]
            definitions.extend(
                [
                    (f"{case['case_id']}.initial", [request["initial_path"]]),
                    (
                        f"{case['case_id']}.replacement",
                        [request["replacement_path"]],
                    ),
                ]
            )
        for scenario_id, paths in definitions:
            population: dict[str, list[str]] = defaultdict(list)
            records: list[dict[str, Any]] = []
            for plugin_order, path in enumerate(paths):
                file = files[path]
                for record_index, record in enumerate(file.get("records", [])):
                    identity = record.get("identity")
                    if not identity or not identity.get("form_key"):
                        continue
                    locator = f"{path}#{record_index}"
                    population[identity["form_key"]].append(locator)
                    records.append(
                        {
                            "locator": locator,
                            "plugin_order": plugin_order,
                            "form_key": identity["form_key"],
                            "deleted": (int(record["flags_hex"], 16) & 0x20) != 0,
                            "links": [
                                {
                                    "field": link["field"],
                                    "occurrence": link["occurrence"],
                                    "component": link.get("component"),
                                    "form_id_hex": link["form_id_hex"],
                                    "form_key": link.get("form_key"),
                                    "resolution_state": (
                                        "unresolved"
                                        if link["resolution_state"] == "resolved"
                                        and link.get("form_key") not in population
                                        else link["resolution_state"]
                                    ),
                                }
                                for link in record.get("links", [])
                            ],
                        }
                    )
            population_keys = set(population)
            for record in records:
                for link in record["links"]:
                    if (
                        link["resolution_state"] == "unresolved"
                        and link["form_key"] in population_keys
                    ):
                        link["resolution_state"] = "resolved"
            scenarios.append(
                {
                    "scenario_id": scenario_id,
                    "plugin_paths": paths,
                    "records": records,
                    "chains": [
                        {
                            "form_key": form_key,
                            "ordered_locators": ordered,
                            "winner_locator": ordered[-1],
                        }
                        for form_key, ordered in sorted(population.items())
                        if len(ordered) >= 2
                    ],
                    "denominator": case.get("denominator"),
                }
            )
    return scenarios


def manual_agreement(
    reader: dict[str, Any], manual: dict[str, Any], matrix: dict[str, Any]
) -> None:
    manual_files = {item["path"]: item for item in manual["files"]}
    if set(manual_files) != {item["path"] for item in reader["files"]}:
        raise ValueError("Independent methods saw different TES4 file sets.")
    for item in reader["files"]:
        other = manual_files[item["path"]]
        if (item["byte_length"], item["sha256"]) != (
            other["byte_length"],
            other["sha256"],
        ):
            raise ValueError(f"Independent identity disagreement: {item['path']}")
        if malformed_key(item.get("malformed")) != malformed_key(other.get("malformed")):
            raise ValueError(
                f"Independent structural disagreement for {item['path']}: "
                f"{malformed_key(item.get('malformed'))!r} != "
                f"{malformed_key(other.get('malformed'))!r}"
            )
        reader_semantics = file_semantics(item)
        manual_semantics = file_semantics(other)
        if item.get("malformed") is not None and not item.get("records"):
            if item.get("groups"):
                raise ValueError(
                    f"Independent reader published partial groups after a framing "
                    f"failure: {item['path']}"
                )
            # The reader deliberately publishes no partial group/record objects after
            # a structural framing failure, and the oracle builder correspondingly
            # attributes no group/record facts for that file. The manual audit keeps
            # its raw traversal rows as evidence, so compare only answer-bearing
            # semantics that can reach the published oracle in this malformed case.
            reader_semantics.pop("groups")
            reader_semantics.pop("records")
            manual_semantics.pop("groups")
            manual_semantics.pop("records")
        if reader_semantics != manual_semantics:
            raise ValueError(f"Independent semantic disagreement: {item['path']}")
    reader_other = {
        item["path"]: (
            item["byte_length"],
            item["sha256"],
            item["raw_hex"],
            item.get("json_value"),
        )
        for item in reader.get("supplemental_inputs", [])
    }
    manual_other = {
        item["path"]: (
            item["byte_length"],
            item["sha256"],
            item["raw_hex"],
            item.get("json_value"),
        )
        for item in manual.get("supplemental_inputs", [])
    }
    if reader_other != manual_other:
        raise ValueError("Independent methods disagree on supplemental request/string bytes.")
    expected_scenarios = reader_scenario_semantics(reader, matrix)
    if reader.get("scenario_semantics") != expected_scenarios:
        raise ValueError("Bounded reader scenario semantics are internally inconsistent.")
    if manual.get("scenario_semantics") != reader.get("scenario_semantics"):
        raise ValueError("Independent methods disagree on scenario chains, winners, or links.")


def build(package: Path, reader_path: Path, manual_path: Path) -> None:
    reader = read_json(reader_path)
    manual = read_json(manual_path)
    matrix = read_json(package / "inputs" / "case-matrix.json")
    manual_agreement(reader, manual, matrix)
    memberships = scenario_map(package, matrix)
    files_by_path = {item["path"]: item for item in reader["files"]}
    missing = set(files_by_path) - set(memberships)
    for index, relative in enumerate(sorted(missing)):
        memberships[relative].append(
            {
                "scenario_id": f"fixture-payload-binding.{index:03d}",
                "plugin_order": 0,
            }
        )

    oracle_dir = package / "oracle"
    oracle_dir.mkdir(parents=True, exist_ok=True)
    reader_retained = oracle_dir / "independent-reader-report.json"
    manual_retained = oracle_dir / "manual-hex-worksheet.json"
    reader_retained.write_text(
        json.dumps(reader, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )
    manual_retained.write_text(
        json.dumps(manual, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )

    facts: list[dict[str, Any]] = []
    dependencies: dict[str, set[str]] = {}
    fact_counter = 0

    def add_fact(
        subject: str,
        kind: str,
        value: dict[str, Any],
        deps: set[str],
    ) -> str:
        nonlocal fact_counter
        fact_counter += 1
        fact_id = f"beth.{safe(package.name.lower())}.fact-{fact_counter:05d}"
        fingerprint = canonical_fingerprint(value)
        facts.append(
            {
                "fact_id": fact_id,
                "subject_id": safe(subject),
                "fact_kind": kind,
                "ground_truth_method_ids": METHODS,
                "canonical_value": value,
                "canonical_value_fingerprint": fingerprint,
            }
        )
        dependencies[fact_id] = set(deps)
        return fact_id

    file_entries = []
    file_keys_by_path: dict[str, str] = {}
    record_ids: dict[tuple[str, int], str] = {}
    scenario_records: dict[str, dict[str, list[str]]] = defaultdict(
        lambda: defaultdict(list)
    )
    for file_index, file in enumerate(reader["files"]):
        relative = file["path"]
        artifact_id = f"inputs/{relative}"
        file_key = f"{package.name.lower()}.file-{file_index:03d}"
        file_keys_by_path[relative] = file_key
        file_deps = {artifact_id}
        add_fact(
            f"{file_key}.identity",
            "file",
            {
                "kind": "file-identity",
                "artifact_id": artifact_id,
                "length": file["byte_length"],
                "text_value": file["sha256"],
            },
            file_deps,
        )
        tes4 = file.get("tes4")
        malformed_code = file["malformed"]["code"] if file["malformed"] else None
        esl_flag_state = "observed" if tes4 else "unknown"
        masters_state = (
            "unknown"
            if tes4 is None
            or malformed_code in {"record-size-overflow", "master-missing-data-pair"}
            else "observed"
        )
        masters_value = tes4["masters"] if tes4 and masters_state == "observed" else None
        esl_flag_value = tes4["esl_flag"] if tes4 and esl_flag_state == "observed" else None
        master_value: dict[str, Any] = {
            "kind": "tes4-master-order",
            "artifact_id": artifact_id,
        }
        if masters_state == "observed":
            master_value["master_order"] = masters_value
        else:
            master_value["resolution_state"] = "unknown"
            master_value["reason"] = "A valid TES4 master declaration cannot be established."
        add_fact(
            f"{file_key}.masters",
            "master-order",
            master_value,
            file_deps,
        )
        esl_value: dict[str, Any] = {
            "kind": "tes4-esl-flag",
            "artifact_id": artifact_id,
        }
        if esl_flag_state == "observed":
            esl_value["boolean_value"] = esl_flag_value
        else:
            esl_value["resolution_state"] = "unknown"
            esl_value["reason"] = "A complete TES4 flags word cannot be established."
        add_fact(
            f"{file_key}.esl-flag",
            "file",
            esl_value,
            file_deps,
        )
        spans = []
        span_index = 0
        all_spans = list(file["spans"])
        for record in file.get("records", []):
            all_spans.extend(record.get("logical_spans", []))
        for span in all_spans:
            if span["length"] == 0:
                continue
            span_index += 1
            spans.append(
                {
                    "span_id": f"f{file_index:03d}.span-{span_index:05d}",
                    "offset_space": span["offset_space"],
                    "offset": span["offset"],
                    "length": span["length"],
                    "classification": span["classification"],
                }
            )
        file_entries.append(
            {
                "artifact_id": artifact_id,
                "byte_length": file["byte_length"],
                "sha256": file["sha256"],
                "provider_id": f"project-authored:{safe(package.name.lower())}",
                "scenario_memberships": memberships[relative],
                "masters_state": masters_state,
                "masters": masters_value,
                "esl_flag_state": esl_flag_state,
                "esl_flag": esl_flag_value,
                "byte_coverage": spans,
            }
        )

        malformed = file["malformed"]
        if malformed is not None:
            add_fact(
                f"{file_key}.malformed",
                "malformed",
                {
                    "kind": "malformed-input",
                    "artifact_id": artifact_id,
                    "offset_space": "physical-file",
                    "offset": malformed["offset"],
                    "classification": safe(malformed["code"]),
                    "reason": malformed["detail"],
                    "record_state": "malformed",
                },
                file_deps,
            )
            continue

        for group_index, group in enumerate(file.get("groups", [])):
            add_fact(
                f"{file_key}.group-{group_index:04d}",
                "group",
                {
                    "kind": "group-header",
                    "artifact_id": artifact_id,
                    "offset_space": "physical-file",
                    "offset": group["offset"],
                    "length": group["length"],
                    "signature": "GRUP",
                    "raw_hex": group["label_hex"],
                    "unsigned_value": group["group_type"],
                },
                file_deps,
            )

        for record_index, record in enumerate(file["records"]):
            identity = record.get("identity")
            state = "deleted" if int(record["flags_hex"], 16) & 0x20 else "present"
            payload = record.get("allowlisted_payload", {})
            if (
                record["signature"] == "NPC_"
                and payload.get("TPLT")
                and payload.get("template_flags_hex") != "0000"
                and payload["TPLT"][0] != "00000000"
            ):
                state = "templated"
            value: dict[str, Any] = {
                "kind": "major-record",
                "artifact_id": artifact_id,
                "offset_space": "physical-file",
                "offset": record["offset"],
                "length": record["length"],
                "signature": record["signature"],
                "form_id_hex": record["raw_form_id_hex"],
                "record_flags_hex": record["flags_hex"],
                "record_state": state,
                "boolean_value": record["compressed"],
            }
            if identity is not None:
                value["resolution_state"] = identity["resolution_state"]
                if identity.get("form_key"):
                    value["form_key"] = identity["form_key"]
                    value["origin_plugin"] = identity["origin_plugin"]
                    value["origin_kind"] = identity["origin_kind"]
                    local = int(identity["local_id_hex"], 16)
                    value["local_id_hex"] = (
                        f"{local:03X}"
                        if identity["origin_kind"] == "light"
                        else f"{local:06X}"
                    )
            record_fact = add_fact(
                f"{file_key}.record-{record_index:04d}",
                "record",
                value,
                file_deps,
            )
            record_ids[(relative, record_index)] = record_fact
            if identity and identity.get("form_key"):
                for membership in memberships[relative]:
                    scenario_records[membership["scenario_id"]][identity["form_key"]].append(
                        record_fact
                    )

            allowed = SUPPORTED.get(record["signature"], set())
            occurrences: dict[str, list[str]] = defaultdict(list)
            logical = record["compressed"]
            for sub_index, sub in enumerate(record["subrecords"]):
                if sub["signature"] not in allowed:
                    continue
                sub_fact = add_fact(
                    f"{file_key}.record-{record_index:04d}.sub-{sub_index:04d}",
                    "subrecord",
                    {
                        "kind": "allowlisted-subrecord",
                        "artifact_id": artifact_id,
                        "offset_space": (
                            "decompressed-record" if logical else "physical-file"
                        ),
                        "offset": sub["data_offset"],
                        "length": sub["length"],
                        "signature": sub["signature"],
                        "raw_hex": sub["raw_hex"],
                    },
                    file_deps,
                )
                occurrences[sub["signature"]].append(sub_fact)
            for signature, ordered in occurrences.items():
                if len(ordered) > 1:
                    add_fact(
                        f"{file_key}.record-{record_index:04d}.{signature}.order",
                        "subrecord",
                        {
                            "kind": "repeated-field-order",
                            "artifact_id": artifact_id,
                            "signature": signature,
                            "ordered_ids": ordered,
                        },
                        file_deps,
                    )

            for membership in memberships[relative]:
                scenario = membership["scenario_id"]
                population = set(scenario_records[scenario])
                # Population is completed in a second pass below.

    # Resolve links only after every scenario population has been collected.
    for file_index, file in enumerate(reader["files"]):
        if file["malformed"] is not None:
            continue
        relative = file["path"]
        artifact_id = f"inputs/{relative}"
        file_key = f"{package.name.lower()}.file-{file_index:03d}"
        for record_index, record in enumerate(file["records"]):
            grouped: dict[tuple[str, str], list[str]] = defaultdict(list)
            for membership in memberships[relative]:
                scenario = membership["scenario_id"]
                population = set(scenario_records[scenario])
                for link_index, link in enumerate(record.get("links", [])):
                    state = link["resolution_state"]
                    target = link.get("form_key")
                    if state == "resolved" and target not in population:
                        state = "unresolved"
                    value = {
                        "kind": "canonical-link",
                        "artifact_id": artifact_id,
                        "form_id_hex": link["form_id_hex"],
                        "resolution_state": state,
                        "classification": safe(
                            f"{link['field']}-{link.get('component', 'value')}"
                        ),
                    }
                    if target:
                        value["target_form_key"] = target
                    fact_id = add_fact(
                        f"{file_key}.record-{record_index:04d}.link-{link_index:04d}.{scenario}",
                        "link",
                        value,
                        {artifact_id, f"scenario:{scenario}"},
                    )
                    grouped[(scenario, link["field"])].append(fact_id)
            for (scenario, field), ordered in grouped.items():
                if len(ordered) > 1:
                    add_fact(
                        f"{file_key}.record-{record_index:04d}.{field}.{scenario}.order",
                        "link",
                        {
                            "kind": "ordered-link-occurrences",
                            "artifact_id": artifact_id,
                            "classification": safe(field),
                            "ordered_ids": ordered,
                        },
                        {artifact_id, f"scenario:{scenario}"},
                    )

    for scenario, chains in sorted(scenario_records.items()):
        scenario_files = {
            f"inputs/{path}"
            for path, entries in memberships.items()
            if any(entry["scenario_id"] == scenario for entry in entries)
        }
        for form_key, ordered in sorted(chains.items()):
            if len(ordered) < 2:
                continue
            chain_fact = add_fact(
                f"{package.name.lower()}.{scenario}.{form_key}.chain",
                "override-chain",
                {
                    "kind": "override-chain",
                    "form_key": form_key,
                    "ordered_ids": ordered,
                    "winner_id": ordered[-1],
                },
                scenario_files | {f"scenario:{scenario}"},
            )
            add_fact(
                f"{package.name.lower()}.{scenario}.{form_key}.winner",
                "winner",
                {
                    "kind": "winning-record",
                    "form_key": form_key,
                    "winner_id": ordered[-1],
                    "ordered_ids": [chain_fact],
                },
                scenario_files | {f"scenario:{scenario}"},
            )

    # Semantic-invalid boundaries that are structurally framed.
    for file_index, file in enumerate(reader["files"]):
        relative = file["path"]
        artifact_id = f"inputs/{relative}"
        if file["malformed"] is not None:
            continue
        if package.name == "BETH-MALFORMED-VAL":
            invalid_record = next(
                (
                    record
                    for record in file["records"]
                    if record.get("identity", {}).get("resolution_state") == "invalid"
                ),
                None,
            )
            invalid_link = next(
                (
                    link
                    for record in file["records"]
                    for link in record.get("links", [])
                    if link["resolution_state"] == "invalid"
                ),
                None,
            )
            if invalid_record or invalid_link:
                add_fact(
                    f"{package.name.lower()}.semantic-invalid-{file_index}",
                    "malformed",
                    {
                        "kind": "malformed-input",
                        "artifact_id": artifact_id,
                        "offset_space": "not-applicable",
                        "classification": (
                            "invalid-record-master-index"
                            if invalid_record
                            else "invalid-link-master-index"
                        ),
                        "reason": (
                            invalid_record["identity"]["reason"]
                            if invalid_record
                            else invalid_link["reason"]
                        ),
                        "record_state": "malformed",
                    },
                    {artifact_id},
                )

    # Case-level invalid/unsupported denominators.
    for case in matrix["cases"]:
        case_id = safe(case["case_id"])
        denominator = case.get("denominator")
        if package.name == "BETH-UNSUPPORTED-VAL":
            add_fact(
                f"{package.name.lower()}.{case_id}",
                "coverage-gap",
                {
                    "kind": "unsupported-denominator",
                    "classification": safe(denominator),
                    "reason": f"{case_id} is outside the EVAL-0052 positive allowlist.",
                    "eligible_count": 1,
                    "completed_count": 0,
                    "gap_count": 1,
                    "resolution_state": "unsupported",
                },
                {f"scenario:{case_id}"},
            )
        elif package.name == "BETH-LIGHT-VAL" and (
            "below" in case_id or "above" in case_id or "out-of-range" in case_id
            or "header-mismatch" in case_id
        ):
            add_fact(
                f"{package.name.lower()}.{case_id}",
                "malformed",
                {
                    "kind": "invalid-light-boundary",
                    "classification": safe(denominator or "light-header-or-local-id"),
                    "reason": f"{case_id} is an explicit invalid full/light boundary.",
                    "record_state": "malformed",
                },
                {f"scenario:{case_id}"},
            )
        elif case["operation"] == "orchestrated-read":
            add_fact(
                f"{package.name.lower()}.{case_id}",
                "malformed",
                {
                    "kind": "changed-during-read",
                    "classification": "changed-during-read",
                    "reason": "Identity and content reads observe different frozen source files.",
                    "record_state": "malformed",
                },
                {f"scenario:{case_id}.initial", f"scenario:{case_id}.replacement"},
            )

    malformed_case_ids = [
        safe(case["case_id"])
        for case in matrix["cases"]
        if (
            package.name == "BETH-MALFORMED-VAL"
            and case["case_id"] != "malformed-control"
        )
        or (
            package.name == "BETH-REFR-DEV"
            and any(
                token in case["case_id"]
                for token in ("truncated-subrecord", "body-overrun", "dangling-extended")
            )
        )
    ]
    if malformed_case_ids:
        add_fact(
            f"{package.name.lower()}.malformed-denominator",
            "snapshot",
            {
                "kind": "malformed-population-denominator",
                "classification": "malformed-input",
                "reason": "Every named malformed member has a pre-registered earliest failure.",
                "eligible_count": len(malformed_case_ids),
                "completed_count": len(malformed_case_ids),
                "gap_count": 0,
                "resolution_state": "not-applicable",
            },
            {f"scenario:{case_id}" for case_id in malformed_case_ids},
        )

    mutation_expectations = []
    mutation_kinds = {
        "one-byte": "one-byte",
        "master-reindexing": "master-order",
        "record-order": "record-order",
        "compression-equivalence": "compression",
        "repeated": "repeated-field",
        "local-id": "local-id",
        "changed-during-read": "changed-during-read",
        "below-range": "local-id",
        "above-range": "local-id",
        "reference-out-of-range": "local-id",
        "header-mismatch": "one-byte",
    }
    fact_ids = {fact["fact_id"] for fact in facts}
    facts_by_id = {fact["fact_id"]: fact for fact in facts}
    logical_value_cache: dict[str, Any] = {}

    def normalize_logical_value(value: Any) -> Any:
        if isinstance(value, dict):
            return {
                key: normalize_logical_value(item)
                for key, item in sorted(value.items())
                if key != "artifact_id"
            }
        if isinstance(value, list):
            return [normalize_logical_value(item) for item in value]
        if isinstance(value, str) and value in facts_by_id:
            if value not in logical_value_cache:
                logical_value_cache[value] = normalize_logical_value(
                    facts_by_id[value]["canonical_value"]
                )
            return {"referenced_fact": logical_value_cache[value]}
        return value

    def logical_signature(fact_id: str) -> str:
        fact = facts_by_id[fact_id]
        return json.dumps(
            {
                "fact_kind": fact["fact_kind"],
                "canonical_value": normalize_logical_value(fact["canonical_value"]),
            },
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
        )

    def file_fact_ids(relative: str) -> set[str]:
        prefix = f"{safe(file_keys_by_path[relative])}."
        return {
            fact["fact_id"]
            for fact in facts
            if fact["subject_id"].startswith(prefix)
        }

    def scenario_fact_ids(prefix: str) -> set[str]:
        return {
            fact_id
            for fact_id, deps in dependencies.items()
            if any(dep.startswith(f"scenario:{prefix}") for dep in deps)
            and not any(
                facts_by_id[fact_id]["subject_id"].startswith(
                    f"{safe(file_key)}."
                )
                for file_key in file_keys_by_path.values()
            )
        }

    def changed_by_logical_comparison(
        baseline_path: str,
        target_path: str,
        baseline_scenario: str | None,
        target_scenario: str | None,
    ) -> set[str]:
        baseline_ids = file_fact_ids(baseline_path)
        target_ids = file_fact_ids(target_path)
        if baseline_scenario:
            baseline_ids |= scenario_fact_ids(baseline_scenario)
        if target_scenario:
            target_ids |= scenario_fact_ids(target_scenario)
        available = Counter(logical_signature(fact_id) for fact_id in baseline_ids)
        changed_ids: set[str] = set()
        for fact_id in sorted(target_ids):
            signature = logical_signature(fact_id)
            if available[signature] > 0:
                available[signature] -= 1
            else:
                changed_ids.add(fact_id)
        return changed_ids

    for case in matrix["cases"]:
        kind = next(
            (value for needle, value in mutation_kinds.items() if needle in case["case_id"]),
            None,
        )
        if kind is None:
            continue
        tes4_paths = [
            path for path in case["input_paths"] if Path(path).suffix.lower() in PLUGIN_SUFFIXES
        ]
        case_id = case["case_id"]
        if case["operation"] == "compare":
            baseline_path, target_path = tes4_paths[0], tes4_paths[-1]
            changed = changed_by_logical_comparison(
                baseline_path,
                target_path,
                safe(f"{case_id}.variant-0"),
                safe(f"{case_id}.variant-{len(tes4_paths) - 1}"),
            )
        elif case["operation"] == "scan":
            baseline_path = SCAN_MUTATION_BASELINES[case_id]
            target_path = tes4_paths[-1]
            changed = changed_by_logical_comparison(
                baseline_path,
                target_path,
                None,
                safe(case_id),
            )
        else:
            request = read_json(package / "inputs" / case["input_paths"][0])
            baseline_path = request["initial_path"]
            target_path = request["replacement_path"]
            changed = changed_by_logical_comparison(
                baseline_path,
                target_path,
                safe(f"{case_id}.initial"),
                safe(f"{case_id}.replacement"),
            )
            changed |= {
                fact_id
                for fact_id, deps in dependencies.items()
                if any("changed-during-read" in dep for dep in deps)
            }
        target = f"inputs/{target_path}"
        mutation_expectations.append(
            {
                "mutation_id": safe(f"mutation.{case['case_id']}"),
                "mutation_kind": kind,
                "target_artifact_id": target,
                "changed_fact_ids": sorted(changed),
                "unchanged_fact_ids": sorted(fact_ids - changed),
            }
        )

    repository_root = Path(__file__).resolve().parents[4]
    dossier = (
        repository_root
        / "docs"
        / "evaluation"
        / "fixtures"
        / "bethesda-byte-format-evidence-v1.md"
    )
    supplemental = {
        "schema_id": "infinium.evaluation.bethesda-byte-oracle/v1",
        "schema_version": "1",
        "fixture_id": package.name,
        "fixture_version": FIXTURE_VERSION,
        "oracle_artifact_version": ORACLE_VERSION,
        "canonicalization": "infinium-canonical-json-sha256/v1",
        "independent_authors_and_reviewers": ["oracle-reviewer"],
        "ground_truth_method_ids": METHODS,
        "format_evidence": [
            {
                "artifact_id": "docs/evaluation/fixtures/bethesda-byte-format-evidence-v1.md",
                "artifact_version": "1",
                "fingerprint": sha(dossier),
                "availability": "retained",
            }
        ],
        "files": file_entries,
        "facts": facts,
        "mutation_expectations": mutation_expectations,
        "limits": reader["limits"],
        "review_state": "reviewed",
    }
    supplemental_path = oracle_dir / "independent-byte-facts.json"
    supplemental_path.write_text(
        json.dumps(supplemental, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )

    observations = []
    invalid = []
    gaps = []
    for fact in facts:
        item_type = (
            "invalid-input"
            if fact["fact_kind"] == "malformed"
            else "coverage-gap"
            if fact["fact_kind"] in {"unsupported", "coverage-gap"}
            else "observation"
        )
        item = {
            "expected_id": fact["fact_id"],
            "subject_id": fact["subject_id"],
            "expected_type": item_type,
            "expected_state": (
                "failed"
                if item_type == "invalid-input"
                else "unsupported"
                if item_type == "coverage-gap"
                else "present"
            ),
            "ground_truth_method_ids": METHODS,
            "canonical_value_fingerprint": fact["canonical_value_fingerprint"],
        }
        (invalid if item_type == "invalid-input" else gaps if item_type == "coverage-gap" else observations).append(item)

    refs = {
        "reader": {
            "artifact_id": "oracle/independent-reader-report.json",
            "artifact_version": "1",
            "fingerprint": sha(reader_retained),
            "availability": "retained",
        },
        "manual": {
            "artifact_id": "oracle/manual-hex-worksheet.json",
            "artifact_version": "1",
            "fingerprint": sha(manual_retained),
            "availability": "retained",
        },
        "supplemental": {
            "artifact_id": "oracle/independent-byte-facts.json",
            "artifact_version": ORACLE_VERSION,
            "fingerprint": sha(supplemental_path),
            "availability": "retained",
        },
    }
    taxonomy_path = oracle_dir / "taxonomy-projections.json"
    if taxonomy_path.exists():
        refs["taxonomy"] = {
            "artifact_id": "oracle/taxonomy-projections.json",
            "artifact_version": "1.1.0",
            "fingerprint": sha(taxonomy_path),
            "availability": "retained",
            "byte_length": taxonomy_path.stat().st_size,
        }
    state_names = [
        "observations", "deterministic_results", "external_claims", "application_links",
        "discovery_leads", "model_proposals", "proposal_admissions", "candidates",
        "hypotheses", "findings", "recommendations", "supported_cases",
        "lead_only_cases", "abstentions", "invalid_inputs", "coverage_gaps", "failures",
    ]
    states = {
        name: {
            "state": (
                "populated"
                if (name == "observations" or (name == "invalid_inputs" and invalid)
                    or (name == "coverage_gaps" and gaps))
                else "empty"
            ),
            "reason": (
                "Pre-registered independent byte expectations are present."
                if (name == "observations" or (name == "invalid_inputs" and invalid)
                    or (name == "coverage_gaps" and gaps))
                else "No Slice 3.5 expectation is assigned to this collection."
            ),
        }
        for name in state_names
    }
    expected = {
        "fixture_id": package.name,
        "fixture_version": FIXTURE_VERSION,
        "oracle_version": ORACLE_VERSION,
        "independent_authors_and_reviewers": ["oracle-reviewer"],
        "ground_truth_methods": [
            {
                "method_id": METHODS[0],
                "method": "Manual annotated hexadecimal offset worksheet over frozen bytes.",
                "evidence_references": [refs["manual"]],
                "independent_of_system_under_test": True,
            },
            {
                "method_id": METHODS[1],
                "method": "Separately implemented bounded raw TES4 byte reader.",
                "evidence_references": [refs["reader"], refs["supplemental"]],
                "independent_of_system_under_test": True,
            },
            *(
                [
                    {
                        "method_id": "independent-taxonomy-projection-review-v1",
                        "method": (
                            "Exhaustive taxonomy projection and literal subject-binding review "
                            "from frozen byte facts and the accepted public subject-ID contract."
                        ),
                        "evidence_references": [refs["taxonomy"]],
                        "independent_of_system_under_test": True,
                    }
                ]
                if "taxonomy" in refs
                else []
            ),
        ],
        "expected_observations": observations,
        **{name: [] for name in EMPTY_COLLECTIONS},
        "expected_invalid_inputs": invalid,
        "expected_coverage_and_gaps": gaps,
        "expected_collection_states": states,
        "expected_taxonomy_assignments": [],
        "expected_replayability": "complete-clean",
        "forbidden_claims": [
            {
                "claim_id": f"forbidden-{index:02d}",
                "claim_type": claim,
                "reason": reason,
            }
            for index, (claim, reason) in enumerate(
                [
                    ("raw-formid-is-formkey", "File-local indices are not canonical identity."),
                    ("extension-proves-light", "Only the TES4 light flag establishes light origin."),
                    ("template-inheritance", "Template inheritance is outside the frozen allowlist."),
                    ("record-family-proves-meaning", "Record family does not prove purpose or gameplay meaning."),
                    ("conflict-is-finding", "A structural conflict alone is not a finding."),
                    ("xesp-enable-parent", "XESP is outside the EVAL-0052 positive allowlist."),
                    ("archive-or-string-resolution", "Archive and localized-string resolution are unsupported."),
                    ("environment-discovery", "Automatic environment discovery is unsupported."),
                ],
                1,
            )
        ],
        "known_limits": [
            "The oracle covers only the frozen EVAL-0052 positive allowlist.",
            "Decompressed logical offsets are distinct from physical file offsets.",
            "No production parser, Mutagen, xEdit, held-out input, or taxonomy answer authored these values.",
        ],
        "pre_registered_at": "2026-07-30T00:00:00.0000000+00:00",
        "change_history": [
            {
                "oracle_version": "1.0.1",
                "changed_at": "2026-07-30T23:00:00.0000000+00:00",
                "independent_evidence_reference": refs["manual"],
                "prior_error_explanation": (
                    "Version 1.0.0 attributed the manual method without comparing "
                    "its decoded semantic answers and classified mutation changes "
                    "from artifact dependencies instead of logical baseline deltas."
                ),
                "reviewer": "oracle-reviewer",
            },
            {
                "oracle_version": "1.0.2",
                "changed_at": "2026-08-01T22:00:00.0000000+00:00",
                "independent_evidence_reference": refs["manual"],
                "prior_error_explanation": (
                    "Fixture version 1.0.0 encoded RACE flags at the wrong DATA "
                    "offset and placed REFR records outside canonical CELL children."
                ),
                "reviewer": "oracle-reviewer",
            },
            {
                "oracle_version": ORACLE_VERSION,
                "changed_at": ORACLE_CHANGED_AT,
                "independent_evidence_reference": refs.get("taxonomy", refs["manual"]),
                "prior_error_explanation": (
                    (
                        "Fixture version 1.0.1 did not seal exhaustive literal taxonomy "
                        "subject bindings or retain the taxonomy projection inside the "
                        "expected oracle's exact reference closure."
                    )
                    if "taxonomy" in refs
                    else (
                        "Fixture version 1.0.1 was uniformly resealed so the public "
                        "Bethesda suite shares one versioned exact-reference-closure "
                        "reader contract; this package had no taxonomy or physical "
                        "oracle-closure defect."
                    )
                ),
                "reviewer": "oracle-reviewer",
            },
        ],
    }
    (package / "expected-oracle.json").write_text(
        json.dumps(expected, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    parser.add_argument("reader_report", type=Path)
    parser.add_argument("manual_report", type=Path)
    args = parser.parse_args()
    build(args.package.resolve(), args.reader_report.resolve(), args.manual_report.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
