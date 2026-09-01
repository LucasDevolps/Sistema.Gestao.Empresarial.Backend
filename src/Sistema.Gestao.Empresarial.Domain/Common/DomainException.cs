namespace Sistema.Gestao.Empresarial.Domain.Common;

public sealed class DomainException(string message) : Exception(message);
