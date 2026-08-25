import { createHash } from "node:crypto";
import { mkdir, readFile, readdir, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

// Current-authority resealer only. It deliberately has no predecessor-version
// migration path and refuses a package before writing if its current oracle
// identity or independently pinned semantic truth does not match.

const repositoryRoot = process.cwd();
const providerBudgetOnly = process.argv.includes("--provider-budget-only");
const providerOfflineOnly = process.argv.includes("--provider-offline-only");
const cleanupRenamesOnly = process.argv.includes("--cleanup-renames-only");
const currentRegistryOnly = process.argv.includes("--current-registry-only");
const candidateFixturesOnly = process.argv.includes("--candidate-fixtures-only");
const providerBudgetFixtures = [
  ["capability-dev", "PROVIDER-CAPABILITY-DEV-v1", "development"],
  ["capability-val", "PROVIDER-CAPABILITY-VAL-v1", "validation"],
  ["authority-dev", "PROVIDER-AUTHORIZATION-DEV-v1", "development"],
  ["authority-val", "PROVIDER-AUTHORIZATION-VAL-v1", "validation"],
  ["budget-dev", "PROVIDER-BUDGET-DEV-v1", "development"],
  ["budget-val", "PROVIDER-BUDGET-VAL-v1", "validation"],
];
const fixtures = [
  {
    relativeRoot: "fixtures/public/platform/analysis-runtime-substrate",
    fixtureId: "PLATFORM-ANALYSIS-RUNTIME-DEV-v1",
    version: "1.1.0",
    semanticTruthSha256: "d34b43340bca6d0c481f2418e2ae5e32870471b5521881a81c5ac06274563ed6",
    platform: true,
    evaluationIds: ["EVAL-0032"],
  },
  {
    relativeRoot: "fixtures/public/bethesda/BETH-NPC-DEV",
    fixtureId: "BETH-NPC-DEV",
    version: "1.4.0",
    semanticTruthSha256: "cc1d1d487e00916e1d5ce983a789018d6fa78dbfbadff6f845ae642a2f168f7a",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
  },
  {
    relativeRoot: "fixtures/public/bethesda/BETH-REFR-DEV",
    fixtureId: "BETH-REFR-DEV",
    version: "1.4.0",
    semanticTruthSha256: "2cd9e8850be6164e835739341e69b6d8f3e9bb58f47048bf36198037be154cca",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
  },
  {
    relativeRoot: "fixtures/public/bethesda/BETH-LIGHT-VAL",
    fixtureId: "BETH-LIGHT-VAL",
    version: "1.4.0",
    semanticTruthSha256: "7bf973b1c07a1f4d532b5a7a8c567f36f6c2e8aa3d604d88b645150327599477",
    evaluationIds: ["EVAL-0052"],
  },
  {
    relativeRoot: "fixtures/public/bethesda/BETH-MALFORMED-VAL",
    fixtureId: "BETH-MALFORMED-VAL",
    version: "1.4.0",
    semanticTruthSha256: "c56b28662a78c345dbbc8023b21e34da5758c2281d671b92343313aa408f5678",
    evaluationIds: ["EVAL-0052"],
  },
  {
    relativeRoot: "fixtures/public/bethesda/BETH-UNSUPPORTED-VAL",
    fixtureId: "BETH-UNSUPPORTED-VAL",
    version: "1.4.0",
    semanticTruthSha256: "2d3153a1b8b74e03d41b3f9e47cfa4d69723298cccf5c0304a0aead5f3512386",
    evaluationIds: ["EVAL-0052", "EVAL-0086"],
  },
];

const candidateFixtures = [
  {
    relativeRoot: "fixtures/public/candidates/CAND-SEMANTIC-DEV-v1",
    fixtureId: "CAND-SEMANTIC-DEV-v1",
    version: "1.0.1",
    productArtifact: "inputs/candidate-delivered-input.json",
    oracleArtifact: "oracle/semantic-population-projection.json",
  },
  {
    relativeRoot: "fixtures/public/candidates/CAND-SCALE-VAL-v1",
    fixtureId: "CAND-SCALE-VAL-v1",
    version: "1.0.1",
    productArtifact: "inputs/candidate-delivered-expansion.json",
    oracleArtifact: "oracle/semantic-population-projection.json",
  },
  {
    relativeRoot: "fixtures/public/candidates/CAND-STRESS-DEV-v1",
    fixtureId: "CAND-STRESS-DEV-v1",
    version: "1.0.1",
    productArtifact: "inputs/candidate-delivered-expansion.json",
    oracleArtifact: "oracle/streaming-expansion-receipt.json",
  },
];

const candidateAuthorityPath = "docs/product/candidate-input-and-expansion.md";

const semanticTruthProperties = [
  "expected_observations", "expected_deterministic_results", "expected_external_claims",
  "expected_application_links", "expected_discovery_leads", "expected_model_proposals",
  "expected_proposal_admissions", "expected_candidates", "expected_hypotheses",
  "expected_findings", "expected_recommendations", "expected_supported_cases",
  "expected_lead_only_cases", "expected_abstentions", "expected_invalid_inputs",
  "expected_failures", "expected_coverage_and_gaps", "expected_collection_states",
  "expected_taxonomy_assignments", "forbidden_claims", "known_limits",
];

if (providerOfflineOnly) {
  await resealProviderOfflineFixtures();
  process.exit(0);
}

if (providerBudgetOnly) {
  await resealProviderBudgetFixtures();
  process.exit(0);
}

if (cleanupRenamesOnly) {
  await resealAnalysisPipelineFixture();
  await resealProviderContractExamples();
  await refreshCurrentRegistryAuthorities();
  process.exit(0);
}

if (currentRegistryOnly) {
  await refreshCurrentRegistryAuthorities();
  process.exit(0);
}

if (candidateFixturesOnly) {
  for (const fixture of candidateFixtures) await resealCandidateFixture(fixture);
  await refreshCurrentRegistryAuthorities();
  process.exit(0);
}

for (const fixture of fixtures) {
  const fixtureRoot = path.join(repositoryRoot, fixture.relativeRoot);
  const oraclePath = path.join(fixtureRoot, "expected-oracle.json");
  const originalOracle = await readJson(oraclePath);
  assertCurrentPackageIdentity(fixture, originalOracle, "expected-oracle.json");
  assertSemanticTruth(fixture, originalOracle);

  await materializeCurrentInputs(fixtureRoot, fixture);
  if (!fixture.platform) {
    await refreshConstructionManifest(fixtureRoot);
    await refreshAcceptedOrderConstructionFingerprint(fixtureRoot);
    await refreshBethesdaByteOracleFileMetadata(fixtureRoot);
  }
  await refreshAllLocalReferences(fixtureRoot, fixture);

  const replayDependencies = await buildReplayDependencies(fixtureRoot, fixture);
  const replayManifest = {
    schema_id: "infinium.evaluation.fixture-replay-manifest/v1",
    schema_version: "1",
    fixture_id: fixture.fixtureId,
    fixture_version: fixture.version,
    replay_state: "complete-clean",
    dependency_graph_fingerprint: replayDependencies.dependency_graph_fingerprint,
    retained_inputs: replayDependencies.dependencies
      .filter((dependency) => dependency.kind === "tracked-fixture-input")
      .map((dependency) => ({
        dependency_id: dependency.dependency_id,
        sha256: dependency.sha256,
        byte_length: dependency.byte_length,
      })),
    boundaries: ["provider", "hosted-search", "nexus", "loot"],
  };
  const replayManifestPath = path.join(fixtureRoot, "oracle", "replay-manifest.json");
  await mkdir(path.dirname(replayManifestPath), { recursive: true });
  await writeJson(replayManifestPath, replayManifest);

  const oracle = await readJson(oraclePath);
  for (const property of [
    "expected_documentation_revisions", "expected_passages", "expected_candidate_decisions",
    "expected_reconciliation_assessments", "expected_lineage_events",
  ]) {
    if (!Array.isArray(oracle[property])) {
      throw new Error(`${fixture.fixtureId} current oracle is missing ${property}.`);
    }
  }
  oracle.expected_replay_manifest = await artifactReference(
    fixtureRoot,
    "oracle/replay-manifest.json",
    fixture.version,
  );
  oracle.expected_not_used_boundaries = ["provider", "hosted-search", "nexus", "loot"];
  await refreshReferencesInValue(oracle, fixtureRoot, fixture.version);
  await writeJson(oraclePath, oracle);
  assertSemanticTruth(fixture, oracle);

  replayDependencies.expected_output_references = [
    await artifactReference(fixtureRoot, "oracle/replay-manifest.json", fixture.version),
  ];
  await writeJson(path.join(fixtureRoot, "replay-dependencies.json"), replayDependencies);

  for (const fileName of ["provenance.json", "redistribution.json", "partition-history.json"]) {
    const filePath = path.join(fixtureRoot, fileName);
    const document = await readJson(filePath);
    document.fixture_version = fixture.version;
    await writeJson(filePath, document);
  }

  const executionInput = await buildRootExecutionInput(fixtureRoot, fixture);
  await writeJson(path.join(fixtureRoot, "execution-input.json"), executionInput);

  const manifestPath = path.join(fixtureRoot, "public-manifest.json");
  const manifest = await readJson(manifestPath);
  manifest.fixture_version = fixture.version;
  manifest.partition_history = (await readJson(path.join(fixtureRoot, "partition-history.json"))).partition_history;
  manifest.input_package_fingerprint = await fileSha256(path.join(fixtureRoot, "execution-input.json"));
  manifest.oracle_fingerprint = await fileSha256(oraclePath);
  manifest.provenance_fingerprint = await fileSha256(path.join(fixtureRoot, "provenance.json"));
  manifest.replay_dependency_fingerprint = await fileSha256(
    path.join(fixtureRoot, "replay-dependencies.json"),
  );
  await writeJson(manifestPath, manifest);

  process.stdout.write(
    `${fixture.fixtureId}/${fixture.version} input=${manifest.input_package_fingerprint} ` +
      `oracle=${manifest.oracle_fingerprint} replay=${manifest.replay_dependency_fingerprint}\n`,
  );
}

for (const fixture of candidateFixtures) {
  await resealCandidateFixture(fixture);
}

await resealAnalysisPipelineFixture();
await resealProviderContractExamples();

async function resealProviderBudgetFixtures() {
  const registryPath = path.join(repositoryRoot, "fixtures/public/current-fixture-registry.v1.json");
  const registry = await readJson(registryPath);
  if (registry.registry_version !== "1.0.0" || registry.status !== "current"
      || registry.package_count !== 30 || registry.packages.length !== 30) {
    throw new Error("Provider-budget reseal requires the exact current 1.0.0/30 registry authority.");
  }
  for (const [directory, fixtureId, partition] of providerBudgetFixtures) {
    const fixtureRoot = path.join(repositoryRoot, "fixtures/public/platform/provider-budget", directory);
    const manifestPath = path.join(fixtureRoot, "public-manifest.json");
    const manifest = await readJson(manifestPath);
    if (manifest.fixture_id !== fixtureId || manifest.fixture_version !== "1.0.0"
        || manifest.partition !== partition || manifest.answer_free_input !== true
        || manifest.review_state !== "accepted") {
      throw new Error(`${fixtureId} is not the exact accepted 1.0.0 package; no downgrade or migration is permitted.`);
    }
    const input = await readJson(path.join(fixtureRoot, "input.json"));
    const inputText = JSON.stringify(input).toLowerCase();
    if (inputText.includes('"expected') || inputText.includes('"oracle')) {
      throw new Error(`${fixtureId} product input contains expected truth.`);
    }
    manifest.input_sha256 = await fileSha256(path.join(fixtureRoot, "input.json"));
    manifest.oracle_sha256 = await fileSha256(path.join(fixtureRoot, "oracle.json"));
    await writeJson(manifestPath, manifest);
    const authorityBytes = await readFile(manifestPath);
    const entry = registry.packages.find((item) => item.package_identity === fixtureId);
    if (!entry || entry.package_version !== "1.0.0" || entry.partition !== partition) {
      throw new Error(`${fixtureId} closed registry entry is missing or inconsistent.`);
    }
    entry.authority_bytes = authorityBytes.length;
    entry.authority_sha256 = sha256(authorityBytes);
    process.stdout.write(`${fixtureId}/1.0.0 input=${manifest.input_sha256} oracle=${manifest.oracle_sha256}\n`);
  }
  await writeJson(registryPath, registry);
}

async function resealProviderOfflineFixtures() {
  const registryPath = path.join(repositoryRoot, "fixtures/public/current-fixture-registry.v1.json");
  const registry = await readJson(registryPath);
  if (registry.registry_version !== "1.0.0" || registry.status !== "current"
      || registry.package_count !== 30 || registry.packages.length !== 30) {
    throw new Error("Provider-offline reseal requires the exact current 1.0.0/30 registry authority.");
  }
  for (const [directory, fixtureId, partition] of [
    ["offline-dev", "PROVIDER-OFFLINE-DEV-v1", "development"],
    ["offline-val", "PROVIDER-OFFLINE-VAL-v1", "validation"],
  ]) {
    const fixtureRoot = path.join(repositoryRoot, "fixtures/public/platform/provider-offline", directory);
    const manifestPath = path.join(fixtureRoot, "public-manifest.json");
    const manifest = await readJson(manifestPath);
    const required = ["purpose", "classification", "partition_history", "construction_provenance",
      "ground_truth_method", "preregistration", "reviewer_provenance", "answer_isolation",
      "replay_dependencies", "known_limitations"];
    if (manifest.fixture_id !== fixtureId || manifest.fixture_version !== "1.0.0"
        || manifest.partition !== partition || manifest.answer_free_input !== true
        || required.some((name) => manifest[name] === undefined)
        || manifest.construction_provenance.product_output_used_to_author_truth !== false) {
      throw new Error(`${fixtureId} lacks its exact independent public authoring metadata.`);
    }
    const input = await readJson(path.join(fixtureRoot, "input.json"));
    const forbidden = new Set(["expected", "expected_answer", "expected_label", "oracle", "answer_key"]);
    const scan = (value) => {
      if (Array.isArray(value)) return value.every(scan);
      if (value !== null && typeof value === "object") {
        return Object.entries(value).every(([name, child]) => !forbidden.has(name.toLowerCase()) && scan(child));
      }
      return true;
    };
    if (!scan(input)) throw new Error(`${fixtureId} product input contains answer-bearing material.`);
    manifest.input_sha256 = await fileSha256(path.join(fixtureRoot, "input.json"));
    manifest.oracle_sha256 = await fileSha256(path.join(fixtureRoot, "oracle.json"));
    await writeJson(manifestPath, manifest);
    const authorityBytes = await readFile(manifestPath);
    const entry = registry.packages.find((item) => item.package_identity === fixtureId);
    if (!entry || entry.package_version !== "1.0.0" || entry.partition !== partition) {
      throw new Error(`${fixtureId} closed registry entry is missing or inconsistent.`);
    }
    entry.authority_bytes = authorityBytes.length;
    entry.authority_sha256 = sha256(authorityBytes);
  }
  await writeJson(registryPath, registry);
}

async function resealProviderContractExamples() {
  const authorityRelative = "fixtures/public/contracts/provider-contract-examples/contract-examples.v1.json";
  const authorityPath = path.join(repositoryRoot, ...authorityRelative.split("/"));
  const authorityBytes = await readFile(authorityPath);
  const authority = JSON.parse(authorityBytes.toString("utf8"));
  const schemaNames = [
    "provider-access-profile.v1.schema.json", "provider-operation.v1.schema.json",
    "provider-response.v1.schema.json", "source-claim-extraction.v1.schema.json",
    "candidate-investigation.v1.schema.json", "provider-execution-input.v1.schema.json",
    "effective-scan-configuration.v2.schema.json", "run-output.v2.schema.json",
    "cli-summary.v2.schema.json",
  ];
  if (authority.package_identity !== "infinium.public-fixtures.provider-contracts.answer-free-examples"
      || authority.package_version !== "1.0.0" || authority.partition !== "development"
      || authority.status !== "Proposed" || authority.answer_free !== true
      || JSON.stringify(Object.keys(authority.examples).sort()) !== JSON.stringify(schemaNames.sort())) {
    throw new Error("Provider contract-example authority is incomplete or not answer-free.");
  }
  const serialized = JSON.stringify(authority);
  for (const forbidden of ["expected_answer", "expected_label", "oracle", "provider_secret", "credential_target", "authorization_header"]) {
    if (serialized.includes(`\"${forbidden}\"`)) {
      throw new Error(`Provider contract examples contain forbidden field ${forbidden}.`);
    }
  }

}

async function refreshCurrentRegistryAuthorities() {
  const registryPath = path.join(repositoryRoot, "fixtures/public/current-fixture-registry.v1.json");
  const registry = await readJson(registryPath);
  for (const item of registry.packages) {
    const authorityPath = path.join(repositoryRoot, ...item.authority_file.split("/"));
    const authorityBytes = await readFile(authorityPath);
    item.authority_bytes = authorityBytes.length;
    item.authority_sha256 = sha256(authorityBytes);
  }
  await writeJson(registryPath, registry);
}

async function resealAnalysisPipelineFixture() {
  const fixtureRoot = path.join(repositoryRoot, "fixtures/public/analysis-pipeline/end-to-end-corpus");
  const manifestPath = path.join(fixtureRoot, "fixture-manifest.v1.json");
  const originalExpected = await readJson(path.join(fixtureRoot, "expected-results.v1.json"));
  const packagePaths = [
    "ordinary-product-inputs.v1.json", "ordinary-product-input.schema.json",
    "harness-envelope.v1.json", "expected-results.v1.json", "provenance.v1.json",
    "replay-dependencies.v1.json", "redistribution.v1.json", "partition-history.v1.json",
    "README.md", "fixture-manifest.v1.json",
  ];

  for (const relative of packagePaths.filter((item) => item !== "fixture-manifest.v1.json")) {
    const filePath = path.join(fixtureRoot, relative);
    if (relative.endsWith(".json")) {
      await writeJson(filePath, rewriteAnalysisPipelineGovernance(await readJson(filePath)));
    } else {
      const text = await readFile(filePath, "utf8");
      await writeFile(filePath, rewriteAnalysisPipelineText(text));
    }
  }

  const historyPath = path.join(fixtureRoot, "partition-history.v1.json");
  const history = await readJson(historyPath);
  const retainedResultClosure = history.history.find(
    (entry) => entry.event.startsWith("Closed the exact retained result.001 producer-consumer flow"));
  if (!retainedResultClosure) {
    throw new Error("End-to-end partition history lost the accepted 1.0.7 retained-result closure.");
  }
  retainedResultClosure.version = "1.0.7";
  if (!history.history.some((entry) => entry.version === "1.0.8")) {
    history.history.push({
      version: "1.0.8",
      partition: "development",
      event: "Functionally normalized current fixture prose, rebound the three candidate-analysis package registrations after their authority-path and governance-version 1.0.1 reseal, and added the current functional package registry; retained 1.0.7 result/query semantics, expected facts, and product flow remained unchanged.",
      product_comparison_occurred: false,
    });
  }
  await writeJson(historyPath, history);

  const readmePath = path.join(fixtureRoot, "README.md");
  let readme = await readFile(readmePath, "utf8");
  const normalizationNarrative =
    "Version `1.0.8` functionally normalizes current fixture terminology, " +
    "rebinds the three candidate-analysis registrations to their current authority path and `1.0.1` governance seals, " +
    "and indexes the package through the functional public-fixture registry. " +
    "The accepted `1.0.7` result/query flow, expected truth, answer isolation, and product execution are unchanged.\n\n";
  if (!readme.includes(normalizationNarrative)) {
    readme = readme.replace("Package: `", normalizationNarrative + "Package: `");
  }
  await writeFile(readmePath, readme);

  const expectedAfter = await readJson(path.join(fixtureRoot, "expected-results.v1.json"));
  if (canonicalJson(rewriteAnalysisPipelineGovernance(originalExpected)) !== canonicalJson(expectedAfter)) {
    throw new Error("End-to-end normalization changed expected truth beyond functional wording/version identity.");
  }

  const manifest = rewriteAnalysisPipelineGovernance(await readJson(manifestPath));
  manifest.status = "normalization-reseal-pending-independent-review";
  manifest.package_file_paths = packagePaths;
  for (const registration of manifest.accumulated_package_registrations) {
    if (!registration.package_identity.startsWith("CAND-")) continue;
    const candidatePath = path.join(repositoryRoot, ...registration.authority_path.split("/"));
    const candidateBytes = await readFile(candidatePath);
    registration.version = "1.0.1";
    registration.bytes = candidateBytes.length;
    registration.sha256 = sha256(candidateBytes);
  }
  manifest.files = [];
  for (const relative of packagePaths.filter((item) => item !== "fixture-manifest.v1.json")) {
    const bytes = await readFile(path.join(fixtureRoot, relative));
    const previous = (await readJsonIfJsonManifest(manifestPath)).files?.find((item) => item.path === relative);
    manifest.files.push({
      path: relative,
      role: previous?.role ?? "current-public-fixture-governance",
      bytes: bytes.length,
      sha256: sha256(bytes),
    });
  }
  manifest.content_aggregate.sha256 = contentAggregate(manifest.files);
  await writeJson(manifestPath, manifest);

  const manifestBytes = await readFile(manifestPath);
  const reviewPath = path.join(fixtureRoot, "independent-review.md");
  const marker = "## v1.0.8 repository-cleanup normalization revalidation";
  let review = await readFile(reviewPath, "utf8");
  const addendum = `${marker}\n\n` +
    `Verdict: **PENDING INDEPENDENT REVIEW**\n\n` +
    `Review target: \`${manifest.package_identity}/1.0.8\`\n\n` +
    `- manifest bytes: \`${manifestBytes.length.toLocaleString("en-US")}\`\n` +
    `- manifest SHA-256: \`${sha256(manifestBytes)}\`\n` +
    `- ordered content aggregate: \`${manifest.content_aggregate.sha256}\`\n` +
    `- closure: 10 package files, 9 non-self hash/length bindings, 11 accumulated registrations\n` +
    `- required review: confirm normalization-only functional wording, candidate authority-path/version reseals, functional registry indexing, and exact retention of the accepted 1.0.7 result/query closure; confirm expected facts, answer isolation, four-case execution, and source-authority pins remain unchanged\n` +
    `- product output used to author truth: false\n`;
  if (review.includes(marker)) review = `${review.slice(0, review.indexOf(marker)).trimEnd()}\n\n${addendum}`;
  else review = `${review.trimEnd()}\n\n${addendum}`;
  await writeFile(reviewPath, review);

  process.stdout.write(
    `end-to-end/1.0.8 manifest=${sha256(manifestBytes)} aggregate=${manifest.content_aggregate.sha256}\n`,
  );
}

async function readJsonIfJsonManifest(filePath) {
  return readJson(filePath);
}

function rewriteAnalysisPipelineGovernance(value) {
  if (Array.isArray(value)) return value.map(rewriteAnalysisPipelineGovernance);
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(
      ([key, item]) => [key, rewriteAnalysisPipelineGovernance(item)]));
  }
  return typeof value === "string" ? rewriteAnalysisPipelineText(value) : value;
}

