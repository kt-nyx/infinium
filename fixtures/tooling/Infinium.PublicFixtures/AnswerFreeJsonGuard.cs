using System.Text.Json;

namespace Infinium.PublicFixtures;

public static class AnswerFreeJsonGuard
{
    private static readonly string[] ForbiddenAnswerTokens =
        ["expected_", "oracle", "ground_truth", "matched_negative", "correct_answer"];

    public static void Validate(JsonElement element) => Validate(element, propertyName: null);

    private static void Validate(JsonElement element, string? propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (ForbiddenAnswerTokens.Any(token =>
                        property.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidDataException(
                            "Product-reachable input contains an answer-authority field.");
                    }
                    Validate(property.Value, property.Name);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Validate(item, propertyName);
                }
                break;
            case JsonValueKind.String when propertyName != "text":
                string value = element.GetString()!;
                if (ForbiddenAnswerTokens.Any(token =>
                    value.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException(
                        "Product-reachable input contains answer-authority data.");
                }
                break;
        }
    }
}
