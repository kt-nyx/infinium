import { createHash } from "node:crypto";
import {
  readFileSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { performance } from "node:perf_hooks";
import os from "node:os";

const artifactDir = dirname(fileURLToPath(import.meta.url));
const configPath = join(artifactDir, "benchmark-config.json");
const truthPath = join(artifactDir, "benchmark-truth-manifest.json");
const resultPath = join(artifactDir, "benchmark-results.json");

class XorShift32 {
  constructor(seed) {
    this.state = seed >>> 0 || 0x9e3779b9;
  }

  nextU32() {
    let value = this.state;
    value ^= value << 13;
    value ^= value >>> 17;
    value ^= value << 5;
    this.state = value >>> 0;
    return this.state;
  }

  int(maxExclusive) {
    return this.nextU32() % maxExclusive;
  }
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex").toUpperCase();
}

function stablePair(left, right) {
  if (!Number.isInteger(left) || !Number.isInteger(right) || left === right) {
    throw new Error(`Invalid participant pair: ${left}, ${right}`);
  }
  return left < right ? [left, right] : [right, left];
}

function pairForCase(modCount, familyIndex, outcomeIndex, caseIndex, repeat) {
  const left =
    (familyIndex * 131 + outcomeIndex * 47 + caseIndex * 17 + repeat * 19) %
    modCount;
  const offset =
    1 + ((caseIndex * 31 + familyIndex * 7 + outcomeIndex * 11) % (modCount - 1));
  return stablePair(left, (left + offset) % modCount);
}

function materializeTruth(config) {
  const deterministic = new Set(config.deterministicFamilies);
  const cases = [];
  const outcomes = ["supported-positive", "matched-negative", "unsupported"];

  for (let scaleIndex = 0; scaleIndex < config.scaleManifests.length; scaleIndex++) {
    const scale = config.scaleManifests[scaleIndex];
    for (let repeat = 0; repeat < config.repeats; repeat++) {
      for (let familyIndex = 0; familyIndex < config.familyOrder.length; familyIndex++) {
        const family = config.familyOrder[familyIndex];
        for (let outcomeIndex = 0; outcomeIndex < outcomes.length; outcomeIndex++) {
          const expectedClass = outcomes[outcomeIndex];
          for (
            let caseIndex = 0;
            caseIndex < scale.casesPerFamilyPerOutcome;
            caseIndex++
          ) {
            const padded = String(caseIndex).padStart(3, "0");
            const observationKey =
              `${scale.id}/r${repeat}/${family}/${expectedClass}/${padded}`;
            const expectedDisposition =
              expectedClass === "matched-negative"
                ? "resolved-negative"
                : expectedClass === "unsupported"
                  ? "gap"
                  : deterministic.has(family)
                    ? "deterministic-local"
                    : "mandatory-semantic";
            cases.push({
              ordinal: cases.length,
              observationKey,
              scale: scale.id,
              repeat,
              family,
              expectedClass,
              expectedDisposition,
              participantModIds: pairForCase(
                scale.mods,
                familyIndex,
                outcomeIndex,
                caseIndex,
                repeat,
              ),
            });
          }
        }
      }
    }
  }

  return {
    schema: "infinium.rq035.truth-manifest/2",
    generatorVersion: config.generatorVersion,
    configSha256: sha256(readFileSync(configPath)),
    cases,
  };
}

function prepareTruth() {
  const config = JSON.parse(readFileSync(configPath, "utf8"));
  const truth = materializeTruth(config);
  writeFileSync(truthPath, `${JSON.stringify(truth, null, 2)}\n`);
  console.log(
    `Prepared ${truth.cases.length} explicit truth cases at ${truthPath}`,
  );
}

function makeStateSurface(capacity) {
  const state = new Uint8Array(capacity);
  const observationOrdinal = new Int32Array(capacity);
  observationOrdinal.fill(-1);
  return {
    state,
    observationOrdinal,
    leftMod: new Uint16Array(capacity),
    rightMod: new Uint16Array(capacity),
  };
}

function encodeTruthState(expectedClass) {
  switch (expectedClass) {
    case "supported-positive":
      return 1;
    case "matched-negative":
      return 2;
    case "unsupported":
      return 3;
    default:
      throw new Error(`Unknown truth class ${expectedClass}`);
  }
}

function plantStateSurface(surface, cases, leadCount, modCount, rng) {
  if (cases.length + leadCount > surface.state.length) {
    throw new Error("State-surface capacity is too small");
  }
  let cursor = 0;
  for (const truthCase of cases) {
    surface.state[cursor] = encodeTruthState(truthCase.expectedClass);
    surface.observationOrdinal[cursor] = truthCase.ordinal;
    surface.leftMod[cursor] = truthCase.participantModIds[0];
    surface.rightMod[cursor] = truthCase.participantModIds[1];
    cursor++;
  }
  for (let index = 0; index < leadCount; index++, cursor++) {
    surface.state[cursor] = 4;
    const left = rng.int(modCount);
    const right = (left + 1 + rng.int(modCount - 1)) % modCount;
    [surface.leftMod[cursor], surface.rightMod[cursor]] = stablePair(left, right);
  }
  return cursor;
}

function buildProviderSurface(scale, cases, rng) {
  const absentSlots =
    cases.filter((item) => item.expectedClass !== "matched-negative").length +
    scale.investigativeAssetLeads;
  const pathUniverse = scale.logicalPaths + absentSlots;
  const head = new Int32Array(pathUniverse);
  head.fill(-1);
  const next = new Int32Array(scale.providerEntries);
  const providerMod = new Uint16Array(scale.providerEntries);

  for (let index = 0; index < scale.providerEntries; index++) {
    const pathId =
      index < scale.logicalPaths ? index : rng.int(scale.logicalPaths);
    next[index] = head[pathId];
    head[pathId] = index;
    providerMod[index] = rng.int(scale.mods);
  }

  const targetPath = new Uint32Array(scale.referenceEdges);
  const requiredness = new Uint8Array(scale.referenceEdges);
  const observationOrdinal = new Int32Array(scale.referenceEdges);
  observationOrdinal.fill(-1);
  const consumerMod = new Uint16Array(scale.referenceEdges);
  const expectedSourceMod = new Uint16Array(scale.referenceEdges);

  for (let index = 0; index < scale.referenceEdges; index++) {
    targetPath[index] = rng.int(scale.logicalPaths);
    consumerMod[index] = rng.int(scale.mods);
    expectedSourceMod[index] = rng.int(scale.mods);
  }

  let row = 0;
  let absentPath = scale.logicalPaths;
  for (const truthCase of cases) {
    requiredness[row] = truthCase.expectedClass === "unsupported" ? 2 : 1;
    targetPath[row] =
      truthCase.expectedClass === "matched-negative"
        ? rng.int(scale.logicalPaths)
        : absentPath++;
    observationOrdinal[row] = truthCase.ordinal;
    consumerMod[row] = truthCase.participantModIds[0];
    expectedSourceMod[row] = truthCase.participantModIds[1];
    row++;
  }

  for (let index = 0; index < scale.investigativeAssetLeads; index++, row++) {
    requiredness[row] = 3;
    targetPath[row] = absentPath++;
    const left = rng.int(scale.mods);
    const right = (left + 1 + rng.int(scale.mods - 1)) % scale.mods;
    [consumerMod[row], expectedSourceMod[row]] = stablePair(left, right);
  }

  return {
    head,
    next,
    providerMod,
    targetPath,
    requiredness,
    observationOrdinal,
    consumerMod,
    expectedSourceMod,
  };
}

function casesFor(truth, scaleId, repeat, family) {
  return truth.cases.filter(
    (item) =>
      item.scale === scaleId &&
      item.repeat === repeat &&
      item.family === family,
  );
}

function buildFixture(config, truth, scale, repeat, seed) {
  const rng = new XorShift32(seed);
  const family = config.familyOrder;
  const started = performance.now();

  const asset = buildProviderSurface(
    scale,
    casesFor(truth, scale.id, repeat, family[0]),
    rng,
  );
  const record = makeStateSurface(scale.records);
  plantStateSurface(
    record,
    casesFor(truth, scale.id, repeat, family[1]),
    scale.investigativeRecordLeads,
    scale.mods,
    rng,
  );
  const topology = makeStateSurface(scale.records);
  plantStateSurface(
    topology,
    casesFor(truth, scale.id, repeat, family[2]),
    scale.investigativeRecordLeads,
    scale.mods,
    rng,
  );
  const script = makeStateSurface(scale.scriptConsumers);
  plantStateSurface(
    script,
    casesFor(truth, scale.id, repeat, family[3]),
    scale.investigativeScriptLeads,
    scale.mods,
    rng,
  );
  const generator = makeStateSurface(scale.generators);
  plantStateSurface(
    generator,
    casesFor(truth, scale.id, repeat, family[4]),
    0,
    scale.mods,
    rng,
  );
  const configSurface = makeStateSurface(scale.configs);
  plantStateSurface(
    configSurface,
    casesFor(truth, scale.id, repeat, family[5]),
    0,
    scale.mods,
    rng,
  );
  const native = makeStateSurface(scale.nativeComponents);
  plantStateSurface(
    native,
    casesFor(truth, scale.id, repeat, family[6]),
    0,
    scale.mods,
    rng,
  );
  const patch = makeStateSurface(scale.patches);
  plantStateSurface(
    patch,
    casesFor(truth, scale.id, repeat, family[7]),
    scale.investigativePatchLeads,
    scale.mods,
    rng,
  );

  const neighborhoodLeft = new Uint16Array(scale.broadNeighborhoodLeads);
  const neighborhoodRight = new Uint16Array(scale.broadNeighborhoodLeads);
  for (let index = 0; index < scale.broadNeighborhoodLeads; index++) {
    const left = rng.int(scale.mods);
    const right = (left + 1 + rng.int(scale.mods - 1)) % scale.mods;
    [neighborhoodLeft[index], neighborhoodRight[index]] = stablePair(left, right);
  }

  return {
    scaleId: scale.id,
    repeat,
    seed,
    buildMs: performance.now() - started,
    asset,
    record,
    topology,
    script,
    generator,
    config: configSurface,
    native,
    patch,
    neighborhoodLeft,
    neighborhoodRight,
  };
}

function makeEvent({
  family,
  observationOrdinal,
  disposition,
  leftMod,
  rightMod,
  score = null,
  sourceRow,
}) {
  const participantModIds = stablePair(leftMod, rightMod);
  return {
    family,
    observationOrdinal,
    disposition,
    participantModIds,
    pairKey: `${participantModIds[0]}:${participantModIds[1]}`,
    score,
    sourceRow,
  };
}

function detectProviderSurface(input, family) {
  const events = [];
  for (let row = 0; row < input.targetPath.length; row++) {
    const state = input.requiredness[row];
    if (state === 0) continue;
    const present = input.head[input.targetPath[row]] !== -1;
    let disposition;
    if (state === 1) {
      disposition = present ? "resolved-negative" : "deterministic-local";
    } else if (state === 2) {
      disposition = "gap";
    } else {
      if (present) continue;
      disposition = "investigative-lead";
    }
    events.push(
      makeEvent({
        family,
        observationOrdinal: input.observationOrdinal[row],
        disposition,
        leftMod: input.consumerMod[row],
        rightMod: input.expectedSourceMod[row],
        sourceRow: row,
      }),
    );
  }
  return events;
}

const mandatoryScores = {
  "record-scope-reversion": 60,
  "placed-reference-topology-reversion": 85,
  "script-public-api-regression": 70,
  "runtime-generated-output-stale": 55,
  "patch-overwritten-or-stale": 75,
};

function detectStateSurface(input, family, supportedDisposition) {
  const events = [];
  for (let row = 0; row < input.state.length; row++) {
    const state = input.state[row];
    if (state === 0) continue;
    const disposition =
      state === 1
        ? supportedDisposition
        : state === 2
          ? "resolved-negative"
          : state === 3
            ? "gap"
            : "investigative-lead";
    events.push(
      makeEvent({
        family,
        observationOrdinal: input.observationOrdinal[row],
        disposition,
        leftMod: input.leftMod[row],
        rightMod: input.rightMod[row],
        sourceRow: row,
      }),
    );
  }
  return events;
}

function rankMandatoryLane(events, scorePolicy) {
  return events
    .filter((item) => item.disposition === "mandatory-semantic")
    .map((item) => ({
      ...item,
      score: scorePolicy[item.family],
    }))
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.family.localeCompare(right.family) ||
        left.observationOrdinal - right.observationOrdinal,
    );
}

