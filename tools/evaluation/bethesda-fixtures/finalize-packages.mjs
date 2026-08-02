import { createHash } from "node:crypto";
import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

const fixtureVersion = "1.1.0";
const createdAt = "2026-08-02T12:00:00.0000000+00:00";
const correctedStructureAt = "2026-08-01T22:00:00.0000000+00:00";
const partitionHistoryInitialAt = "2026-07-30T18:00:00.0000000+00:00";
const privateReplacementAt = "2026-08-01T18:00:00.0000000+00:00";
const generatorSha256 =
  "6ffc864936944642742024c96590c8d9fec5e2cd151d982a6cf4172ddaecc4a5";
const generatorProjectSha256 =
  "f360a93248ae4a6a92176c50f85eba13e630c3f64af23ad970b395cb0028b04e";
const root = path.resolve(
  process.cwd(),
  "test-data",
  "evaluation",
  "m1-semantic",
);
const privateRegistry = JSON.parse(
  await readFile(path.join(root, "evaluator-private-registry.json"), "utf8"),
);
if (
  privateRegistry.schema_id !==
    "infinium.evaluation.evaluator-private-fixture-registry/v2" ||
  privateRegistry.schema_version !== "2"
) {
  throw new Error("The accepted evaluator-private registry v2 is required.");
}
const privateReplacements = new Map(
  privateRegistry.fixtures.map((fixture) => [fixture.fixture_id, fixture]),
);
if (
  privateReplacements.size !== privateRegistry.fixtures.length ||
  privateReplacements.size !== 3
) {
  throw new Error("The evaluator-private registry must contain three unique replacements.");
}

const fixtures = [
  {
    fixtureId: "BETH-NPC-DEV",
    partition: "development",
    classification: "boundary",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
    purpose:
      "Qualify project-authored NPC, race, override, winner, FormKey, field, link, and bounded taxonomy evidence for later Slice 4 evaluation.",
  },
  {
    fixtureId: "BETH-REFR-DEV",
    partition: "development",
    classification: "boundary",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
    purpose:
      "Qualify project-authored placed-reference, relation, placement, override, winner, FormKey, boundary, and bounded taxonomy evidence for later Slice 4 evaluation.",
  },
  {
    fixtureId: "BETH-LIGHT-VAL",
    partition: "development",
    replacementFixtureId: "BETH-LIGHT-VAL-002",
    replacementAt: privateReplacementAt,
    replacementReason:
      "Owner-approved correction: public answer exposure makes this package development evidence under ADR-0026.",
    classification: "boundary",
    evaluationIds: ["EVAL-0052"],
    purpose:
      "Qualify project-authored full, light, ESL-flagged, maximum, and invalid local-identity boundaries for later Slice 4 evaluation.",
  },
  {
    fixtureId: "BETH-MALFORMED-VAL",
    partition: "development",
    replacementFixtureId: "BETH-MALFORMED-VAL-002",
    replacementAt: "2026-07-30T18:00:00.0000001+00:00",
    initialRegistrationReason:
      "Initial registration before review discovered that validation cases had already influenced fixture-generator corrections.",
    replacementReason:
      "Review correction: malformed cases guided generator fixes, so they cannot remain validation evidence.",
    classification: "malformed",
    evaluationIds: ["EVAL-0052"],
    purpose:
      "Qualify project-authored malformed Bethesda byte boundaries and bounded failure expectations for later Slice 4 evaluation.",
  },
  {
    fixtureId: "BETH-UNSUPPORTED-VAL",
    partition: "development",
    replacementFixtureId: "BETH-UNSUPPORTED-VAL-002",
    replacementAt: privateReplacementAt,
    replacementReason:
      "Owner-approved correction: public answer exposure makes this package development evidence under ADR-0026.",
    classification: "unsupported",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
    purpose:
      "Qualify explicit unsupported and gap states for unallowlisted, localized, archive-member, discovery, and bounded taxonomy inputs.",
  },
];

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

async function writeJson(filePath, value) {
  await writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function enumerateFiles(directory, prefix = "") {
  const result = [];
  const entries = await readdir(directory, { withFileTypes: true });
  entries.sort((left, right) =>
    left.name < right.name ? -1 : left.name > right.name ? 1 : 0,
  );
  for (const entry of entries) {
    const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      result.push(...(await enumerateFiles(fullPath, relative)));
    } else if (entry.isFile()) {
      result.push(relative);
    } else {
      throw new Error(`Unsupported retained input entry: ${fullPath}`);
    }
  }

  return result;
}

