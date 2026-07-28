#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import zlib from "node:zlib";

const RECORD_HEADER_SIZE = 24;
const COMPRESSED_FLAG = 0x00040000;

function ascii(buffer, offset, length) {
  return buffer.toString("ascii", offset, offset + length);
}

function hex(buffer) {
  return buffer.toString("hex").toUpperCase();
}

function parseSubrecords(data, absoluteDataOffset) {
  const subrecords = [];
  let offset = 0;
  let extendedSize = null;

  while (offset + 6 <= data.length) {
    const signature = ascii(data, offset, 4);
    const declaredSize = data.readUInt16LE(offset + 4);
    const headerOffset = offset;
    offset += 6;

    if (signature === "XXXX") {
      if (declaredSize !== 4 || offset + 4 > data.length) {
        throw new Error(`Malformed XXXX subrecord at ${absoluteDataOffset + headerOffset}`);
      }
      extendedSize = data.readUInt32LE(offset);
      offset += 4;
      continue;
    }

    const size = extendedSize ?? declaredSize;
    extendedSize = null;
    if (offset + size > data.length) {
      throw new Error(
        `Subrecord ${signature} overruns record data at ${absoluteDataOffset + headerOffset}`,
      );
    }

    const value = data.subarray(offset, offset + size);
    subrecords.push({
      signature,
      headerOffset: absoluteDataOffset + headerOffset,
      dataOffset: absoluteDataOffset + offset,
      size,
      hex: hex(value),
    });
    offset += size;
  }

  if (offset !== data.length) {
    throw new Error(`Trailing ${data.length - offset} bytes in record data`);
  }
  return subrecords;
}

function recordData(buffer, offset, size, flags) {
  const raw = buffer.subarray(offset, offset + size);
  if ((flags & COMPRESSED_FLAG) === 0) {
    return { data: raw, compressed: false, absoluteDataOffset: offset };
  }
  if (raw.length < 4) {
    throw new Error(`Compressed record at ${offset - RECORD_HEADER_SIZE} has no size prefix`);
  }
  const expectedSize = raw.readUInt32LE(0);
  const data = zlib.inflateSync(raw.subarray(4));
  if (data.length !== expectedSize) {
    throw new Error(
      `Compressed record at ${offset - RECORD_HEADER_SIZE} expected ${expectedSize} bytes, got ${data.length}`,
    );
  }
  return { data, compressed: true, absoluteDataOffset: offset + 4 };
}

function parseRecord(buffer, offset, contexts) {
  if (offset + RECORD_HEADER_SIZE > buffer.length) {
    throw new Error(`Truncated record header at ${offset}`);
  }

  const signature = ascii(buffer, offset, 4);
  const dataSize = buffer.readUInt32LE(offset + 4);
  const flags = buffer.readUInt32LE(offset + 8);
  const formId = buffer.readUInt32LE(offset + 12);
  const dataOffset = offset + RECORD_HEADER_SIZE;
  if (dataOffset + dataSize > buffer.length) {
    throw new Error(`Record ${signature} at ${offset} overruns file`);
  }

  const decoded = recordData(buffer, dataOffset, dataSize, flags);
  return {
    nextOffset: dataOffset + dataSize,
    record: {
      signature,
      offset,
      dataOffset,
      dataSize,
      flags: `0x${flags.toString(16).padStart(8, "0").toUpperCase()}`,
      formId: `0x${formId.toString(16).padStart(8, "0").toUpperCase()}`,
      compressed: decoded.compressed,
      contexts,
      subrecords: parseSubrecords(decoded.data, decoded.absoluteDataOffset),
    },
  };
}

function parseRange(buffer, start, end, contexts, records) {
  let offset = start;
  while (offset < end) {
    if (offset + 4 > end) {
      throw new Error(`Truncated signature at ${offset}`);
    }
    const signature = ascii(buffer, offset, 4);
    if (signature !== "GRUP") {
      const parsed = parseRecord(buffer, offset, contexts);
      records.push(parsed.record);
      offset = parsed.nextOffset;
      continue;
    }

    if (offset + RECORD_HEADER_SIZE > end) {
      throw new Error(`Truncated GRUP header at ${offset}`);
    }
    const groupSize = buffer.readUInt32LE(offset + 4);
    if (groupSize < RECORD_HEADER_SIZE || offset + groupSize > end) {
      throw new Error(`Invalid GRUP size ${groupSize} at ${offset}`);
    }
    const labelHex = hex(buffer.subarray(offset + 8, offset + 12));
    const labelAscii = ascii(buffer, offset + 8, 4);
    const groupType = buffer.readInt32LE(offset + 12);
    parseRange(
      buffer,
      offset + RECORD_HEADER_SIZE,
      offset + groupSize,
      [...contexts, { offset, groupType, labelHex, labelAscii }],
      records,
    );
    offset += groupSize;
  }
  if (offset !== end) {
    throw new Error(`Range ended at ${offset}, expected ${end}`);
  }
}

export function inspectPlugin(pluginPath) {
  const buffer = fs.readFileSync(pluginPath);
  const records = [];
  parseRange(buffer, 0, buffer.length, [], records);
  if (records.length === 0 || records[0].signature !== "TES4") {
    throw new Error(`${pluginPath} has no leading TES4 record`);
  }

  const masters = records[0].subrecords
    .filter((subrecord) => subrecord.signature === "MAST")
    .map((subrecord) =>
      Buffer.from(subrecord.hex, "hex").toString("utf8").replace(/\0+$/u, ""),
    );

  return {
    file: path.basename(pluginPath),
    bytes: buffer.length,
    masters,
    records,
  };
}

function selectedRecord(record, selectedTypes, selectedFormIds) {
  return (
    (selectedTypes.size === 0 || selectedTypes.has(record.signature)) &&
    (selectedFormIds.size === 0 || selectedFormIds.has(record.formId))
  );
}

function main() {
  const args = process.argv.slice(2);
  const paths = [];
  const selectedTypes = new Set();
  const selectedFormIds = new Set();
  const selectedSubrecords = new Set();
  let outputPath = null;

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument === "--type") {
      selectedTypes.add(args[++index]);
    } else if (argument === "--form") {
      const value = Number.parseInt(args[++index], 16);
      selectedFormIds.add(`0x${value.toString(16).padStart(8, "0").toUpperCase()}`);
    } else if (argument === "--subrecord") {
      selectedSubrecords.add(args[++index]);
    } else if (argument === "--output") {
      outputPath = args[++index];
    } else {
      paths.push(argument);
    }
  }

  if (paths.length === 0) {
    throw new Error(
      "Usage: node tes4-inspect.mjs [--type NPC_] [--form 0x0001339A] [--output result.json] plugin...",
    );
  }

  const result = {
    schema: "infinium-research-tes4-inspection/1",
    generatedAt: new Date().toISOString(),
    plugins: paths.map((pluginPath) => {
      const plugin = inspectPlugin(pluginPath);
      return {
        ...plugin,
        records: plugin.records
          .filter((record) => selectedRecord(record, selectedTypes, selectedFormIds))
          .map((record) => ({
            ...record,
            subrecords:
              selectedSubrecords.size === 0
                ? record.subrecords
                : record.subrecords.filter((subrecord) =>
                    selectedSubrecords.has(subrecord.signature),
                  ),
          })),
      };
    }),
  };
  const json = `${JSON.stringify(result, null, 2)}\n`;
  if (outputPath) {
    fs.writeFileSync(outputPath, json);
  } else {
    process.stdout.write(json);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
