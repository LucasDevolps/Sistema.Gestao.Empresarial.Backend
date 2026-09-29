using System.Text.Json.Serialization;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

[JsonConverter(typeof(JsonStringEnumConverter<TipoUnidadeHospitalar>))]
public enum TipoUnidadeHospitalar
{
    HospitalGeral = 1, HospitalEspecializado, HospitalDia, UnidadeProntoAtendimento,
    Clinica, Maternidade, Ambulatorio, CentroDiagnostico, Outro
}

[JsonConverter(typeof(JsonStringEnumConverter<NaturezaUnidadeHospitalar>))]
public enum NaturezaUnidadeHospitalar
{
    Publica = 1, Privada, Filantropica, Conveniada, Outra
}