function rewriteAnalysisPipelineText(value) {
  return value
    .replaceAll("Analysis pipeline end-to-end corpus independent analysis pipeline corpus v1", "Analysis pipeline end-to-end fixture corpus v1")
    .replaceAll("Version `1.0.8` makes the prior-result chain executable", "Version `1.0.7` makes the prior-result chain executable")
    .replaceAll("documentation stage-finding/case stage", "documentation and finding/case analysis")
    .replaceAll("documentation stage/operations stage", "documentation and analysis-operations")
    .replaceAll("documentation stage-operations stage", "documentation-through-operations")
    .replaceAll("candidate stage/finding/case stage", "candidate and finding/case analysis")
    .replaceAll("candidate stage/operations stage", "candidate analysis and analysis-operations")
    .replaceAll("finding/case stage/operations stage", "finding/case analysis and analysis-operations")
    .replaceAll("contract foundation-operations stage", "contract-foundation and analysis-operations")
    .replaceAll("documentation stage", "documentation analysis")
    .replaceAll("candidate stage", "candidate analysis")
    .replaceAll("finding/case stage", "finding/case analysis")
    .replaceAll("operations stage", "analysis-operations");
}

function contentAggregate(files) {
  const value = files.map((file) => `${file.path}:${file.bytes}:${file.sha256}\n`).join("");
  return sha256(Buffer.from(value, "utf8"));
}

