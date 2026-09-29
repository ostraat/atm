namespace Atm.Domain;

public abstract class AtmException : Exception
{
    protected AtmException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : AtmException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ValidationException : AtmException
{
    public ValidationException(string message) : base(message)
    {
    }
}

public sealed class InsufficientFundsException : AtmException
{
    public InsufficientFundsException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : AtmException
{
    public ConflictException(string message) : base(message)
    {
    }
}
