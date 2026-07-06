namespace Ringfall.Core.Scenarios;

public sealed class InitialStateValidationException : Exception
{
    public InitialStateValidationException(string message)
        : base(message)
    {
    }

    public InitialStateValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