async function resealCandidateFixture(fixture) {
  const fixtureRoot = path.join(repositoryRoot, fixture.relativeRoot);
  const productPath = path.join(fixtureRoot, ...fixture.productArtifact.split("/"));
  const productBytes = await readFile(productPath);
  const productFingerprint = sha256(productBytes);
  const oracleArtifactPath = path.join(fixtureRoot, ...fixture.oracleArtifact.split("/"));
  const originalOracleArtifact = await readJson(oracleArtifactPath);
  const originalExpectedOracle = await readJson(path.join(fixtureRoot, "expected-oracle.json"));

  for (const relative of [
    "execution-input.json", "expected-oracle.json", "partition-history.json",
    "provenance.json", "redistribution.json", fixture.oracleArtifact,
  ]) {
    const filePath = path.join(fixtureRoot, ...relative.split("/"));
    const document = rewriteCandidateGovernance(await readJson(filePath));
    if (Object.hasOwn(document, "fixture_version")) document.fixture_version = fixture.version;
    await writeJson(filePath, document);
  }

  const executionPath = path.join(fixtureRoot, "execution-input.json");
  const execution = await readJson(executionPath);
  const inputReferences = [execution.analysis_execution_input, ...execution.input_payload_refs]
    .filter((reference) => reference?.artifact_id === fixture.productArtifact);
  if (inputReferences.length !== 2) {
    throw new Error(`${fixture.fixtureId} does not bind its product artifact in both required input locations.`);
  }
  for (const reference of inputReferences) {
    reference.fingerprint = productFingerprint;
    reference.byte_length = productBytes.length;
  }
  await writeJson(executionPath, execution);

  const authorityPath = path.join(repositoryRoot, ...candidateAuthorityPath.split("/"));
  const authorityBytes = await readFile(authorityPath);
  const verificationProfileIdentity = "docs/evaluation/product-conformance-verification-profile.md";
  const verificationProfilePath = path.join(repositoryRoot, ...verificationProfileIdentity.split("/"));
  const verificationProfileBytes = await readFile(verificationProfilePath);
  const replayPath = path.join(fixtureRoot, "replay-dependencies.json");
  const replay = rewriteCandidateGovernance(await readJson(replayPath));
  replay.fixture_version = fixture.version;
  const inputDependency = replay.dependencies.find(
    (dependency) => dependency.dependency_id === "dependency.input");
  const authorityDependency = replay.dependencies.find(
    (dependency) => dependency.dependency_id === "dependency.field-guide");
  const verificationProfileDependency = replay.dependencies.find(
    (dependency) => dependency.dependency_id === "dependency.verification-profile");
  if (!inputDependency || !authorityDependency || !verificationProfileDependency) {
    throw new Error(`${fixture.fixtureId} candidate replay dependency closure is incomplete.`);
  }
  Object.assign(inputDependency, {
    identity_or_version: fixture.productArtifact,
    sha256: productFingerprint,
    byte_length: productBytes.length,
  });
  Object.assign(authorityDependency, {
    identity_or_version: candidateAuthorityPath,
    sha256: sha256(authorityBytes),
    byte_length: authorityBytes.length,
  });
  Object.assign(verificationProfileDependency, {
    identity_or_version: verificationProfileIdentity,
    sha256: sha256(verificationProfileBytes),
    byte_length: verificationProfileBytes.length,
  });
  const oracleArtifactBytes = await readFile(oracleArtifactPath);
  replay.expected_output_references = [{
    artifact_id: fixture.oracleArtifact,
    artifact_version: "1.0.0",
    fingerprint: sha256(oracleArtifactBytes),
    availability: "retained",
    byte_length: oracleArtifactBytes.length,
  }];
  replay.dependency_graph_fingerprint = candidateDependencyGraphFingerprint(replay.dependencies);
  await writeJson(replayPath, replay);

  const expectedOraclePath = path.join(fixtureRoot, "expected-oracle.json");
  const expectedOracle = await readJson(expectedOraclePath);
  refreshCandidateArtifactReference(
    expectedOracle,
    fixture.oracleArtifact,
    oracleArtifactBytes,
  );
  expectedOracle.expected_replay_manifest = await artifactReference(
    fixtureRoot, "replay-dependencies.json", fixture.version);
  await writeJson(expectedOraclePath, expectedOracle);

  const manifestPath = path.join(fixtureRoot, "public-manifest.json");
  const manifest = rewriteCandidateGovernance(await readJson(manifestPath));
  manifest.fixture_version = fixture.version;
  manifest.input_package_fingerprint = await fileSha256(executionPath);
  manifest.oracle_fingerprint = await fileSha256(expectedOraclePath);
  manifest.provenance_fingerprint = await fileSha256(path.join(fixtureRoot, "provenance.json"));
  manifest.replay_dependency_fingerprint = await fileSha256(replayPath);
  await writeJson(manifestPath, manifest);

  if (await fileSha256(productPath) !== productFingerprint) {
    throw new Error(`${fixture.fixtureId} reseal changed product input bytes.`);
  }
  const expectedOracleAfter = await readJson(expectedOraclePath);
  const normalizedOriginalExpectations = candidateExpectations(
    rewriteCandidateGovernance(structuredClone(originalExpectedOracle)));
  if (canonicalJson(candidateExpectations(expectedOracleAfter)) !== canonicalJson(normalizedOriginalExpectations)) {
    throw new Error(`${fixture.fixtureId} reseal changed candidate expected truth.`);
  }
  const expectedOracleArtifact = rewriteCandidateGovernance(structuredClone(originalOracleArtifact));
  expectedOracleArtifact.fixture_version = fixture.version;
  if (canonicalJson(await readJson(oracleArtifactPath)) !== canonicalJson(expectedOracleArtifact)) {
    throw new Error(`${fixture.fixtureId} reseal changed candidate oracle facts beyond governance identity.`);
  }

  process.stdout.write(
    `${fixture.fixtureId}/${fixture.version} input=${manifest.input_package_fingerprint} ` +
      `oracle=${manifest.oracle_fingerprint} replay=${manifest.replay_dependency_fingerprint}\n`,
  );
}

