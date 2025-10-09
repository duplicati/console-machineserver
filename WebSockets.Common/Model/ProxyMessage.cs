using System.Text.Json;

namespace WebSockets.Common.Model;

/// <summary>
/// A proxied message
/// </summary>
public sealed record ProxyMessage
{
    /// <summary>
    /// The type of the inner message
    /// </summary>
    public required string Type { get; init; }
    /// <summary>
    /// The original sender of the inner message
    /// </summary>
    public required string From { get; init; }
    /// <summary>
    /// The final recipient of the inner message
    /// </summary>
    public required string To { get; init; }
    /// <summary>
    /// The organization the message is within
    /// </summary>
    public required string OrganizationId { get; init; }
    /// <summary>
    /// The inner message being wrapped, serialized as JSON
    /// </summary>
    public required string? InnerMessage { get; init; }


    /// <summary>
    /// Deserializes the inner message to the specified type
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <returns>The deserialized message, or null if InnerMessage is null or whitespace</returns>
    public T? DeserializeInnerMessage<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(InnerMessage))
            return null;

        return JsonSerializer.Deserialize<T>(InnerMessage, EnvelopedMessage.JsonSerializerOptions);
    }
}
