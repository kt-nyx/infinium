import { describe, expect, it } from "vitest";
import type { ModInfo } from "../src/shared/types";
import {
  enrichModInfoWithMo2Meta,
  parseMo2MetaIni,
  type Mo2Meta,
} from "../src/main/mo2/mo2Meta";

const exampleMetaIni = `[General]
gameName=SkyrimSE
modid=94642
version=1.2.0.0
newestVersion=1.2.0.0
category="33,"
nexusFileStatus=1
installationFile=A Dog's Life-94642-1-2-1723057742.7z
repository=Nexus
ignoredVersion=
comments=
notes=
nexusDescription="..."
url=
hasCustomURL=false
lastNexusQuery=2024-10-31T15:49:59Z
lastNexusUpdate=2024-10-31T15:49:59Z
nexusLastModified=2024-08-07T19:09:02Z
nexusCategory=65
converted=false
validated=false

[installedFiles]
1\\modid=94642
size=1
1\\fileid=529214
`;

describe("parseMo2MetaIni", () => {
  it("parses modid, versions, repository, and installedFiles from meta.ini", () => {
    const meta = parseMo2MetaIni(exampleMetaIni) as Mo2Meta;
    expect(meta).not.toBeNull();
    expect(meta.modid).toBe(94642);
    expect(meta.version).toBe("1.2.0.0");
    expect(meta.newestVersion).toBe("1.2.0.0");
    expect(meta.repository).toBe("Nexus");
    expect(meta.nexusFileStatus).toBe(1);
    expect(meta.lastNexusQuery).toBe("2024-10-31T15:49:59Z");
    expect(meta.lastNexusUpdate).toBe("2024-10-31T15:49:59Z");
    expect(meta.nexusLastModified).toBe("2024-08-07T19:09:02Z");
    expect(meta.nexusCategory).toBe(65);
    expect(meta.installedFiles).toBeDefined();
    expect(meta.installedFiles?.length).toBe(1);
    expect(meta.installedFiles?.[0].modid).toBe(94642);
    expect(meta.installedFiles?.[0].fileid).toBe(529214);
  });

  it("returns null for empty or whitespace-only content", () => {
    expect(parseMo2MetaIni("")).toBeNull();
    expect(parseMo2MetaIni("   \n  ")).toBeNull();
  });
});

describe("enrichModInfoWithMo2Meta", () => {
  it("populates nexusId, installedVersion, and metadata.mo2 without overwriting unrelated metadata", () => {
    const base: ModInfo = {
      id: "A Dog's Life",
      name: "A Dog's Life",
      enabled: true,
      path: "C:/MO2/mods/A Dog's Life",
      plugins: [],
      metadata: {
        existingKey: "keep-me",
      },
    };

    const meta = parseMo2MetaIni(exampleMetaIni) as Mo2Meta;
    const enriched = enrichModInfoWithMo2Meta(base, meta);

    expect(enriched.nexusId).toBe(94642);
    expect(enriched.installedVersion).toBe("1.2.0.0");
    expect(enriched.metadata).toBeDefined();
    expect(enriched.metadata?.existingKey).toBe("keep-me");

    const mo2Bucket = (enriched.metadata?.mo2 ?? {}) as Record<string, unknown>;
    expect(mo2Bucket.modid).toBe(94642);
    expect(mo2Bucket.version).toBe("1.2.0.0");
    expect(mo2Bucket.newestVersion).toBe("1.2.0.0");
    expect(mo2Bucket.repository).toBe("Nexus");
    expect(mo2Bucket.nexusFileStatus).toBe(1);
    expect(mo2Bucket.nexusCategory).toBe(65);
  });

  it("does not overwrite an existing nexusId or installedVersion on the mod", () => {
    const base: ModInfo = {
      id: "A Dog's Life",
      name: "A Dog's Life",
      enabled: true,
      path: "C:/MO2/mods/A Dog's Life",
      plugins: [],
      nexusId: 12345,
      installedVersion: "9.9.9",
      metadata: {},
    };

    const meta = parseMo2MetaIni(exampleMetaIni) as Mo2Meta;
    const enriched = enrichModInfoWithMo2Meta(base, meta);

    expect(enriched.nexusId).toBe(12345);
    expect(enriched.installedVersion).toBe("9.9.9");
  });
});