async function artifactReference(fixtureRoot, artifactId) {
  const bytes = await readFile(
    path.join(fixtureRoot, ...artifactId.split("/")),
  );
  return {
    artifact_id: artifactId,
    artifact_version: fixtureVersion,
    fingerprint: sha256(bytes),
    availability: "retained",
  };
}

function collectArtifactIds(value, prefix, result = new Set()) {
  if (Array.isArray(value)) {
    for (const item of value) collectArtifactIds(item, prefix, result);
  } else if (value && typeof value === "object") {
    if (
      typeof value.artifact_id === "string" &&
      value.artifact_id.startsWith(prefix)
    ) {
      result.add(value.artifact_id);
    }
    for (const nested of Object.values(value)) {
      collectArtifactIds(nested, prefix, result);
    }
  }
  return result;
}

async function validateTaxonomyAndOracleClosure(fixtureRoot, fixture) {
  const oracle = JSON.parse(
    await readFile(path.join(fixtureRoot, "expected-oracle.json"), "utf8"),
  );
  const physicalOracleFiles = (await enumerateFiles(path.join(fixtureRoot, "oracle")))
    .map((relative) => `oracle/${relative}`);
  const referencedOracleFiles = [...collectArtifactIds(oracle, "oracle/")];
  if (
    physicalOracleFiles.length !== referencedOracleFiles.length ||
    physicalOracleFiles.some((item) => !referencedOracleFiles.includes(item))
  ) {
    throw new Error(`${fixture.fixtureId} does not have exact oracle reference closure.`);
  }

  const expectsTaxonomy = fixture.evaluationIds.includes("EVAL-0086");
  const taxonomyPath = path.join(fixtureRoot, "oracle", "taxonomy-projections.json");
  const bindingsPath = path.join(fixtureRoot, "inputs", "taxonomy-subject-bindings.json");
  const hasTaxonomy = physicalOracleFiles.includes("oracle/taxonomy-projections.json");
  let hasBindings = true;
  try {
    await stat(bindingsPath);
  } catch {
    hasBindings = false;
  }
  if (hasTaxonomy !== expectsTaxonomy || hasBindings !== expectsTaxonomy) {
    throw new Error(`${fixture.fixtureId} taxonomy projections and bindings are incomplete.`);
  }
  if (!expectsTaxonomy) return;

  const taxonomy = JSON.parse(await readFile(taxonomyPath, "utf8"));
  const bindings = JSON.parse(await readFile(bindingsPath, "utf8"));
  for (const document of [taxonomy, bindings]) {
    if (
      document.fixture_id !== fixture.fixtureId ||
      document.fixture_version !== fixtureVersion ||
      document.taxonomy_id !== "infinium.skyrim-se.mod-impact-taxonomy" ||
      document.taxonomy_version !== "0.1.0"
    ) {
      throw new Error(`${fixture.fixtureId} taxonomy identity drifted.`);
    }
  }
  const expectedTaxonomySources = new Set([
    "oracle/independent-byte-facts.json",
    "inputs/snapshot/accepted-order.json",
  ]);
  const taxonomySourceIds = taxonomy.source_artifacts.map(
    (reference) => reference.artifact_id,
  );
  if (
    taxonomySourceIds.length !== expectedTaxonomySources.size ||
    new Set(taxonomySourceIds).size !== taxonomySourceIds.length ||
    taxonomySourceIds.some((artifactId) => !expectedTaxonomySources.has(artifactId))
  ) {
    throw new Error(`${fixture.fixtureId} taxonomy source closure is not exact.`);
  }
  for (const reference of taxonomy.source_artifacts) {
    const sourcePath = path.join(fixtureRoot, ...reference.artifact_id.split("/"));
    const sourceBytes = await readFile(sourcePath);
    const sourceStats = await stat(sourcePath);
    if (
      reference.artifact_version !== fixtureVersion ||
      reference.fingerprint !== sha256(sourceBytes) ||
      reference.availability !== "retained" ||
      (Object.hasOwn(reference, "byte_length") &&
        reference.byte_length !== sourceStats.size)
    ) {
      throw new Error(`${fixture.fixtureId} taxonomy source metadata drifted.`);
    }
  }
  const sealedIds = taxonomy.subjects.map((subject) => subject.subject_id);
  const boundIds = bindings.bindings.map((binding) => binding.sealed_subject_id);
  const targets = bindings.bindings.map(
    (binding) => binding.production_subject_participant_id,
  );
  if (
    new Set(sealedIds).size !== sealedIds.length ||
    new Set(boundIds).size !== boundIds.length ||
    new Set(targets).size !== targets.length ||
    sealedIds.length !== boundIds.length ||
    sealedIds.some((id) => !boundIds.includes(id))
  ) {
    throw new Error(`${fixture.fixtureId} taxonomy subject bindings are not bijective.`);
  }
}

