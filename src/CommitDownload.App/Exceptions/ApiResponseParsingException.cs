namespace CommitDownload.App.Exceptions;

public sealed class ApiResponseParsingException(string message, string? snippet = null, Exception? inner = null)
    : Exception(message, inner)
{
    public string? ResponseSnippet { get; } = snippet;
}