function detectFixture(fixture, config) {
  const family = config.familyOrder;
  const started = performance.now();
  const events = [
    ...detectProviderSurface(fixture.asset, family[0]),
    ...detectStateSurface(fixture.record, family[1], "mandatory-semantic"),
    ...detectStateSurface(fixture.topology, family[2], "mandatory-semantic"),
    ...detectStateSurface(fixture.script, family[3], "mandatory-semantic"),
    ...detectStateSurface(fixture.generator, family[4], "mandatory-semantic"),
    ...detectStateSurface(fixture.config, family[5], "deterministic-local"),
    ...detectStateSurface(fixture.native, family[6], "deterministic-local"),
    ...detectStateSurface(fixture.patch, family[7], "mandatory-semantic"),
  ];

  for (let row = 0; row < fixture.neighborhoodLeft.length; row++) {
    events.push(
      makeEvent({
        family: "bounded-neighborhood-lead",
        observationOrdinal: -1,
        disposition: "investigative-lead",
        leftMod: fixture.neighborhoodLeft[row],
        rightMod: fixture.neighborhoodRight[row],
        sourceRow: row,
      }),
    );
  }

  const mandatoryQueue = rankMandatoryLane(events, mandatoryScores);
  const perturbedScores = Object.fromEntries(
    Object.entries(mandatoryScores).map(([familyName, score]) => [
      familyName,
      200 - score,
    ]),
  );
  const perturbedQueue = rankMandatoryLane(events, perturbedScores);
  const baselineMembership = [...mandatoryQueue]
    .map((item) => item.observationOrdinal)
    .sort((left, right) => left - right);
  const perturbedMembership = [...perturbedQueue]
    .map((item) => item.observationOrdinal)
    .sort((left, right) => left - right);
  const scorePerturbationMembershipInvariant =
    baselineMembership.length === perturbedMembership.length &&
    baselineMembership.every(
      (ordinal, index) => ordinal === perturbedMembership[index],
    );
  const scorePerturbationChangedOrdering =
    mandatoryQueue.length > 1 &&
    mandatoryQueue.some(
      (item, index) =>
        item.observationOrdinal !== perturbedQueue[index].observationOrdinal,
    );

  return {
    events,
    mandatoryQueue,
    mandatoryLaneTest: {
      baselineScores: mandatoryScores,
      perturbedScores,
      scorePerturbationMembershipInvariant,
      scorePerturbationChangedOrdering,
    },
    detectAndRankMs: performance.now() - started,
  };
}

