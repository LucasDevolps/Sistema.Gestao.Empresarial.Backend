namespace Sistema.Gestao.Empresarial.Domain.Common;

/// <summary>
/// Sinaliza a tentativa de criar ou atualizar um registro cuja chave natural de
/// negócio já pertence a outro registro (por exemplo, duas profissões com o mesmo
/// título, desconsiderando caixa e espaços das extremidades).
/// </summary>
/// <remarks>
/// É tratada pelo handler global de exceções como <c>HTTP 409 Conflict</c>. A
/// <see cref="System.Exception.Message"/> é redigida para consumo do cliente e não
/// contém detalhes de infraestrutura (SQL, nome de constraint, stack trace).
/// </remarks>
public sealed class DuplicateBusinessKeyException(string message) : Exception(message);
