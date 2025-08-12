namespace CommitDownload.App.Exceptions;

public sealed class ApiRequestException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public ApiRequestException(string message, System.Net.HttpStatusCode statusCode, string? body = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }
}