function rewriteCandidateGovernance(value) {
  if (Array.isArray(value)) return value.map(rewriteCandidateGovernance);
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(
      ([key, item]) => [key, rewriteCandidateGovernance(item)]));
  }
  if (typeof value !== "string") return value;
  return value
    .replaceAll("docs/evaluation/candidate-delivered-input-v1.md", candidateAuthorityPath)
    .replaceAll("candidate-delivered-input-v1", candidateAuthorityPath)
    .replaceAll("candidate stage candidate", "candidate analysis")
    .replaceAll("candidate stage semantic", "candidate analysis semantic")
    .replaceAll("candidate stage", "candidate analysis")
    .replaceAll("finding/case stage", "finding/case analysis");
}

function candidateExpectations(oracle) {
  return Object.fromEntries(Object.entries(oracle).filter(([key]) =>
    (key.startsWith("expected_") && !["expected_replay_manifest"].includes(key))
      || key === "forbidden_claims"));
}

function refreshCandidateArtifactReference(value, artifactId, bytes) {
  if (Array.isArray(value)) {
    for (const item of value) refreshCandidateArtifactReference(item, artifactId, bytes);
    return;
  }
  if (value === null || typeof value !== "object") return;
  if (value.artifact_id === artifactId) {
    value.fingerprint = sha256(bytes);
    value.byte_length = bytes.length;
  }
  for (const nested of Object.values(value)) {
    refreshCandidateArtifactReference(nested, artifactId, bytes);
  }
}