function constructionSmokeCheck(fixture, truthCases) {
  const ordinalToExpected = new Map(
    truthCases.map((item) => [item.ordinal, item.expectedClass]),
  );
  const observed = new Map();

  const collect = (surface) => {
    for (let row = 0; row < surface.observationOrdinal.length; row++) {
      const ordinal = surface.observationOrdinal[row];
      if (ordinal >= 0) observed.set(ordinal, surface.state[row]);
    }
  };
  collect(fixture.record);
  collect(fixture.topology);
  collect(fixture.script);
  collect(fixture.generator);
  collect(fixture.config);
  collect(fixture.native);
  collect(fixture.patch);

  for (let row = 0; row < fixture.asset.observationOrdinal.length; row++) {
    const ordinal = fixture.asset.observationOrdinal[row];
    if (ordinal < 0) continue;
    const state =
      fixture.asset.requiredness[row] === 2
        ? 3
        : fixture.asset.head[fixture.asset.targetPath[row]] === -1
          ? 1
          : 2;
    observed.set(ordinal, state);
  }

  let passed = 0;
  const failures = [];
  for (const [ordinal, expectedClass] of ordinalToExpected) {
    const expectedState = encodeTruthState(expectedClass);
    const actualState = observed.get(ordinal);
    if (actualState === expectedState) {
      passed++;
    } else {
      failures.push({ ordinal, expectedState, actualState: actualState ?? null });
    }
  }
  return {
    kind: "construction-coupled-smoke-check",
    eligible: truthCases.length,
    passed,
    failures,
  };
}

