namespace StandFast.Application.Dtos;

/// <summary>A profile photo's bytes and media type, as uploaded in the person dialog or served to an image request.</summary>
public sealed record PersonPhotoDto(string ContentType, byte[] Content);
