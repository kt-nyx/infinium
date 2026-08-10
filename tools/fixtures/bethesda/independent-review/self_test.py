#!/usr/bin/env python3
"""Bounded regression checks for independent Bethesda oracle qualification."""

from __future__ import annotations

import argparse
import copy
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Callable


def load_builder(path: Path):
    spec = importlib.util.spec_from_file_location("bethesda_build_oracles", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def expect_rejection(action: Callable[[], None], label: str) -> None:
    try:
        action()
    except ValueError:
        return
    raise AssertionError(f"{label} was accepted")


def generate_reports(
    repository: Path,
    package: Path,
    output: Path,
) -> tuple[Path, Path]:
    reader = output / f"{package.name}-reader.json"
    manual = output / f"{package.name}-manual.json"
    subprocess.run(
        [
            sys.executable,
            str(
                repository
                / "tools/fixtures/bethesda/independent-review"
                / "bounded_raw_reader.py"
            ),
            str(package),
            str(reader),
        ],
        check=True,
    )
    subprocess.run(
        [
            "pwsh",
            "-NoProfile",
            "-File",
            str(
                repository
                / "tools/fixtures/bethesda/independent-review"
                / "manual_hex_audit.ps1"
            ),
            "-PackagePath",
            str(package),
            "-OutputPath",
            str(manual),
        ],
        check=True,
    )
    return reader, manual


def first_record(
    report: dict[str, Any], predicate: Callable[[dict[str, Any]], bool]
) -> dict[str, Any]:
    return next(
        record
        for file in report["files"]
        for record in file.get("records", [])
        if predicate(record)
    )


def assert_independent_rejections(
    builder: Any,
    reader: dict[str, Any],
    manual: dict[str, Any],
    matrix: dict[str, Any],
) -> None:
    builder.manual_agreement(reader, manual, matrix)

    wrong_form_key = copy.deepcopy(reader)
    identity = first_record(
        wrong_form_key,
        lambda record: bool(record.get("identity", {}).get("form_key")),
    )["identity"]
    identity["form_key"] = f"DEADBEEF:{identity['origin_plugin']}"
    expect_rejection(
        lambda: builder.manual_agreement(wrong_form_key, manual, matrix),
        "reader FormKey corruption",
    )

    wrong_link = copy.deepcopy(reader)
    link = first_record(wrong_link, lambda record: bool(record.get("links")))["links"][0]
    link["form_key"] = "DEADBEEF:Missing.esm"
    link["resolution_state"] = "resolved"
    expect_rejection(
        lambda: builder.manual_agreement(wrong_link, manual, matrix),
        "reader link corruption",
    )

    wrong_chain = copy.deepcopy(reader)
    chain = next(
        chain
        for scenario in wrong_chain["scenario_semantics"]
        for chain in scenario["chains"]
    )
    chain["winner_locator"] = chain["ordered_locators"][0]
    expect_rejection(
        lambda: builder.manual_agreement(wrong_chain, manual, matrix),
        "reader chain corruption",
    )

    empty_manual_records = copy.deepcopy(manual)
    next(
        file for file in empty_manual_records["files"] if file.get("records")
    )["records"] = []
    expect_rejection(
        lambda: builder.manual_agreement(reader, empty_manual_records, matrix),
        "empty manual record semantics",
    )

    empty_manual_scenarios = copy.deepcopy(manual)
    empty_manual_scenarios["scenario_semantics"] = []
    expect_rejection(
        lambda: builder.manual_agreement(reader, empty_manual_scenarios, matrix),
        "empty manual scenario semantics",
    )


def assert_one_byte_partition(
    oracle: dict[str, Any],
    mutation_id: str,
    expected_signature: str,
) -> None:
    mutation = next(
        item
        for item in oracle["mutation_expectations"]
        if item["mutation_id"] == mutation_id
    )
    facts = {fact["fact_id"]: fact for fact in oracle["facts"]}
    changed = [facts[fact_id] for fact_id in mutation["changed_fact_ids"]]
    changed_shapes = sorted(
        (
            fact["fact_kind"],
            fact["canonical_value"].get("kind"),
            fact["canonical_value"].get("signature"),
        )
        for fact in changed
    )
    expected_shapes = sorted(
        [
            ("file", "file-identity", None),
            ("subrecord", "allowlisted-subrecord", expected_signature),
        ]
    )
    if changed_shapes != expected_shapes:
        raise AssertionError(
            f"{mutation_id} changed unrelated facts: {changed_shapes}"
        )
    if set(mutation["changed_fact_ids"]) | set(mutation["unchanged_fact_ids"]) != set(
        facts
    ):
        raise AssertionError(f"{mutation_id} partitions do not cover all facts")
    if set(mutation["changed_fact_ids"]) & set(mutation["unchanged_fact_ids"]):
        raise AssertionError(f"{mutation_id} partitions overlap")


def assert_malformed_classification(
    report: dict[str, Any], artifact_id: str, expected: str
) -> None:
    report_path = artifact_id.removeprefix("inputs/")
    file = next(item for item in report["files"] if item["path"] == report_path)
    malformed = file.get("malformed")
    if malformed is None or malformed.get("code") != expected:
        actual = None if malformed is None else malformed.get("code")
        raise AssertionError(
            f"{artifact_id} classified as {actual!r}, expected {expected!r}"
        )


def assert_invalid_link_is_the_only_semantic_defect(
    report: dict[str, Any], scenario_id: str
) -> None:
    scenario = next(
        item for item in report["scenario_semantics"] if item["scenario_id"] == scenario_id
    )
    links = [link for record in scenario["records"] for link in record["links"]]
    if len(links) != 1 or links[0]["resolution_state"] != "invalid":
        raise AssertionError(f"{scenario_id} does not isolate one invalid link")


def assert_taxonomy_builder_dependency_closed(
    taxonomy_builder: Any,
    fixture_root: Path,
    temporary: Path,
) -> None:
    for fixture_id in (
        "BETH-NPC-DEV",
        "BETH-REFR-DEV",
        "BETH-UNSUPPORTED-VAL",
    ):
        source = fixture_root / fixture_id
        package = temporary / "taxonomy-two-source" / fixture_id
        (package / "oracle").mkdir(parents=True)
        (package / "inputs/snapshot").mkdir(parents=True)
        shutil.copy2(
            source / "oracle/independent-byte-facts.json",
            package / "oracle/independent-byte-facts.json",
        )
        shutil.copy2(
            source / "inputs/snapshot/accepted-order.json",
            package / "inputs/snapshot/accepted-order.json",
        )

        taxonomy_builder.build(package)
        for relative in (
            "oracle/taxonomy-projections.json",
            "inputs/taxonomy-subject-bindings.json",
        ):
            if (package.joinpath(relative).read_bytes() != source.joinpath(relative).read_bytes()):
                raise AssertionError(
                    f"{fixture_id} taxonomy output changed in declared two-source replay: {relative}"
                )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repository",
        type=Path,
        default=Path(__file__).resolve().parents[4],
    )
    args = parser.parse_args()
    repository = args.repository.resolve()
    tool_root = (
        repository / "tools/fixtures/bethesda/independent-review"
    )
    builder = load_builder(tool_root / "build_oracles.py")
    taxonomy_builder = load_builder(tool_root / "build_taxonomy_projections.py")
    fixture_root = repository / "test-data/public-fixtures/bethesda"

    with tempfile.TemporaryDirectory(prefix="infinium-bethesda-oracle-self-test-") as raw:
        temporary = Path(raw)
        reports: dict[str, tuple[Path, Path]] = {}
        for fixture_id in (
            "BETH-NPC-DEV",
            "BETH-REFR-DEV",
            "BETH-MALFORMED-VAL",
        ):
            source = fixture_root / fixture_id
            reports[fixture_id] = generate_reports(
                repository, source, temporary / "reports"
            )

        npc_reader = read_json(reports["BETH-NPC-DEV"][0])
        npc_manual = read_json(reports["BETH-NPC-DEV"][1])
        npc_matrix = read_json(
            fixture_root / "BETH-NPC-DEV/inputs/case-matrix.json"
        )
        assert_independent_rejections(
            builder, npc_reader, npc_manual, npc_matrix
        )

        refr_reader = read_json(reports["BETH-REFR-DEV"][0])
        assert_malformed_classification(
            refr_reader,
            "inputs/mutations/Refr-SubrecordHeaderTruncated.esp",
            "truncated-subrecord-header",
        )
        malformed_reader = read_json(reports["BETH-MALFORMED-VAL"][0])
        assert_invalid_link_is_the_only_semantic_defect(
            malformed_reader,
            "malformed-link-master-index",
        )

        for fixture_id, mutation_id, signature in (
            (
                "BETH-NPC-DEV",
                "mutation.npc-one-byte-field-change",
                "AIDT",
            ),
            (
                "BETH-REFR-DEV",
                "mutation.refr-one-byte-data-change",
                "DATA",
            ),
        ):
            package = temporary / fixture_id
            shutil.copytree(fixture_root / fixture_id, package)
            reader_path, manual_path = reports[fixture_id]
            builder.build(package, reader_path, manual_path)
            oracle = read_json(package / "oracle/independent-byte-facts.json")
            assert_one_byte_partition(oracle, mutation_id, signature)

        assert_taxonomy_builder_dependency_closed(
            taxonomy_builder,
            fixture_root,
            temporary,
        )

    print(
        "PASS: independent FormKey/link/chain/manual corruption rejection; "
        "AIDT/DATA logical mutation partitions; isolated malformed boundary; "
        "taxonomy builder exact two-source replay"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