function evaluateDetections(detection, truthCases) {
  const eventByOrdinal = new Map();
  const duplicateOrdinals = [];
  for (const event of detection.events) {
    if (event.observationOrdinal < 0) continue;
    if (eventByOrdinal.has(event.observationOrdinal)) {
      duplicateOrdinals.push(event.observationOrdinal);
    } else {
      eventByOrdinal.set(event.observationOrdinal, event);
    }
  }

  const outcomes = [];
  const counts = {
    supportedEligible: 0,
    supportedCorrect: 0,
    matchedNegativesEligible: 0,
    matchedNegativesResolved: 0,
    matchedNegativesEscalated: 0,
    unsupportedEligible: 0,
    unsupportedGapped: 0,
    mandatoryEligible: 0,
    mandatoryQueued: 0,
  };

  for (const truthCase of truthCases) {
    const event = eventByOrdinal.get(truthCase.ordinal);
    const observedDisposition = event?.disposition ?? "not-detected";
    const participantsCorrect =
      event !== undefined &&
      event.participantModIds.length === truthCase.participantModIds.length &&
      event.participantModIds.every(
        (modId, index) => modId === truthCase.participantModIds[index],
      );
    const correct =
      observedDisposition === truthCase.expectedDisposition &&
      participantsCorrect;
    const escalated =
      observedDisposition === "mandatory-semantic" ||
      observedDisposition === "investigative-lead";

    if (truthCase.expectedClass === "supported-positive") {
      counts.supportedEligible++;
      if (correct) counts.supportedCorrect++;
      if (truthCase.expectedDisposition === "mandatory-semantic") {
        counts.mandatoryEligible++;
        if (
          detection.mandatoryQueue.some(
            (item) => item.observationOrdinal === truthCase.ordinal,
          )
        ) {
          counts.mandatoryQueued++;
        }
      }
    } else if (truthCase.expectedClass === "matched-negative") {
      counts.matchedNegativesEligible++;
      if (observedDisposition === "resolved-negative") {
        counts.matchedNegativesResolved++;
      }
      if (escalated) counts.matchedNegativesEscalated++;
    } else {
      counts.unsupportedEligible++;
      if (observedDisposition === "gap") counts.unsupportedGapped++;
    }

    outcomes.push({
      observationKey: truthCase.observationKey,
      family: truthCase.family,
      expectedClass: truthCase.expectedClass,
      expectedDisposition: truthCase.expectedDisposition,
      observedDisposition,
      correct,
      participantsCorrect,
      escalated,
      participantModIds: event?.participantModIds ?? null,
      pairKey: event?.pairKey ?? null,
      mandatoryQueuePosition:
        truthCase.expectedDisposition === "mandatory-semantic"
          ? detection.mandatoryQueue.findIndex(
              (item) => item.observationOrdinal === truthCase.ordinal,
            )
          : null,
    });
  }

  return {
    kind: "post-detection-truth-evaluation",
    duplicateOrdinals,
    counts,
    supportedRecall:
      counts.supportedEligible === 0
        ? null
        : counts.supportedCorrect / counts.supportedEligible,
    matchedNegativeResolutionRecall:
      counts.matchedNegativesEligible === 0
        ? null
        : counts.matchedNegativesResolved / counts.matchedNegativesEligible,
    unsupportedGapRecall:
      counts.unsupportedEligible === 0
        ? null
        : counts.unsupportedGapped / counts.unsupportedEligible,
    mandatoryQueueRecall:
      counts.mandatoryEligible === 0
        ? null
        : counts.mandatoryQueued / counts.mandatoryEligible,
    outcomes,
  };
}

