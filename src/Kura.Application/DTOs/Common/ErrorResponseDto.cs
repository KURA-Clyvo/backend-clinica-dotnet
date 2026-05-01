namespace Kura.Application.DTOs.Common;

public sealed class ErrorResponseDto
{
    public string Title { get; init; } = string.Empty;
    public int Status { get; init; }
    public string Type { get; init; } = string.Empty;
}
