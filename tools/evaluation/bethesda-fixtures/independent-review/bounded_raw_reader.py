#!/usr/bin/env python3
"""Independent bounded TES4 byte reader for M1 Slice 3.5 fixtures.

This deliberately has no dependency on Infinium production code, the fixture
generator, Mutagen, or xEdit. It emits observations only; it does not emit the
accepted oracle.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any

MAX_INPUT = 64 * 1024 * 1024
MAX_DECOMPRESSED = 64 * 1024 * 1024
MAX_RECORDS = 4096
MAX_SUBRECORDS = 4096
MAX_GROUP_DEPTH = 64
PLUGIN_SUFFIXES = {".esm", ".esp", ".esl"}


class Malformed(Exception):
    def __init__(self, code: str, offset: int, detail: str):
        super().__init__(f"{code} at {offset}: {detail}")
        self.code = code
        self.offset = offset
        self.detail = detail


def u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def sig(data: bytes, offset: int) -> str:
    return data[offset : offset + 4].decode("ascii", "replace")


def hx(data: bytes) -> str:
    return data.hex().upper()


@dataclass
class Bounds:
    records: int = 0


def add_span(
    spans: list[dict[str, Any]],
    offset_space: str,
    offset: int,
    length: int,
    classification: str,
) -> None:
    spans.append(
        {
            "offset_space": offset_space,
            "offset": offset,
            "length": length,
            "classification": classification,
        }
    )


def parse_subrecords(
    payload: bytes,
    offset_space: str,
    base_offset: int,
    spans: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    cursor = 0
    pending_extended: int | None = None
    count = 0
    while cursor < len(payload):
        if len(payload) - cursor < 6:
            raise Malformed(
                "truncated-subrecord-header",
                base_offset + cursor,
                f"{len(payload) - cursor} bytes remain",
            )
        signature = sig(payload, cursor)
        declared16 = u16(payload, cursor + 4)
        header_offset = base_offset + cursor
        add_span(spans, offset_space, header_offset, 6, "subrecord-header")
        cursor += 6
        count += 1
        if count > MAX_SUBRECORDS:
            raise Malformed(
                "subrecord-count-over-limit",
                header_offset,
                f"more than {MAX_SUBRECORDS} subrecords",
            )

        if signature == "XXXX":
            if pending_extended is not None:
                raise Malformed(
                    "chained-extended-size",
                    header_offset,
                    "XXXX cannot follow a pending XXXX",
                )
            if declared16 != 4 or len(payload) - cursor < 4:
                raise Malformed(
                    "invalid-extended-size",
                    header_offset,
                    "XXXX must contain exactly four bytes",
                )
            pending_extended = u32(payload, cursor)
            raw = payload[cursor : cursor + 4]
            add_span(spans, offset_space, base_offset + cursor, 4, "subrecord-data")
            result.append(
                {
                    "signature": signature,
                    "header_offset": header_offset,
                    "data_offset": base_offset + cursor,
                    "length": 4,
                    "raw_hex": hx(raw),
                }
            )
            cursor += 4
            continue

        declared = pending_extended if pending_extended is not None else declared16
        pending_extended = None
        if declared > len(payload) - cursor:
            raise Malformed(
                "subrecord-body-overrun",
                header_offset,
                f"declared {declared}, available {len(payload) - cursor}",
            )
        raw = payload[cursor : cursor + declared]
        add_span(spans, offset_space, base_offset + cursor, declared, "subrecord-data")
        result.append(
            {
                "signature": signature,
                "header_offset": header_offset,
                "data_offset": base_offset + cursor,
                "length": declared,
                "raw_hex": hx(raw),
            }
        )
        cursor += declared

    if pending_extended is not None:
        raise Malformed(
            "dangling-extended-size",
            base_offset + cursor,
            "XXXX has no following subrecord",
        )
    return result


def parse_record(
    data: bytes,
    offset: int,
    end: int,
    spans: list[dict[str, Any]],
    bounds: Bounds,
) -> tuple[dict[str, Any], int]:
    if end - offset < 24:
        raise Malformed(
            "truncated-record-header", offset, f"{end - offset} bytes remain"
        )
    signature = sig(data, offset)
    size = u32(data, offset + 4)
    flags = u32(data, offset + 8)
    raw_form_id = u32(data, offset + 12)
    payload_offset = offset + 24
    if size > 0xFFFFFFFF - payload_offset:
        raise Malformed(
            "record-size-overflow",
            offset,
            f"declared {size} overflows a 32-bit end offset",
        )
    if size > end - payload_offset:
        raise Malformed(
            "record-size-past-end",
            offset,
            f"declared {size}, available {end - payload_offset}",
        )
    record_end = payload_offset + size
    add_span(spans, "physical-file", offset, 24, "record-header")
    bounds.records += 1
    if bounds.records > MAX_RECORDS:
        raise Malformed(
            "record-count-over-limit", offset, f"more than {MAX_RECORDS} records"
        )

    physical_payload = data[payload_offset:record_end]
    compressed = (flags & 0x00040000) != 0
    logical_spans: list[dict[str, Any]] = []
    if compressed:
        add_span(
            spans,
            "physical-file",
            payload_offset,
            size,
            "compressed-container",
        )
        if size < 4:
            raise Malformed(
                "compressed-missing-length",
                payload_offset,
                "compressed record has no four-byte declared length",
            )
        declared = u32(physical_payload, 0)
        if declared > MAX_DECOMPRESSED:
            raise Malformed(
                "compressed-declared-size-over-limit",
                payload_offset,
                f"declared {declared}",
            )
        inflater = zlib.decompressobj()
        try:
            logical = inflater.decompress(
                physical_payload[4:], MAX_DECOMPRESSED + 1
            )
            logical += inflater.flush()
        except zlib.error as exc:
            raise Malformed(
                "compressed-invalid-zlib", payload_offset + 4, str(exc)
            ) from exc
        if len(logical) > MAX_DECOMPRESSED:
            raise Malformed(
                "compressed-output-over-limit", payload_offset + 4, str(len(logical))
            )
        if not inflater.eof or inflater.unused_data or inflater.unconsumed_tail:
            raise Malformed(
                "compressed-trailing-or-incomplete",
                payload_offset + 4,
                "zlib stream is incomplete or has trailing data",
            )
        if len(logical) != declared:
            raise Malformed(
                "compressed-size-mismatch",
                payload_offset,
                f"declared {declared}, actual {len(logical)}",
            )
        subrecords = parse_subrecords(
            logical, "decompressed-record", 0, logical_spans
        )
    else:
        subrecords = parse_subrecords(
            physical_payload, "physical-file", payload_offset, spans
        )

    return (
        {
            "signature": signature,
            "offset": offset,
            "length": 24 + size,
            "data_length": size,
            "flags_hex": f"{flags:08X}",
            "raw_form_id_hex": f"{raw_form_id:08X}",
            "compressed": compressed,
            "subrecords": subrecords,
            "logical_spans": logical_spans,
        },
        record_end,
    )


def parse_elements(
    data: bytes,
    start: int,
    end: int,
    depth: int,
    spans: list[dict[str, Any]],
    bounds: Bounds,
) -> list[dict[str, Any]]:
    if depth > MAX_GROUP_DEPTH:
        raise Malformed(
            "group-depth-over-limit", start, f"depth {depth} exceeds {MAX_GROUP_DEPTH}"
        )
    result: list[dict[str, Any]] = []
    cursor = start
    while cursor < end:
        if end - cursor < 4:
            raise Malformed("truncated-signature", cursor, f"{end-cursor} bytes remain")
        if sig(data, cursor) == "GRUP":
            if end - cursor < 24:
                raise Malformed(
                    "truncated-group-header", cursor, f"{end-cursor} bytes remain"
                )
            size = u32(data, cursor + 4)
            if size < 24:
                raise Malformed("group-size-too-small", cursor, f"declared {size}")
            if size > 0xFFFFFFFF - cursor:
                raise Malformed(
                    "group-size-overflow",
                    cursor,
                    f"declared {size} overflows a 32-bit end offset",
                )
            if size > end - cursor:
                raise Malformed(
                    "group-size-past-end",
                    cursor,
                    f"declared {size}, available {end-cursor}",
                )
            group_end = cursor + size
            add_span(spans, "physical-file", cursor, 24, "group-header")
            children = parse_elements(
                data, cursor + 24, group_end, depth + 1, spans, bounds
            )
            result.append(
                {
                    "signature": "GRUP",
                    "offset": cursor,
                    "length": size,
                    "label_hex": hx(data[cursor + 8 : cursor + 12]),
                    "group_type": u32(data, cursor + 12),
                    "children": children,
                }
            )
            cursor = group_end
        else:
            record, cursor = parse_record(data, cursor, end, spans, bounds)
            result.append(record)
    return result


def flattened_records(elements: list[dict[str, Any]]):
    for element in elements:
        if element["signature"] == "GRUP":
            yield from flattened_records(element["children"])
        else:
            yield element


def flattened_groups(elements: list[dict[str, Any]]):
    for element in elements:
        if element["signature"] == "GRUP":
            yield {
                "offset": element["offset"],
                "length": element["length"],
                "label_hex": element["label_hex"],
                "group_type": element["group_type"],
            }
            yield from flattened_groups(element["children"])


def subrecord_values(record: dict[str, Any], signature: str) -> list[bytes]:
    return [
        bytes.fromhex(item["raw_hex"])
        for item in record["subrecords"]
        if item["signature"] == signature
    ]


def parse_masters(tes4: dict[str, Any]) -> list[str]:
    masters: list[str] = []
    subrecords = tes4["subrecords"]
    index = 0
    while index < len(subrecords):
        item = subrecords[index]
        if item["signature"] == "MAST":
            raw = bytes.fromhex(item["raw_hex"])
            if not raw.endswith(b"\0"):
                raise Malformed(
                    "master-not-zero-terminated",
                    item["data_offset"],
                    "MAST lacks zero terminator",
                )
            if index + 1 >= len(subrecords) or subrecords[index + 1]["signature"] != "DATA":
                raise Malformed(
                    "master-missing-data-pair",
                    item["header_offset"],
                    "MAST is not immediately followed by DATA",
                )
            masters.append(raw[:-1].decode("ascii"))
            index += 2
            continue
        index += 1
    return masters


def analyze_payload(record: dict[str, Any]) -> dict[str, Any] | None:
    signature = record["signature"]
    fields: dict[str, Any] = {}
    if signature == "NPC_":
        for name in ("ACBS", "TPLT", "RNAM", "AIDT", "PKID", "PNAM", "HCLF"):
            values = subrecord_values(record, name)
            if values:
                fields[name] = [hx(value) for value in values]
        if "ACBS" in fields:
            raw = bytes.fromhex(fields["ACBS"][0])
            if len(raw) != 24:
                raise Malformed("invalid-npc-acbs-length", record["offset"], str(len(raw)))
            fields["configuration_flags_hex"] = f"{u32(raw, 0):08X}"
            fields["template_flags_hex"] = f"{u16(raw, 18):04X}"
        for name in ("TPLT", "RNAM", "PKID", "PNAM", "HCLF"):
            for raw_hex in fields.get(name, []):
                if len(bytes.fromhex(raw_hex)) != 4:
                    raise Malformed(
                        f"invalid-npc-{name.lower()}-length",
                        record["offset"],
                        str(len(bytes.fromhex(raw_hex))),
                    )
        for raw_hex in fields.get("AIDT", []):
            if len(bytes.fromhex(raw_hex)) != 20:
                raise Malformed("invalid-npc-aidt-length", record["offset"], str(len(bytes.fromhex(raw_hex))))
    elif signature == "REFR":
        for name in ("NAME", "XLKR", "XLRL", "XOWN", "DATA", "XESP"):
            values = subrecord_values(record, name)
            if values:
                fields[name] = [hx(value) for value in values]
        for name in ("NAME", "XLRL", "XOWN"):
            for raw_hex in fields.get(name, []):
                if len(bytes.fromhex(raw_hex)) != 4:
                    raise Malformed(f"invalid-refr-{name.lower()}-length", record["offset"], raw_hex)
        for raw_hex in fields.get("XLKR", []):
            if len(bytes.fromhex(raw_hex)) != 8:
                raise Malformed("invalid-refr-xlkr-length", record["offset"], raw_hex)
        for raw_hex in fields.get("DATA", []):
            if len(bytes.fromhex(raw_hex)) != 24:
                raise Malformed("invalid-refr-data-length", record["offset"], raw_hex)
            fields["float32_bit_patterns"] = [
                hx(bytes.fromhex(raw_hex)[i : i + 4]) for i in range(0, 24, 4)
            ]
    elif signature == "RACE":
        values = subrecord_values(record, "DATA")
        if values:
            fields["DATA"] = [hx(value) for value in values]
            if len(values[0]) < 4:
                raise Malformed("invalid-race-data-length", record["offset"], str(len(values[0])))
            fields["face_gen_head"] = (u32(values[0], 0) & 0x2) != 0
    else:
        return None
    return fields


def inspect_tes4_prefix(data: bytes) -> tuple[dict[str, Any] | None, dict[str, Any] | None]:
    if len(data) < 24 or sig(data, 0) != "TES4":
        return None, None
    size = u32(data, 4)
    flags = u32(data, 8)
    if size > len(data) - 24 or (flags & 0x00040000) != 0:
        return (
            {
                "flags_hex": f"{flags:08X}",
                "esl_flag": (flags & 0x200) != 0,
                "masters": [],
            },
            None,
        )
    scratch: list[dict[str, Any]] = []
    try:
        subs = parse_subrecords(data[24 : 24 + size], "physical-file", 24, scratch)
    except Malformed as exc:
        return (
            {
                "flags_hex": f"{flags:08X}",
                "esl_flag": (flags & 0x200) != 0,
                "masters": [],
            },
            {"code": exc.code, "offset": exc.offset, "detail": exc.detail},
        )
    pseudo = {"subrecords": subs}
    try:
        masters = parse_masters(pseudo)
        return (
            {
                "flags_hex": f"{flags:08X}",
                "esl_flag": (flags & 0x200) != 0,
                "masters": masters,
            },
            None,
        )
    except Malformed as exc:
        loose_masters = []
        for item in subs:
            if item["signature"] == "MAST":
                raw = bytes.fromhex(item["raw_hex"])
                if raw.endswith(b"\0"):
                    loose_masters.append(raw[:-1].decode("ascii", "replace"))
        return (
            {
                "flags_hex": f"{flags:08X}",
                "esl_flag": (flags & 0x200) != 0,
                "masters": loose_masters,
            },
            {"code": exc.code, "offset": exc.offset, "detail": exc.detail},
        )


def parse_file(path: Path, relative_path: str) -> dict[str, Any]:
    data = path.read_bytes()
    tes4_prefix, tes4_prefix_error = inspect_tes4_prefix(data)
    result: dict[str, Any] = {
        "path": relative_path.replace("\\", "/"),
        "byte_length": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "spans": [],
        "records": [],
        "malformed": None,
    }
    if tes4_prefix is not None:
        result["tes4"] = tes4_prefix
    if len(data) > MAX_INPUT:
        result["malformed"] = {
            "code": "input-over-limit",
            "offset": 0,
            "detail": str(len(data)),
        }
        return result
    spans: list[dict[str, Any]] = result["spans"]
    try:
        elements = parse_elements(data, 0, len(data), 0, spans, Bounds())
        records = list(flattened_records(elements))
        result["records"] = records
        result["groups"] = list(flattened_groups(elements))
        if not records or records[0]["signature"] != "TES4":
            raise Malformed("missing-tes4", 0, "first record is not TES4")
        tes4 = records[0]
        result["tes4"] = {
            "flags_hex": tes4["flags_hex"],
            "esl_flag": (int(tes4["flags_hex"], 16) & 0x200) != 0,
            "masters": parse_masters(tes4),
        }
        if tes4_prefix_error is not None:
            raise Malformed(
                tes4_prefix_error["code"],
                tes4_prefix_error["offset"],
                tes4_prefix_error["detail"],
            )
        for record in records:
            payload = analyze_payload(record)
            if payload is not None:
                record["allowlisted_payload"] = payload
    except (Malformed, UnicodeDecodeError) as exc:
        if isinstance(exc, Malformed):
            result["malformed"] = {
                "code": exc.code,
                "offset": exc.offset,
                "detail": exc.detail,
            }
        else:
            result["malformed"] = {
                "code": "invalid-master-name",
                "offset": 0,
                "detail": str(exc),
            }
        covered = [
            (span["offset"], span["offset"] + span["length"])
            for span in spans
            if span["offset_space"] == "physical-file"
        ]
        covered.sort()
        cursor = 0
        for start, finish in covered:
            if start > cursor:
                add_span(spans, "physical-file", cursor, start - cursor, "opaque")
            cursor = max(cursor, finish)
        if cursor < len(data):
            add_span(spans, "physical-file", cursor, len(data) - cursor, "opaque")
    return result


def resolve_form_id(
    raw_hex: str,
    current_name: str,
    masters: list[str],
    light_flags: dict[str, bool],
) -> dict[str, Any]:
    raw = int(raw_hex, 16)
    if raw == 0:
        return {
            "form_id_hex": raw_hex,
            "resolution_state": "null",
            "form_key": None,
            "origin_plugin": None,
            "origin_kind": None,
            "local_id_hex": None,
        }
    index = raw >> 24
    origins = masters + [current_name]
    if index >= len(origins):
        return {
            "form_id_hex": raw_hex,
            "resolution_state": "invalid",
            "reason": f"master index {index} unavailable in {len(masters)} masters",
            "form_key": None,
            "origin_plugin": None,
            "origin_kind": "invalid",
            "local_id_hex": None,
        }
    origin = origins[index]
    if origin not in light_flags:
        return {
            "form_id_hex": raw_hex,
            "resolution_state": "unknown",
            "reason": f"origin flags unavailable for {origin}",
            "form_key": None,
            "origin_plugin": origin,
            "origin_kind": "unknown",
            "local_id_hex": None,
        }
    raw_local = raw & 0x00FFFFFF
    is_light = light_flags[origin]
    if is_light:
        if raw_local < 0x800 or raw_local > 0xFFF:
            return {
                "form_id_hex": raw_hex,
                "resolution_state": "invalid",
                "reason": f"light local ID {raw_local:06X} outside 000800..000FFF",
                "form_key": None,
                "origin_plugin": origin,
                "origin_kind": "invalid",
                "local_id_hex": f"{raw_local:06X}",
            }
        local = raw_local & 0xFFF
        kind = "light"
    else:
        local = raw_local
        kind = "full"
    return {
        "form_id_hex": raw_hex,
        "resolution_state": "resolved",
        "form_key": f"{local:08X}:{origin}",
        "origin_plugin": origin,
        "origin_kind": kind,
        "local_id_hex": f"{local:08X}",
    }


def annotate_identities(files: list[dict[str, Any]]) -> None:
    light_flags: dict[str, bool] = {}
    for file in files:
        tes4 = file.get("tes4")
        if tes4 is not None and file["path"].startswith("plugins/"):
            light_flags[Path(file["path"]).name] = tes4["esl_flag"]
    for file in files:
        tes4 = file.get("tes4")
        if tes4 is None:
            continue
        current_name = Path(file["path"]).name
        extension_header_mismatch = (
            Path(current_name).suffix.lower() == ".esl" and not tes4["esl_flag"]
        )
        file["extension_header_mismatch"] = extension_header_mismatch
        light_flags.setdefault(current_name, tes4["esl_flag"])
        masters = tes4["masters"]
        for record in file["records"]:
            if record["signature"] == "TES4":
                continue
            record["identity"] = resolve_form_id(
                record["raw_form_id_hex"], current_name, masters, light_flags
            )
            if (
                extension_header_mismatch
                and record["identity"].get("origin_plugin") == current_name
            ):
                record["identity"].update(
                    {
                        "resolution_state": "invalid",
                        "reason": "native .esl extension/header light-flag mismatch",
                        "form_key": None,
                        "origin_kind": "invalid",
                        "local_id_hex": None,
                    }
                )
            payload = record.get("allowlisted_payload")
            if payload is None:
                continue
            links: list[dict[str, Any]] = []
            for field in ("TPLT", "RNAM", "PKID", "PNAM", "HCLF", "NAME", "XLRL", "XOWN"):
                for occurrence, raw_le in enumerate(payload.get(field, [])):
                    resolved = resolve_form_id(
                        f"{int.from_bytes(bytes.fromhex(raw_le), 'little'):08X}",
                        current_name,
                        masters,
                        light_flags,
                    )
                    if extension_header_mismatch and resolved.get("origin_plugin") == current_name:
                        resolved.update(
                            {
                                "resolution_state": "invalid",
                                "reason": "native .esl extension/header light-flag mismatch",
                                "form_key": None,
                                "origin_kind": "invalid",
                                "local_id_hex": None,
                            }
                        )
                    links.append(
                        {
                            "field": field,
                            "occurrence": occurrence,
                            **resolved,
                        }
                    )
            for occurrence, raw_le in enumerate(payload.get("XLKR", [])):
                raw = bytes.fromhex(raw_le)
                for component, part in (
                    ("keyword", raw[0:4]),
                    ("linked-reference", raw[4:8]),
                ):
                    links.append(
                        {
                            "field": "XLKR",
                            "occurrence": occurrence,
                            "component": component,
                            **resolve_form_id(
                                f"{int.from_bytes(part, 'little'):08X}",
                                current_name,
                                masters,
                                light_flags,
                            ),
                        }
                    )
            record["links"] = links


def scenario_semantics(
    files: list[dict[str, Any]],
    matrix: dict[str, Any],
    supplemental_inputs: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    files_by_path = {item["path"]: item for item in files}
    requests = {
        item["path"]: item.get("json_value") for item in supplemental_inputs
    }
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
            population: dict[str, list[str]] = {}
            records: list[dict[str, Any]] = []
            for plugin_order, path in enumerate(paths):
                file = files_by_path[path]
                for record_index, record in enumerate(file.get("records", [])):
                    identity = record.get("identity")
                    if not identity or not identity.get("form_key"):
                        continue
                    locator = f"{path}#{record_index}"
                    population.setdefault(identity["form_key"], []).append(locator)
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
                                    "resolution_state": link["resolution_state"],
                                }
                                for link in record.get("links", [])
                            ],
                        }
                    )
            population_keys = set(population)
            for record in records:
                for link in record["links"]:
                    if (
                        link["resolution_state"] == "resolved"
                        and link["form_key"] not in population_keys
                    ):
                        link["resolution_state"] = "unresolved"
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    inputs = args.package / "inputs"
    files = sorted(
        (
            path
            for path in inputs.rglob("*")
            if path.is_file() and path.suffix.lower() in PLUGIN_SUFFIXES
        ),
        key=lambda path: path.relative_to(inputs).as_posix(),
    )
    parsed_files = [
        parse_file(path, path.relative_to(inputs).as_posix()) for path in files
    ]
    annotate_identities(parsed_files)
    supplemental_inputs = []
    for path in sorted(
        (
            path
            for path in inputs.rglob("*")
            if path.is_file()
            and path.suffix.lower() not in PLUGIN_SUFFIXES
            and (
                "requests" in path.relative_to(inputs).parts
                or path.suffix.lower() == ".strings"
            )
        ),
        key=lambda path: path.relative_to(inputs).as_posix(),
    ):
        raw = path.read_bytes()
        item: dict[str, Any] = {
            "path": path.relative_to(inputs).as_posix(),
            "byte_length": len(raw),
            "sha256": hashlib.sha256(raw).hexdigest(),
            "raw_hex": hx(raw),
        }
        if path.suffix.lower() == ".json":
            item["json_value"] = json.loads(raw)
        supplemental_inputs.append(item)
    report = {
        "method_id": "independent-bounded-raw-reader-v1",
        "package_id": args.package.name,
        "limits": {
            "maximum_input_bytes": MAX_INPUT,
            "maximum_records": MAX_RECORDS,
            "maximum_subrecords_per_record": MAX_SUBRECORDS,
            "maximum_group_depth": MAX_GROUP_DEPTH,
            "maximum_decompressed_bytes": MAX_DECOMPRESSED,
        },
        "files": parsed_files,
        "supplemental_inputs": supplemental_inputs,
        "scenario_semantics": scenario_semantics(
            parsed_files,
            json.loads((inputs / "case-matrix.json").read_text(encoding="utf-8")),
            supplemental_inputs,
        ),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(report, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
