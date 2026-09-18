using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandFast.Application.Backup;

/// <summary>Reads and writes the backup file. The one place the JSON settings live, so a file written by one release is read the same way by the next.</summary>
public static class BackupSerializer
{
    /// <summary>Indented because a backup is something a person opens to check, and enum names rather than numbers for the same reason.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Serialize(BackupDocument document) => JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);

    public static async Task<BackupDocument> DeserializeAsync(Stream content, CancellationToken cancellationToken = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<BackupDocument>(content, SerializerOptions, cancellationToken)
                ?? throw new BackupFormatException("The file is empty.");
        }
        catch (JsonException exception)
        {
            throw new BackupFormatException("The file is not a StandFast backup, or it has been edited into something that cannot be read.", exception);
        }
    }
}