function candidateDependencyGraphFingerprint(dependencies) {
  const canonical = dependencies
    .map((dependency) =>
      `${dependency.identity_or_version}\0${dependency.sha256}\0${dependency.byte_length}\n`)
    .sort((left, right) => left.localeCompare(right, "en"))
    .join("");
  return sha256(Buffer.from(canonical, "utf8"));
}

async function materializeCurrentInputs(fixtureRoot, fixture) {
  const inputRoot = path.join(fixtureRoot, "inputs");
  await mkdir(inputRoot, { recursive: true });
  const analyzerPath = path.join(inputRoot, "analyzer-declaration.json");
  const analyzer = analyzerDeclaration(fixture);
  await writeJson(analyzerPath, analyzer);
  const analyzerReference = await artifactReference(
    fixtureRoot,
    "inputs/analyzer-declaration.json",
    fixture.version,
  );

  const analysisContext = {
    schema_id: "infinium.analysis.fixture-context/v1",
    schema_version: "1",
    context_id: `${fixture.fixtureId.toLowerCase()}.current-public-context`,
    fixture_id: fixture.fixtureId,
    fixture_version: fixture.version,
    authority: "project-authored-current-public-fixture",
    use_case: fixture.platform ? "platform-substrate-regression" : "bethesda-semantic-regression",
  };
  await writeJson(path.join(inputRoot, "analysis-context.json"), analysisContext);

  const installationSnapshot = fixture.platform
    ? {
        schema_id: "infinium.analysis.fixture-installation-snapshot/v1",
        schema_version: "1",
        snapshot_id: `${fixture.fixtureId.toLowerCase()}.synthetic-platform-snapshot`,
        fixture_id: fixture.fixtureId,
        fixture_version: fixture.version,
        state: "empty",
        source_artifact_id: null,
        assurance: "structural",
        reason: "The platform substrate has no game installation and retains an explicit empty snapshot.",
      }
    : {
        schema_id: "infinium.analysis.fixture-installation-snapshot/v1",
        schema_version: "1",
        snapshot_id: `${fixture.fixtureId.toLowerCase()}.synthetic-bethesda-snapshot`,
        fixture_id: fixture.fixtureId,
        fixture_version: fixture.version,
        state: "provided",
        source_artifact_id: "inputs/snapshot/accepted-order.json",
        assurance: "selectively-content-sealed",
        reason: "The current product input binds the retained synthetic accepted-order construction receipt.",
      };
  await writeJson(path.join(inputRoot, "analysis-installation-snapshot.json"), installationSnapshot);

  const semanticInput = {
    schema_id: "infinium.analysis.fixture-bethesda-semantic-input/v1",
    schema_version: "1",
    semantic_input_id: `${fixture.fixtureId.toLowerCase()}.semantic-input`,
    fixture_id: fixture.fixtureId,
    fixture_version: fixture.version,
    state: fixture.platform ? "not-applicable" : "provided",
    case_matrix_artifact_id: fixture.platform ? null : "inputs/case-matrix.json",
    accepted_order_artifact_id: fixture.platform ? null : "inputs/snapshot/accepted-order.json",
    reason: fixture.platform
      ? "Bethesda semantics are outside this platform-only package."
      : "The input selects retained bytes and operations without containing outcome labels.",
  };
  await writeJson(path.join(inputRoot, "bethesda-semantic-input.json"), semanticInput);

  const effectiveConfiguration = effectiveScanConfiguration(fixture, analyzerReference);
  const effectivePath = path.join(inputRoot, fixture.platform
    ? "effective-scan-configuration.json"
    : "effective-scan-configuration.json");
  await writeJson(effectivePath, effectiveConfiguration);

  const resolvedManifest = {
    schema_id: "infinium.evaluation.current-public-resolved-input-manifest/v1",
    schema_version: "1",
    manifest_id: `${fixture.fixtureId.toLowerCase()}.resolved-inputs`,
    fixture_id: fixture.fixtureId,
    fixture_version: fixture.version,
    seed: 3520260730,
    source_artifact_ids: fixture.platform
      ? ["inputs/analysis-context.json"]
      : ["inputs/case-matrix.json", "inputs/snapshot/accepted-order.json"],
    analyzer_artifact_ids: ["inputs/analyzer-declaration.json"],
  };
  await writeJson(path.join(inputRoot, "resolved-input-manifest.json"), resolvedManifest);

  const execution = {
    schema_id: "infinium.analysis.execution-input/v1",
    schema_version: "1.0.0",
    execution_input_id: `${fixture.fixtureId.toLowerCase()}.clean-execution`,
    run_id: `${fixture.fixtureId.toLowerCase()}.clean-run`,
    installation_snapshot: await artifactReference(
      fixtureRoot,
      "inputs/analysis-installation-snapshot.json",
      fixture.version,
    ),
    bethesda_semantic_input: await artifactReference(
      fixtureRoot,
      "inputs/bethesda-semantic-input.json",
      fixture.version,
    ),
    source_inputs: [
      await artifactReference(fixtureRoot, "inputs/analysis-context.json", fixture.version),
    ],
    analyzer_declarations: [analyzerReference],
    effective_configuration: await artifactReference(
      fixtureRoot,
      "inputs/effective-scan-configuration.json",
      fixture.version,
    ),
    resolved_input_manifest: await artifactReference(
      fixtureRoot,
      "inputs/resolved-input-manifest.json",
      fixture.version,
    ),
    mode: "clean",
    seed: 3520260730,
    limits: {
      maximum_entities: fixture.platform ? 64 : 10000,
      maximum_edges: fixture.platform ? 128 : 20000,
      maximum_truth_rows: fixture.platform ? 64 : 10000,
      maximum_output_items: fixture.platform ? 64 : 10000,
      maximum_wall_time_milliseconds: 120000,
    },
    boundaries: notUsedBoundaries("This current public package is local and non-billable."),
  };
  await writeJson(path.join(inputRoot, "analysis-execution-input.json"), execution);
}

