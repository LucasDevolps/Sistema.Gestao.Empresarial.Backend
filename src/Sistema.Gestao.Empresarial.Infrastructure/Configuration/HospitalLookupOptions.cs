using System.ComponentModel.DataAnnotations;

namespace Sistema.Gestao.Empresarial.Infrastructure.Configuration;

public sealed class HospitalLookupOptions
{
    public const string SectionName = "HospitalLookups";
    [Required, Url] public string BrasilApiBaseUrl { get; set; } = "https://brasilapi.com.br/";
    [Required, Url] public string ViaCepBaseUrl { get; set; } = "https://viacep.com.br/";
    [Range(1, 30)] public int TimeoutSeconds { get; set; } = 5;
}
