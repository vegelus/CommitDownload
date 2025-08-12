namespace CommitDownload.App.Exceptions;

public sealed class PersistenceException(string message, Exception? inner = null) : Exception(message, inner);