function analyzerDeclaration(fixture) {
  const evaluationId = fixture.evaluationIds[0];
  return {
    schema_id: "infinium.analyzer.declaration/v1",
    schema_version: "1",
    analyzer_id: fixture.platform ? "current-public-platform-substrate" : "current-public-bethesda-semantic",
    analyzer_version: "1.0.0",
    semantic_contract_version: "1.0.0",
    identity_contract_version: "1.0.0",
    ruleset_version: "1.0.0",
    taxonomy_id: "infinium.skyrim-se.mod-impact-taxonomy",
    taxonomy_version: "0.1.0",
    scope: {
      supported_inputs: fixture.platform ? ["synthetic-platform-substrate"] : ["sealed-bethesda-plugin-bytes"],
      excluded_inputs: [{ scope_id: "live-or-private-input", reason: "Only retained public local inputs are admitted." }],
      supported_record_field_asset_shapes: fixture.platform ? ["platform-substrate-event"] : ["bounded-bethesda-record-field-link"],
      excluded_record_field_asset_shapes: [{ scope_id: "unregistered-shape", reason: "Unregistered shapes remain explicit gaps." }],
      supported_taxonomy_codes: ["bounded-current-public-scope"],
      unsupported_taxonomy_codes: [{ scope_id: "unbound-taxonomy-code", reason: "Unbound codes are not inferred." }],
      supported_extent_facets: ["direct"],
      excluded_extent_facets: [{ scope_id: "unproven-propagation", reason: "Propagation requires separate evidence." }],
    },
    input_populations: [{ population_id: "retained-public-inputs", description: "Retained current public package inputs.", required: true }],
    dependencies: [],
    minimum_snapshot_assurance: fixture.platform ? "structural" : "selectively-content-sealed",
    thresholds: Object.fromEntries(["candidate_admission", "evidence", "abstention", "finding_promotion"].map((name) => [name, {
      rule_id: `${name.replaceAll("_", "-")}-v1`,
      ruleset_version: "1.0.0",
      description: "Use the accepted typed rule without numeric score authority.",
    }])),
    possible_outputs: ["observation", "deterministic-result", "candidate", "hypothesis", "finding", "recommendation", "supported-case", "lead-only-case", "abstention", "invalid-input", "coverage-gap", "failure"],
    coverage: {
      populations: ["retained-public-inputs"],
      possible_states: ["completed", "completed-with-gaps", "failed", "skipped-by-configuration", "skipped-by-limit", "unsupported"],
      unsupported_behavior: "Retain the input and expose a typed gap without promotion.",
    },
    operation_requirements: { mode: "local-only", network_required: false, llm_required: false, provider_required: false },
    expected_scale_and_cost: { population_scale: fixture.platform ? "small" : "bounded-fixture", cost_class: "local-moderate", billable: false },
    resource_bounds: { max_input_items: fixture.platform ? 64 : 10000, max_output_items: fixture.platform ? 64 : 10000, max_wall_time_ms: 120000 },
    maturity: "Experimental",
    raw_development_output: true,
    preset_or_maturity_suppression: false,
    linked_evaluation_cases: {
      positive: [evaluationId], negative: [evaluationId], boundary: [evaluationId],
      malformed: [evaluationId], cross_category: [evaluationId], gap: [evaluationId],
    },
    payload_contracts: payloadContracts(),
    state_model_version: "1.0.0",
    not_used_boundaries: notUsedBoundaries("Current public fixture execution is local and non-billable."),
  };
}

