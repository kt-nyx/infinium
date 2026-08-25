using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class FindingReportJsonCodec
{
    public static byte[] Serialize(FindingReportDocument value) =>
        SchemaValidatedJsonCodec.Serialize(
            value,
            "finding-report.v1.schema.json",
            FindingReportContract.Validate);

    public static FindingReportDocument Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<FindingReportDocument>(
            bytes,
            "finding-report.v1.schema.json",
            FindingReportContract.Validate);
}
