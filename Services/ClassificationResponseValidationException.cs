namespace DevNote.Services;

public sealed class ClassificationResponseValidationException : Exception
{
    public ClassificationResponseValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