function effectiveScanConfiguration(fixture, analyzerReference) {
  return {
    schema_id: "infinium.scan.effective-configuration/v1",
    schema_version: "1",
    configuration_id: `${fixture.fixtureId.toLowerCase()}.current-public`,
    configuration_version: fixture.version,
    resolved_at: "2026-08-08T00:00:00.0000000+00:00",
    saved_configuration_reference: null,
    analyzers: [{
      analyzer_id: fixture.platform ? "current-public-platform-substrate" : "current-public-bethesda-semantic",
      analyzer_version: "1.0.0",
      declaration_fingerprint: analyzerReference.fingerprint,
      enabled: true,
      origin: "default",
    }],
    sources: [{ source_id: "retained-current-public-inputs", mode: "local-fixture", enabled: true, origin: "default" }],
    budgets: { max_dispatch_count: 0, max_input_tokens: 0, max_output_tokens: 0, max_hosted_search_calls: 0, max_nano_usd: 0, dispatch_deadline: "2026-08-08T00:02:00.0000000+00:00", origin: "default" },
    cache_policy: { analytical_mode: "force-clean-recomputation", source_mode: "reuse-resolved-source", provider_cache_mode: "disabled", origin: "default" },
    tracing: { enabled: false, level: "off", sensitivity_label: "sensitive-development-diagnostic", origin: "default" },
    candidate_breadth: { mode: "declared-mandatory-and-causal-lanes", max_candidates: 10000, all_pairs_llm_comparison: false, origin: "default" },
    thresholds: [],
    provider: { mode: "disabled", origin: "default" },
    resources: { max_general_workers: 1, max_memory_bytes: 536870912, max_output_bytes: 16777216, origin: "default" },
    semantic_context_overrides: [],
    payload_contracts: payloadContracts(),
    not_used_boundaries: notUsedBoundaries("The current public fixture configuration is local and non-billable."),
  };
}

function payloadContracts() {
  return [
    "infinium.documentation.evidence/v1", "infinium.analysis.candidate/v1",
    "infinium.analysis.finding-case/v1", "infinium.analysis.replay/v1", "infinium.run-output/v1",
  ].map((schema_id) => ({ schema_id, schema_version: "1.0.0", required: true }));
}

function notUsedBoundaries(reason) {
  return ["provider", "hosted-search", "nexus", "loot"].map((boundary_id) => ({
    boundary_id,
    state: "not-used",
    reason,
  }));
}

async function refreshConstructionManifest(fixtureRoot) {
  const inputRoot = path.join(fixtureRoot, "inputs");
  const manifestPath = path.join(inputRoot, "construction-manifest.json");
  const manifest = await readJson(manifestPath);
  const existing = new Map(manifest.files.map((entry) => [entry.path, entry]));
  const generatedPaths = (await enumerateFiles(inputRoot))
    .filter((relative) => relative !== "construction-manifest.json")
    .filter((relative) => !relative.startsWith("snapshot/"));
  manifest.files = [];
  for (const relative of generatedPaths) {
    const bytes = await readFile(path.join(inputRoot, ...relative.split("/")));
    const entry = existing.get(relative) ?? {
      path: relative,
      regions: [{ offset: 0, length: bytes.length, kind: "project-authored-analysis-input-metadata" }],
    };
    entry.byte_length = bytes.length;
    entry.sha256 = sha256(bytes);
    if (entry.regions.length === 1 && entry.regions[0].offset === 0) entry.regions[0].length = bytes.length;
    manifest.files.push(entry);
  }
  const self = existing.get("construction-manifest.json") ?? {
    path: "construction-manifest.json", byte_length: 0, sha256: null,
    regions: [{ offset: 0, length: 1, kind: "project-authored-construction-metadata" }],
  };
  self.sha256 = null;
  manifest.files.push(self);
  for (let attempt = 0; attempt < 12; attempt += 1) {
    const bytes = jsonBytes(manifest);
    if (self.byte_length === bytes.length && self.regions[0].length === bytes.length) {
      await writeFile(manifestPath, bytes);
      return;
    }
    self.byte_length = bytes.length;
    self.regions[0].length = bytes.length;
  }
  throw new Error(`Construction manifest length did not stabilize: ${fixtureRoot}`);
}

async function refreshAcceptedOrderConstructionFingerprint(fixtureRoot) {
  const receiptPath = path.join(fixtureRoot, "inputs", "snapshot", "accepted-order.json");
  const receipt = await readJson(receiptPath);
  receipt.construction_manifest_fingerprint = await fileSha256(
    path.join(fixtureRoot, "inputs", "construction-manifest.json"),
  );
  await writeJson(receiptPath, receipt);
}

async function refreshBethesdaByteOracleFileMetadata(fixtureRoot) {
  const oraclePath = path.join(fixtureRoot, "oracle", "independent-byte-facts.json");
  const oracle = await readJson(oraclePath);
  for (const file of oracle.files) {
    const bytes = await readFile(path.join(fixtureRoot, ...file.artifact_id.split("/")));
    file.byte_length = bytes.length;
    if (file.sha256 !== sha256(bytes)) {
      throw new Error(`Independent byte-oracle seal drifted for ${file.artifact_id}.`);
    }
  }
  await writeJson(oraclePath, oracle);
}

async function buildReplayDependencies(fixtureRoot, fixture) {
  const inputRoot = path.join(fixtureRoot, "inputs");
  const dependencies = [];
  for (const relative of await enumerateFiles(inputRoot)) {
    const fullPath = path.join(inputRoot, ...relative.split("/"));
    const bytes = await readFile(fullPath);
    dependencies.push({
      dependency_id: `inputs-${relative.replaceAll("/", "-").replaceAll(".", "-")}`,
      kind: "tracked-fixture-input",
      identity_or_version: fixture.version,
      sha256: sha256(bytes),
      byte_length: bytes.length,
      retention_location_class: "tracked-repository",
      availability: "retained",
      required_for: ["clean-recomputation", "boundary-replay", "audit"],
      permission_and_redistribution: "project-authored-redistributable",
      deletion_effect: "Removal makes clean recomputation incomplete and creates an explicit replay and audit gap.",
    });
  }
  const sourcePaths = ["fixtures/tooling/reseal-public-fixtures.mjs"];
  if (!fixture.platform) {
    sourcePaths.push(
      "fixtures/tooling/bethesda/Program.cs",
      "fixtures/tooling/bethesda/Infinium.BethesdaFixtures.Generator.csproj",
    );
  }
  for (const relative of sourcePaths) {
    const fullPath = path.join(repositoryRoot, ...relative.split("/"));
    const bytes = await readFile(fullPath);
    dependencies.push({
      dependency_id: relative.replaceAll("/", "-").replaceAll(".", "-"),
      kind: "tracked-source",
      identity_or_version: sha256(bytes),
      sha256: sha256(bytes),
      byte_length: bytes.length,
      retention_location_class: "tracked-repository",
      availability: "retained",
      required_for: ["clean-recomputation", "audit"],
      permission_and_redistribution: "project-authored-redistributable",
      deletion_effect: "Retained package bytes remain auditable, but deterministic current-package reconstruction is lost.",
    });
  }
  return {
    fixture_id: fixture.fixtureId,
    fixture_version: fixture.version,
    expected_replay_state: "complete-clean",
    dependencies,
    dependency_graph_fingerprint: sha256(Buffer.from(canonicalJson(dependencies), "utf8")),
    expected_output_references: [],
  };
}

