namespace Sistema.Gestao.Empresarial.Domain.Common;

/// <summary>
/// Normalização canônica de chaves naturais de negócio (por exemplo, o título de uma
/// profissão ou o nome de um cargo) usada exclusivamente para detecção de duplicidade.
/// </summary>
/// <remarks>
/// A normalização remove apenas os espaços das extremidades. A insensibilidade a
/// caixa (maiúsculas/minúsculas) é responsabilidade da <c>collation</c> das colunas
/// de chave de negócio (<c>Latin1_General_CI_AS</c>), preservando a acentuação —
/// portanto "Médico" e "Medico" permanecem distintos. Centralizar aqui evita espalhar
/// <c>Trim</c>/<c>ToLower</c>/<c>ToUpper</c> pelos handlers.
/// </remarks>
public static class ChaveNegocio
{
    public static string Normalizar(string? valor) => valor?.Trim() ?? string.Empty;
}
