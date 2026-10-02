namespace NpiManagement.Domain.Exceptions;

/// <summary>
/// Base de las excepciones de dominio. Application las traduce a ProblemDetails (409 en la
/// mayoría de los casos — ver docs/api-contract.md). "Code" identifica la regla violada de
/// forma estable para pruebas y para el cliente (no depende del texto del mensaje).
/// </summary>
public abstract class DomainException : Exception
{
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Se lanza cuando una acción de workflow se solicita en un estado (LifecycleStatus/estado de
/// área) o sin una precondición de negocio que la acción exige — ver docs/workflow.md.
/// </summary>
public sealed class InvalidNpiTransitionException : DomainException
{
    public InvalidNpiTransitionException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>
/// Se lanza cuando los datos de entrada de una acción violan un invariante del agregado
/// (p. ej. falta un comentario o una evidencia obligatoria) — distinto de errores de formato
/// de DTO, que se validan en Application con FluentValidation antes de llegar al dominio.
/// </summary>
public sealed class NpiValidationException : DomainException
{
    public NpiValidationException(string code, string message) : base(code, message)
    {
    }
}