async function buildRootExecutionInput(fixtureRoot, fixture) {
  const previous = await readJson(path.join(fixtureRoot, "execution-input.json"));
  const ref = (artifactId) => artifactReference(fixtureRoot, artifactId, fixture.version);
  const inputPaths = await enumerateFiles(path.join(fixtureRoot, "inputs"));
  const inputReferences = [];
  let inputBytes = 0;
  for (const relative of inputPaths) {
    const reference = await ref(`inputs/${relative}`);
    inputReferences.push(reference);
    inputBytes += reference.byte_length;
  }
  previous.fixture_version = fixture.version;
  previous.installation_snapshot_input = fixture.platform
    ? {
        state: "provided",
        reason: "A retained empty synthetic snapshot makes the platform-only boundary explicit.",
        artifact: await ref("inputs/analysis-installation-snapshot.json"),
      }
    : {
        state: "not-applicable",
        reason: "The canonical Bethesda package uses the distinct accepted-order construction role; the current analysis envelope retains its own typed snapshot descriptor.",
      };
  previous.analysis_context_input = {
    state: "provided",
    reason: "The current answer-free analysis context is retained and fingerprinted.",
    artifact: await ref("inputs/analysis-context.json"),
  };
  previous.effective_scan_configuration = await ref("inputs/effective-scan-configuration.json");
  previous.analysis_execution_input = await ref("inputs/analysis-execution-input.json");
  previous.analyzer_declarations = [await ref("inputs/analyzer-declaration.json")];
  const resealerSha256 = await fileSha256(
    path.join(repositoryRoot, "fixtures", "tooling", "reseal-public-fixtures.mjs"),
  );
  previous.tool_library_versions = fixture.platform
    ? [{ component_id: "current-public-fixture-resealer", version: "1.0.0", fingerprint: resealerSha256 }]
    : [
        {
          component_id: "Infinium.BethesdaFixtures.Generator",
          version: "1.4.0",
          fingerprint: await fileSha256(
            path.join(repositoryRoot, "fixtures", "tooling", "bethesda", "Program.cs"),
          ),
        },
        { component_id: "current-public-fixture-resealer", version: "1.0.0", fingerprint: resealerSha256 },
      ];
  previous.resource_and_time_limits.input_bytes = inputBytes;
  previous.input_payload_refs = inputReferences;
  return previous;
}

async function refreshAllLocalReferences(fixtureRoot, fixture) {
  for (let pass = 0; pass < 4; pass += 1) {
    for (const relative of await enumerateFiles(fixtureRoot)) {
      if (!relative.endsWith(".json") || relative === "public-manifest.json") continue;
      const filePath = path.join(fixtureRoot, ...relative.split("/"));
      const value = await readJson(filePath);
      await refreshReferencesInValue(value, fixtureRoot, fixture.version);
      await writeJson(filePath, value);
    }
  }
}

async function refreshReferencesInValue(value, fixtureRoot, fixtureVersion) {
  if (Array.isArray(value)) {
    for (const item of value) await refreshReferencesInValue(item, fixtureRoot, fixtureVersion);
    return;
  }
  if (!value || typeof value !== "object") return;
  const artifactReferenceProperties = new Set([
    "artifact_id", "artifact_version", "fingerprint", "availability", "byte_length",
  ]);
  const isExactArtifactReferenceShape =
    typeof value.artifact_id === "string" &&
    Object.keys(value).every((property) => artifactReferenceProperties.has(property));
  if (typeof value.artifact_id === "string" && !isExactArtifactReferenceShape) {
    delete value.artifact_version;
    delete value.fingerprint;
    delete value.availability;
    if (!Object.hasOwn(value, "byte_coverage")) delete value.byte_length;
  }
  if (
    isExactArtifactReferenceShape &&
    (value.artifact_id.startsWith("inputs/") || value.artifact_id.startsWith("oracle/"))
  ) {
    const fullPath = path.join(fixtureRoot, ...value.artifact_id.split("/"));
    try {
      const bytes = await readFile(fullPath);
      value.artifact_version = fixtureVersion;
      value.fingerprint = sha256(bytes);
      value.availability = "retained";
      value.byte_length = bytes.length;
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
  }
  for (const nested of Object.values(value)) await refreshReferencesInValue(nested, fixtureRoot, fixtureVersion);
}

function assertSemanticTruth(fixture, oracle) {
  const value = Object.fromEntries(semanticTruthProperties.map((property) => [property, oracle[property]]));
  const actual = sha256(Buffer.from(canonicalJson(value), "utf8"));
  if (actual !== fixture.semanticTruthSha256) {
    throw new Error(`${fixture.fixtureId} semantic oracle truth changed: ${actual}`);
  }
}

function assertCurrentPackageIdentity(fixture, document, documentName) {
  if (document.fixture_id !== fixture.fixtureId || document.fixture_version !== fixture.version) {
    throw new Error(
      `${fixture.fixtureId} ${documentName} is not the current ${fixture.version} identity; ` +
      "this tool does not migrate predecessor packages.",
    );
  }
}

async function artifactReference(fixtureRoot, artifactId, fixtureVersion) {
  const fullPath = path.join(fixtureRoot, ...artifactId.split("/"));
  const bytes = await readFile(fullPath);
  return {
    artifact_id: artifactId,
    artifact_version: fixtureVersion,
    fingerprint: sha256(bytes),
    availability: "retained",
    byte_length: bytes.length,
  };
}

async function enumerateFiles(directory, prefix = "") {
  const result = [];
  const entries = await readdir(directory, { withFileTypes: true });
  entries.sort((left, right) => left.name.localeCompare(right.name, "en"));
  for (const entry of entries) {
    const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...await enumerateFiles(fullPath, relative));
    else if (entry.isFile()) result.push(relative);
  }
  return result;
}

async function readJson(filePath) {
  return JSON.parse(await readFile(filePath, "utf8"));
}

async function writeJson(filePath, value) {
  await mkdir(path.dirname(filePath), { recursive: true });
  const bytes = jsonBytes(value);
  for (let attempt = 0; ; attempt += 1) {
    try {
      await writeFile(filePath, bytes);
      return;
    } catch (error) {
      if (error.code !== "UNKNOWN" || attempt === 9) throw error;
      await new Promise((resolve) => setTimeout(resolve, 25 * (attempt + 1)));
    }
  }
}

function jsonBytes(value) {
  return Buffer.from(`${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function fileSha256(filePath) {
  return sha256(await readFile(filePath));
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function canonicalJson(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(",")}]`;
  if (value !== null && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalJson(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}
