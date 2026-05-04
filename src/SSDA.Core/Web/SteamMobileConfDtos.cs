using System.Text.Json.Serialization;

namespace SSDA.Core.Web;

/// <summary>
/// Wire-format DTOs for the JSON returned by <c>steamcommunity.com/mobileconf/getlist</c>
/// since Steam moved the page to a React payload in 2023.
/// </summary>
internal sealed class GetListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("needauth")]
    public bool NeedAuth { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("conf")]
    public List<ConfirmationDto>? Conf { get; set; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed class ConfirmationDto
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("type_name")]
    public string? TypeName { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("creator_id")]
    public string? CreatorId { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("creation_time")]
    public long CreationTime { get; set; }

    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("summary")]
    public List<string>? Summary { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("multi")]
    public bool Multi { get; set; }
}
