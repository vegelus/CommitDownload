namespace CommitDownload.App.Exceptions;

public sealed class RateLimitExceededException(string message, DateTimeOffset? resetAt = null, Exception? inner = null)
    : Exception(message, inner)
{
    public DateTimeOffset? ResetAt { get; } = resetAt;
}