function allTypedArrays(value, seen = new Set()) {
  if (value === null || typeof value !== "object" || seen.has(value)) return [];
  seen.add(value);
  if (ArrayBuffer.isView(value)) return [value];
  return Object.values(value).flatMap((child) => allTypedArrays(child, seen));
}

function memorySnapshot() {
  const memory = process.memoryUsage();
  return {
    rss: memory.rss,
    heapUsed: memory.heapUsed,
    external: memory.external,
    arrayBuffers: memory.arrayBuffers,
  };
}

function summarizeEvents(events, modCount) {
  const dispositionCounts = {};
  const familyCounts = {};
  const pairKeys = new Set();
  for (const event of events) {
    if (
      event.participantModIds.some(
        (modId) => modId < 0 || modId >= modCount,
      )
    ) {
      throw new Error(
        `Participant outside 0..${modCount - 1}: ${event.participantModIds}`,
      );
    }
    dispositionCounts[event.disposition] =
      (dispositionCounts[event.disposition] ?? 0) + 1;
    familyCounts[event.family] = (familyCounts[event.family] ?? 0) + 1;
    pairKeys.add(event.pairKey);
  }
  const allPairs = (modCount * (modCount - 1)) / 2;
  return {
    events: events.length,
    dispositionCounts,
    familyCounts,
    canonicalParticipantPairs: pairKeys.size,
    allPossibleModPairs: allPairs,
    pairPopulationReduction:
      allPairs === 0 ? null : 1 - pairKeys.size / allPairs,
  };
}