for (const fixture of fixtures) {
  const fixtureRoot = path.join(root, fixture.fixtureId);
  await validateTaxonomyAndOracleClosure(fixtureRoot, fixture);
  const inputPaths = await enumerateFiles(path.join(fixtureRoot, "inputs"));
  const inputReferences = [];
  let inputBytes = 0;
  for (const inputPath of inputPaths) {
    const artifactId = `inputs/${inputPath}`;
    inputReferences.push(await artifactReference(fixtureRoot, artifactId));
    inputBytes += (await stat(path.join(fixtureRoot, ...artifactId.split("/"))))
      .size;
  }

  const byId = new Map(
    inputReferences.map((reference) => [reference.artifact_id, reference]),
  );
  const snapshotReference = byId.get("inputs/snapshot/accepted-order.json");
  const caseMatrixReference = byId.get("inputs/case-matrix.json");
  if (!snapshotReference || !caseMatrixReference) {
    throw new Error(`${fixture.fixtureId} lacks required retained controls.`);
  }

  const inputComponent = (reason) => ({
    state: "provided",
    reason,
    artifact: snapshotReference,
  });
  const executionInput = {
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    installation_snapshot_input: inputComponent(
      "Retained Slice 3 accepted-order receipt binds the disposable synthetic snapshot input.",
    ),
    analysis_context_input: {
      state: "empty",
      reason:
        "Slice 3.5 qualifies inputs and independent truth; production semantic analysis remains pending Slice 4.",
    },
    effective_scan_configuration: caseMatrixReference,
    runtime_support_input: {
      state: "not-applicable",
      reason:
        "Runtime execution is outside the Bethesda byte-input qualification boundary.",
    },
    mo2_instance_profile_input: inputComponent(
      "Retained synthetic profile receipt records the selected profile and accepted adapter identity.",
    ),
    plugin_order_input: inputComponent(
      "Retained synthetic profile receipt records exact plugin order and fingerprints.",
    ),
    provider_order_input: inputComponent(
      "Retained synthetic profile receipt records exact provider order and fingerprints.",
    ),
    source_claim_inputs: [],
    analyzer_declarations: [],
    tool_library_versions: [
      {
        component_id: "Infinium.BethesdaFixtures.Generator",
        version: "1",
        fingerprint: generatorSha256,
      },
    ],
    declared_archive_state: {
      state:
        fixture.fixtureId === "BETH-UNSUPPORTED-VAL"
          ? "unsupported"
          : "excluded",
      reason:
        fixture.fixtureId === "BETH-UNSUPPORTED-VAL"
          ? "Archive-member semantics are represented only as an explicit unsupported request."
          : "Archive-member semantics are outside this retained plugin-byte package.",
    },
    declared_supported_capabilities: [
      "bethesda-project-authored-fixture-input-v1",
      "slice3-accepted-order-receipt-v1",
    ],
    declared_unsupported_capabilities: [
      {
        capability_id: "production-bethesda-semantic-analysis",
        reason:
          "Slice 3.5 prepares inputs and independent truth only; production analysis remains pending Slice 4.",
      },
      {
        capability_id: "complete-eval-0052-or-eval-0086-pass",
        reason:
          "Package acceptance does not execute or pass either evaluation case.",
      },
    ],
    resource_and_time_limits: {
      wall_time_ms: 120000,
      memory_bytes: 536870912,
      input_bytes: inputBytes,
      output_bytes: 16777216,
    },
    input_payload_refs: inputReferences,
  };
  await writeJson(path.join(fixtureRoot, "execution-input.json"), executionInput);

  const provenance = {
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    created_by: "infinium-project-authored-bethesda-fixtures",
  };
  await writeJson(path.join(fixtureRoot, "provenance.json"), provenance);

  const replayDependencies = {
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    expected_replay_state: "complete-clean",
    dependencies: [
      {
        dependency_id: "bethesda-fixture-generator-v1",
        kind: "tracked-source",
        identity_or_version: generatorSha256,
        sha256: generatorSha256,
        byte_length: 69137,
        retention_location_class: "tracked-repository",
        availability: "retained",
        required_for: ["clean-recomputation", "audit"],
        permission_and_redistribution: "project-authored-redistributable",
        deletion_effect:
          "Retained bytes remain auditable, but deterministic clean reconstruction is lost.",
      },
      {
        dependency_id: "bethesda-fixture-generator-project-v1",
        kind: "tracked-project",
        identity_or_version: generatorProjectSha256,
        sha256: generatorProjectSha256,
        byte_length: 308,
        retention_location_class: "tracked-repository",
        availability: "retained",
        required_for: ["clean-recomputation", "audit"],
        permission_and_redistribution: "project-authored-redistributable",
        deletion_effect:
          "Retained bytes remain auditable, but the exact generator project contract required for deterministic reconstruction is lost.",
      },
      {
        dependency_id: "dotnet-sdk-10.0.302",
        kind: "toolchain",
        identity_or_version: "10.0.302",
        byte_length: null,
        retention_location_class: "external-authoritative-source",
        availability: "externally-reacquirable",
        required_for: ["clean-recomputation"],
        permission_and_redistribution: "external-reacquisition-only",
        deletion_effect:
          "Retained bytes remain executable and auditable, but clean generator reconstruction requires SDK reacquisition.",
      },
    ],
  };
  await writeJson(
    path.join(fixtureRoot, "replay-dependencies.json"),
    replayDependencies,
  );

  const redistribution = {
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    redistribution_class: "project-authored-redistributable",
  };
  await writeJson(
    path.join(fixtureRoot, "redistribution.json"),
    redistribution,
  );

  let partitionHistory;
  if (fixture.replacementFixtureId) {
    const replacement = privateReplacements.get(fixture.replacementFixtureId);
    if (!replacement) {
      throw new Error(
        `Missing private replacement metadata: ${fixture.replacementFixtureId}`,
      );
    }
    if (
      replacement.partition !== "validation" ||
      replacement.review_state !== "sealed" ||
      replacement.contamination_state !== "clean"
    ) {
      throw new Error(
        `Private replacement is not sealed validation evidence: ${fixture.replacementFixtureId}`,
      );
    }
    partitionHistory = [
      {
        from: null,
        to: "validation",
        at: partitionHistoryInitialAt,
        reason:
          fixture.initialRegistrationReason ??
          "Initial registration before the answer-bearing package entered ordinary implementation context.",
        change_influenced_implementation: false,
      },
      {
        from: "validation",
        to: "development",
        at: fixture.replacementAt,
        reason: fixture.replacementReason,
        change_influenced_implementation: true,
        replacement_fixture_id: replacement.fixture_id,
        replacement_partition: replacement.partition,
        replacement_input_package_fingerprint:
          replacement.declared_manifest_input_package_fingerprint,
        replacement_oracle_fingerprint: replacement.oracle_fingerprint,
        independence_evidence_reference: replacement.independence_evidence,
        authorized_by: replacement.corrective_authority_id,
      },
    ];
  } else {
    partitionHistory = [
      {
        from: null,
        to: fixture.partition,
        at: correctedStructureAt,
        reason:
          "Corrected successor to fixture version 1.0.0 after the Slice 4 Mutagen conformance probe exposed a fixture-structure error.",
        change_influenced_implementation: true,
      },
    ];
  }
  await writeJson(path.join(fixtureRoot, "partition-history.json"), {
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    partition_history: partitionHistory,
  });

  const expectedOracleBytes = await readFile(
    path.join(fixtureRoot, "expected-oracle.json"),
  );
  const executionInputBytes = await readFile(
    path.join(fixtureRoot, "execution-input.json"),
  );
  const provenanceBytes = await readFile(
    path.join(fixtureRoot, "provenance.json"),
  );
  const replayDependencyBytes = await readFile(
    path.join(fixtureRoot, "replay-dependencies.json"),
  );
  const publicManifest = {
    schema_id: "infinium.evaluation.fixture-public-manifest/v1",
    schema_version: "1",
    fixture_id: fixture.fixtureId,
    fixture_version: fixtureVersion,
    evaluation_ids: fixture.evaluationIds,
    purpose: fixture.purpose,
    classification: fixture.classification,
    partition: fixture.partition,
    partition_history: partitionHistory,
    taxonomy_id: "infinium.skyrim-se.mod-impact-taxonomy",
    taxonomy_version: "0.1.0",
    input_package_fingerprint: sha256(executionInputBytes),
    oracle_fingerprint: sha256(expectedOracleBytes),
    provenance_fingerprint: sha256(provenanceBytes),
    replay_dependency_fingerprint: sha256(replayDependencyBytes),
    redistribution_class: "project-authored-redistributable",
    owner: "infinium-evaluation",
    review_state: "accepted",
    created_at: createdAt,
  };
  await writeJson(path.join(fixtureRoot, "public-manifest.json"), publicManifest);

  console.log(
    fixture.fixtureId,
    publicManifest.input_package_fingerprint,
    publicManifest.oracle_fingerprint,
  );
}