function workloadFor(bundleCount, model) {
  const calls = Math.ceil(bundleCount / model.bundlesPerCall);
  const inputEnvelope =
    bundleCount * model.inputTokensPerBundle +
    calls * model.fixedInputTokensPerCall;
  const outputCap = bundleCount * model.outputCapTokensPerBundle;
  return {
    bundles: bundleCount,
    calls,
    inputEnvelope,
    outputCap,
    totalTokenEnvelope: inputEnvelope + outputCap,
  };
}

function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2
    ? sorted[middle]
    : (sorted[middle - 1] + sorted[middle]) / 2;
}

function runBenchmark() {
  const configBytes = readFileSync(configPath);
  const truthBytes = readFileSync(truthPath);
  const config = JSON.parse(configBytes);
  const truth = JSON.parse(truthBytes);
  if (truth.configSha256 !== sha256(configBytes)) {
    throw new Error("Truth manifest does not match benchmark config");
  }
  if (truth.generatorVersion !== config.generatorVersion) {
    throw new Error("Truth and config generator versions differ");
  }

  const runs = [];
  for (let scaleIndex = 0; scaleIndex < config.scaleManifests.length; scaleIndex++) {
    const scale = config.scaleManifests[scaleIndex];
    for (let repeat = 0; repeat < config.repeats; repeat++) {
      if (global.gc) global.gc();
      const before = memorySnapshot();
      const seed = (config.baseSeed + scaleIndex * 0x10000 + repeat) >>> 0;
      const runTruth = truth.cases.filter(
        (item) => item.scale === scale.id && item.repeat === repeat,
      );
      const fixture = buildFixture(config, truth, scale, repeat, seed);
      const afterBuild = memorySnapshot();
      const smoke = constructionSmokeCheck(fixture, runTruth);

      // Candidate detection receives only the generated fixture and public
      // benchmark configuration. Truth enters only the evaluator below.
      const detection = detectFixture(fixture, config);
      const afterDetection = memorySnapshot();
      const evaluationStarted = performance.now();
      const evaluation = evaluateDetections(detection, runTruth);
      const evaluationMs = performance.now() - evaluationStarted;

      const typedArrayBytes = allTypedArrays(fixture).reduce(
        (sum, item) => sum + item.byteLength,
        0,
      );
      const eventSummary = summarizeEvents(detection.events, scale.mods);
      const mandatory = workloadFor(
        detection.mandatoryQueue.length,
        config.workloadModel,
      );
      const optionalInvestigative = workloadFor(
        detection.events.filter(
          (item) => item.disposition === "investigative-lead",
        ).length,
        config.workloadModel,
      );

      runs.push({
        scale: scale.id,
        repeat,
        seed,
        inputManifest: scale,
        graphShape: {
          logicalNodes:
            scale.mods +
            scale.plugins +
            scale.logicalPaths +
            scale.records +
            scale.scriptDefinitions +
            scale.configs +
            scale.generators +
            scale.nativeComponents +
            scale.patches,
          logicalEdges:
            scale.providerEntries +
            scale.referenceEdges +
            scale.records * 2 +
            scale.scriptConsumers +
            scale.configs +
            scale.generators +
            scale.nativeComponents +
            scale.patches,
        },
        timingMs: {
          fixtureAndIndexBuild: fixture.buildMs,
          detectAndMandatoryRank: detection.detectAndRankMs,
          postDetectionEvaluation: evaluationMs,
          total:
            fixture.buildMs + detection.detectAndRankMs + evaluationMs,
        },
        memoryBytes: {
          before,
          afterBuild,
          afterDetection,
          typedArrayBytes,
          maxObservedRss: Math.max(
            before.rss,
            afterBuild.rss,
            afterDetection.rss,
          ),
        },
        constructionSmoke: smoke,
        mandatoryLaneTest: detection.mandatoryLaneTest,
        evaluation,
        eventSummary,
        workloadEnvelopes: {
          mandatorySemantic: mandatory,
          optionalInvestigative,
          combinedIfInvestigativeEnabled: workloadFor(
            mandatory.bundles + optionalInvestigative.bundles,
            config.workloadModel,
          ),
        },
      });
    }
  }

  const summaries = config.scaleManifests.map((scale) => {
    const selected = runs.filter((item) => item.scale === scale.id);
    const aggregate = selected.reduce(
      (acc, run) => {
        for (const [key, value] of Object.entries(run.evaluation.counts)) {
          acc[key] = (acc[key] ?? 0) + value;
        }
        return acc;
      },
      {},
    );
    const outcomes = selected.flatMap((item) => item.evaluation.outcomes);
    return {
      scale: scale.id,
      repeats: selected.length,
      aggregateEvaluationCounts: aggregate,
      aggregateSupportedRecall:
        aggregate.supportedCorrect / aggregate.supportedEligible,
      aggregateMatchedNegativeResolutionRecall:
        aggregate.matchedNegativesResolved / aggregate.matchedNegativesEligible,
      aggregateUnsupportedGapRecall:
        aggregate.unsupportedGapped / aggregate.unsupportedEligible,
      aggregateMandatoryQueueRecall:
        aggregate.mandatoryQueued / aggregate.mandatoryEligible,
      matchedNegativeOutcomes: outcomes.filter(
        (item) => item.expectedClass === "matched-negative",
      ),
      incorrectOutcomes: outcomes.filter((item) => !item.correct),
      medianTimingMs: {
        fixtureAndIndexBuild: median(
          selected.map((item) => item.timingMs.fixtureAndIndexBuild),
        ),
        detectAndMandatoryRank: median(
          selected.map((item) => item.timingMs.detectAndMandatoryRank),
        ),
        postDetectionEvaluation: median(
          selected.map((item) => item.timingMs.postDetectionEvaluation),
        ),
        total: median(selected.map((item) => item.timingMs.total)),
      },
      representativeRun: selected[0],
    };
  });

  const results = {
    schema: "infinium.rq035.synthetic-benchmark-results/2",
    generatedAt: new Date().toISOString(),
    runtime: {
      node: process.version,
      platform: process.platform,
      release: os.release(),
      architecture: process.arch,
      cpu: os.cpus()[0]?.model ?? null,
      physicalMemoryBytes: os.totalmem(),
      exposedGc: typeof global.gc === "function",
    },
    artifactInputs: {
      configFile: "benchmark-config.json",
      configSha256: sha256(configBytes),
      truthFile: "benchmark-truth-manifest.json",
      truthSha256: sha256(truthBytes),
    },
    evaluationBoundary: {
      detectorInputs: ["generated fixture/index inputs", "public benchmark config"],
      detectorExcludedInputs: ["truth manifest", "expected class", "expected disposition"],
      evaluationOrder: "detect first; compare to truth afterward",
      constructionSmokeIsRecallEvidence: false,
    },
    summaries,
    runs,
  };

  writeFileSync(resultPath, `${JSON.stringify(results, null, 2)}\n`);
  console.log(`Wrote benchmark results to ${resultPath}`);
}

const command = process.argv[2];
if (command === "prepare") {
  prepareTruth();
} else if (command === "run") {
  runBenchmark();
} else {
  console.error("Usage: node benchmark.mjs prepare | node --expose-gc benchmark.mjs run");
  process.exitCode = 2;
}
