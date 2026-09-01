using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260901142106_ConfigurableAuthorizationAndHttpAudit")]
public class ConfigurableAuthorizationAndHttpAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("VersaoPermissoes", "Usuarios", "bigint", null, null, rowVersion: false, "sge", nullable: false, 0L);
        migrationBuilder.CreateTable("ApiRequestLogs", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<long>("bigint").Annotation("SqlServer:Identity", "1, 1");
            int? precision = 3;
            OperationBuilder<AddColumnOperation> dataHoraInicio = table.Column<DateTimeOffset>("datetimeoffset(3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision);
            precision = 3;
            OperationBuilder<AddColumnOperation> dataHoraFim = table.Column<DateTimeOffset>("datetimeoffset(3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision);
            precision = 16;
            OperationBuilder<AddColumnOperation> metodoHttp = table.Column<string>("nvarchar(16)", null, precision);
            precision = 2048;
            OperationBuilder<AddColumnOperation> endpoint = table.Column<string>("nvarchar(2048)", null, precision);
            OperationBuilder<AddColumnOperation> queryString = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> requestHeaders = table.Column<string>("nvarchar(max)");
            OperationBuilder<AddColumnOperation> requestBody = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> responseHeaders = table.Column<string>("nvarchar(max)");
            OperationBuilder<AddColumnOperation> responseBody = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> statusCode = table.Column<int>("int");
            OperationBuilder<AddColumnOperation> tempoExecucaoMs = table.Column<long>("bigint");
            OperationBuilder<AddColumnOperation> sucesso = table.Column<bool>("bit");
            OperationBuilder<AddColumnOperation> correlationId = table.Column<Guid>("uniqueidentifier");
            precision = 64;
            OperationBuilder<AddColumnOperation> traceId = table.Column<string>("nvarchar(64)", null, precision);
            precision = 64;
            OperationBuilder<AddColumnOperation> ipOrigem = table.Column<string>("nvarchar(64)", null, precision, rowVersion: false, null, nullable: true);
            precision = 1000;
            OperationBuilder<AddColumnOperation> userAgent = table.Column<string>("nvarchar(1000)", null, precision, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> usuarioId = table.Column<long>("bigint", null, null, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> usuarioGuid = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true);
            precision = 100;
            OperationBuilder<AddColumnOperation> ambiente = table.Column<string>("nvarchar(100)", null, precision);
            precision = 1000;
            OperationBuilder<AddColumnOperation> exception = table.Column<string>("nvarchar(1000)", null, precision, rowVersion: false, null, nullable: true);
            OperationBuilder<AddColumnOperation> guid = table.Column<Guid>("uniqueidentifier");
            OperationBuilder<AddColumnOperation> ativo = table.Column<bool>("bit");
            OperationBuilder<AddColumnOperation> excluido = table.Column<bool>("bit");
            precision = 0;
            OperationBuilder<AddColumnOperation> dataCriacao = table.Column<DateTimeOffset>("datetimeoffset(0)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision);
            precision = 0;
            OperationBuilder<AddColumnOperation> dataAtualizacao = table.Column<DateTimeOffset>("datetimeoffset(0)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision);
            precision = 0;
            return new
            {
                Id = id,
                DataHoraInicio = dataHoraInicio,
                DataHoraFim = dataHoraFim,
                MetodoHttp = metodoHttp,
                Endpoint = endpoint,
                QueryString = queryString,
                RequestHeaders = requestHeaders,
                RequestBody = requestBody,
                ResponseHeaders = responseHeaders,
                ResponseBody = responseBody,
                StatusCode = statusCode,
                TempoExecucaoMs = tempoExecucaoMs,
                Sucesso = sucesso,
                CorrelationId = correlationId,
                TraceId = traceId,
                IpOrigem = ipOrigem,
                UserAgent = userAgent,
                UsuarioId = usuarioId,
                UsuarioGuid = usuarioGuid,
                Ambiente = ambiente,
                Exception = exception,
                Guid = guid,
                Ativo = ativo,
                Excluido = excluido,
                DataCriacao = dataCriacao,
                DataAtualizacao = dataAtualizacao,
                ExcluidoEm = table.Column<DateTimeOffset>("datetimeoffset(0)", null, null, rowVersion: false, null, nullable: true, null, null, null, null, null, null, precision),
                ExcluidoPor = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true),
                Versao = table.Column<byte[]>("rowversion", null, null, rowVersion: true)
            };
        }, "sge", table =>
        {
            table.PrimaryKey("PK_ApiRequestLogs", x => x.Id);
            table.ForeignKey("FK_ApiRequestLogs_Usuarios_UsuarioId", x => x.UsuarioId, "Usuarios", "Id", "sge", ReferentialAction.NoAction, ReferentialAction.Restrict);
        });
        migrationBuilder.InsertData("Permissoes", new string[10] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" }, new object[8, 10]
        {
            {
                1L,
                true,
                "FUNCIONARIO_VISUALIZAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Visualizar funcionários",
                false,
                null,
                null,
                new Guid("0eaf698d-1185-4a06-9b59-3b4d130db4a9")
            },
            {
                2L,
                true,
                "FUNCIONARIO_CRIAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Criar funcionários",
                false,
                null,
                null,
                new Guid("c79230e7-b0e0-4bd7-ad93-88637d55ec47")
            },
            {
                3L,
                true,
                "FUNCIONARIO_EDITAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Editar funcionários",
                false,
                null,
                null,
                new Guid("dd4f7ba8-63be-44dd-9b9d-820fb79d8563")
            },
            {
                4L,
                true,
                "PROFISSAO_VISUALIZAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Visualizar profissões",
                false,
                null,
                null,
                new Guid("f5ee67be-705c-4426-9584-92e03bf06728")
            },
            {
                5L,
                true,
                "PROFISSAO_CRIAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Criar profissões",
                false,
                null,
                null,
                new Guid("28e904dc-9d46-4f76-a735-681f8335d381")
            },
            {
                6L,
                true,
                "SETOR_VISUALIZAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Visualizar setores",
                false,
                null,
                null,
                new Guid("163f7eb6-a167-457a-9974-e1ea759380c9")
            },
            {
                7L,
                true,
                "SETOR_EDITAR",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Editar setores",
                false,
                null,
                null,
                new Guid("a4292aa5-27f2-4525-ad76-dc84dc95f19c")
            },
            {
                8L,
                true,
                "USUARIO_GERENCIAR_PERMISSOES",
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                "Gerenciar permissões de usuários",
                false,
                null,
                null,
                new Guid("f47ed124-e9a8-4373-90c6-ba98e450ad5f")
            }
        }, "sge");
        migrationBuilder.CreateIndex("IX_ApiRequestLogs_CorrelationId", "ApiRequestLogs", "CorrelationId", "sge");
        migrationBuilder.CreateIndex("IX_ApiRequestLogs_DataHoraInicio", "ApiRequestLogs", "DataHoraInicio", "sge");
        migrationBuilder.CreateIndex("IX_ApiRequestLogs_Guid", "ApiRequestLogs", "Guid", "sge", unique: true);
        migrationBuilder.CreateIndex("IX_ApiRequestLogs_TraceId", "ApiRequestLogs", "TraceId", "sge");
        migrationBuilder.CreateIndex("IX_ApiRequestLogs_UsuarioId_DataHoraInicio", "ApiRequestLogs", new string[2] { "UsuarioId", "DataHoraInicio" }, "sge");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Rollback bloqueado: dados de segurança e auditoria não podem ser fisicamente excluídos.");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sge").HasAnnotation("ProductVersion", "10.0.11").HasAnnotation("Relational:MaxIdentifierLength", 128);
        modelBuilder.UseIdentityColumns(1L);
        modelBuilder.HasSequence("SequenciaMatriculaFuncionario", "sge");
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Auditoria.ApiRequestLog", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<string>("Ambiente").IsRequired().HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<Guid>("CorrelationId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataHoraFim").HasPrecision(3).HasColumnType("datetimeoffset(3)");
            b.Property<DateTimeOffset>("DataHoraInicio").HasPrecision(3).HasColumnType("datetimeoffset(3)");
            b.Property<string>("Endpoint").IsRequired().HasMaxLength(2048)
                .HasColumnType("nvarchar(2048)");
            b.Property<string>("Exception").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("IpOrigem").HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("MetodoHttp").IsRequired().HasMaxLength(16)
                .HasColumnType("nvarchar(16)");
            b.Property<string>("QueryString").HasColumnType("nvarchar(max)");
            b.Property<string>("RequestBody").HasColumnType("nvarchar(max)");
            b.Property<string>("RequestHeaders").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("ResponseBody").HasColumnType("nvarchar(max)");
            b.Property<string>("ResponseHeaders").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<int>("StatusCode").HasColumnType("int");
            b.Property<bool>("Sucesso").HasColumnType("bit");
            b.Property<long>("TempoExecucaoMs").HasColumnType("bigint");
            b.Property<string>("TraceId").IsRequired().HasMaxLength(64)
                .HasColumnType("nvarchar(64)");
            b.Property<string>("UserAgent").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<Guid?>("UsuarioGuid").HasColumnType("uniqueidentifier");
            b.Property<long?>("UsuarioId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("CorrelationId");
            b.HasIndex("DataHoraInicio");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("TraceId");
            b.HasIndex("UsuarioId", "DataHoraInicio");
            b.ToTable("ApiRequestLogs", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Auditoria.AuditLog", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<string>("Acao").IsRequired().HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<Guid>("CorrelationId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataHora").HasColumnType("datetimeoffset");
            b.Property<string>("Entidade").IsRequired().HasMaxLength(150)
                .HasColumnType("nvarchar(150)");
            b.Property<Guid?>("EntidadeGuid").HasColumnType("uniqueidentifier");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Ip").HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("TraceId").IsRequired().HasMaxLength(64)
                .HasColumnType("nvarchar(64)");
            b.Property<Guid?>("UsuarioGuid").HasColumnType("uniqueidentifier");
            b.Property<string>("ValorAnterior").HasColumnType("nvarchar(max)");
            b.Property<string>("ValorNovo").HasColumnType("nvarchar(max)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("CorrelationId");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Entidade", "EntidadeGuid");
            b.ToTable("AuditLogs", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Integracao.OutboxMessage", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<Guid>("CorrelationId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Erro").HasColumnType("nvarchar(max)");
            b.Property<Guid>("EventId").HasColumnType("uniqueidentifier");
            b.Property<string>("EventType").IsRequired().HasMaxLength(200)
                .HasColumnType("nvarchar(200)");
            b.Property<int>("EventVersion").HasColumnType("int");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<Guid>("MessageId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("OccurredAt").HasColumnType("datetimeoffset");
            b.Property<string>("Payload").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("Producer").IsRequired().HasMaxLength(200)
                .HasColumnType("nvarchar(200)");
            b.Property<DateTimeOffset?>("PublicadoEm").HasColumnType("datetimeoffset");
            b.Property<string>("Status").IsRequired().HasMaxLength(50)
                .HasColumnType("nvarchar(50)");
            b.Property<int>("Tentativas").HasColumnType("int");
            b.Property<string>("TraceId").IsRequired().HasMaxLength(64)
                .HasColumnType("nvarchar(64)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("CorrelationId");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("MessageId").IsUnique();
            b.HasIndex("Status", "OccurredAt");
            b.ToTable("OutboxMessages", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Organizacoes.Organizacao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(200)
                .HasColumnType("nvarchar(200)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("Organizacoes", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Organizacoes.Setor", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(150)
                .HasColumnType("nvarchar(150)");
            b.Property<long>("UnidadeHospitalarId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("UnidadeHospitalarId", "Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("Setores", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Organizacoes.UnidadeHospitalar", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(200)
                .HasColumnType("nvarchar(200)");
            b.Property<long>("OrganizacaoId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("OrganizacaoId", "Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("UnidadesHospitalares", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.Cargo", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Descricao").HasMaxLength(500).HasColumnType("nvarchar(500)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(150)
                .HasColumnType("nvarchar(150)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("Cargos", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<long>("CargoId").HasColumnType("bigint");
            b.Property<DateOnly>("DataAdmissao").HasColumnType("date");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Email").IsRequired().HasMaxLength(254)
                .HasColumnType("nvarchar(254)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Matricula").IsRequired().ValueGeneratedOnAdd()
                .HasMaxLength(20)
                .HasColumnType("nvarchar(20)")
                .HasDefaultValueSql("CONCAT('FUN', RIGHT(REPLICATE('0', 9) + CONVERT(varchar(20), NEXT VALUE FOR [sge].[SequenciaMatriculaFuncionario]), 9))");
            b.Property<long>("NivelId").HasColumnType("bigint");
            b.Property<string>("Nome").IsRequired().HasMaxLength(200)
                .HasColumnType("nvarchar(200)");
            b.Property<long>("ProfissaoId").HasColumnType("bigint");
            b.Property<string>("Telefone").HasMaxLength(30).HasColumnType("nvarchar(30)");
            b.Property<long>("UnidadeContratacaoId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("CargoId");
            b.HasIndex("Email").HasFilter("[Excluido] = 0");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Matricula").IsUnique();
            b.HasIndex("NivelId");
            b.HasIndex("ProfissaoId");
            b.HasIndex("UnidadeContratacaoId");
            b.ToTable("Funcionarios", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.FuncionarioSetor", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateOnly?>("DataFim").HasColumnType("date");
            b.Property<DateOnly>("DataInicio").HasColumnType("date");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<long>("FuncionarioId").HasColumnType("bigint");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<long>("SetorId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("SetorId");
            b.HasIndex("FuncionarioId", "SetorId", "Ativo").HasFilter("[Excluido] = 0");
            b.ToTable("FuncionariosSetores", "sge", delegate (TableBuilder t)
            {
                t.HasCheckConstraint("CK_FuncionarioSetor_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
            });
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.FuncionarioUnidadeAtuacao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateOnly?>("DataFim").HasColumnType("date");
            b.Property<DateOnly>("DataInicio").HasColumnType("date");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<long>("FuncionarioId").HasColumnType("bigint");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<long>("UnidadeHospitalarId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("UnidadeHospitalarId");
            b.HasIndex("FuncionarioId", "UnidadeHospitalarId", "Ativo").HasFilter("[Excluido] = 0");
            b.ToTable("FuncionariosUnidadesAtuacao", "sge", delegate (TableBuilder t)
            {
                t.HasCheckConstraint("CK_FuncionarioUnidadeAtuacao_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
            });
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.NivelProfissional", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<string>("Codigo").IsRequired().HasMaxLength(10)
                .HasColumnType("nvarchar(10)");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(80)
                .HasColumnType("nvarchar(80)");
            b.Property<int>("Ordem").HasColumnType("int");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Codigo").IsUnique().HasFilter("[Excluido] = 0");
            b.HasIndex("Guid").IsUnique();
            b.ToTable("NiveisProfissionais", "sge");
            b.HasData(new
            {
                Id = 1L,
                Ativo = true,
                Codigo = "JR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Excluido = false,
                Guid = new Guid("870d89d7-153a-46eb-93e4-a2e08e966d19"),
                Nome = "Júnior",
                Ordem = 1
            }, new
            {
                Id = 2L,
                Ativo = true,
                Codigo = "PL",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Excluido = false,
                Guid = new Guid("9d77bfd3-dc47-44e5-a4ca-b62497cd5864"),
                Nome = "Pleno",
                Ordem = 2
            }, new
            {
                Id = 3L,
                Ativo = true,
                Codigo = "SR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Excluido = false,
                Guid = new Guid("e6e15ae5-ff9b-4a07-884a-5e66f805bfe0"),
                Nome = "Sênior",
                Ordem = 3
            });
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.Profissao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Descricao").HasMaxLength(500).HasColumnType("nvarchar(500)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(150)
                .HasColumnType("nvarchar(150)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("Profissoes", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.Perfil", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Descricao").HasMaxLength(500).HasColumnType("nvarchar(500)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("Nome").IsRequired().HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Nome").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("Perfis", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.PerfilPermissao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<long>("PerfilId").HasColumnType("bigint");
            b.Property<long>("PermissaoId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("PermissaoId");
            b.HasIndex("PerfilId", "PermissaoId").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("PerfisPermissoes", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.Permissao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<string>("Codigo").IsRequired().HasMaxLength(150)
                .HasColumnType("nvarchar(150)");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("Descricao").IsRequired().HasMaxLength(300)
                .HasColumnType("nvarchar(300)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Codigo").IsUnique().HasFilter("[Excluido] = 0");
            b.HasIndex("Guid").IsUnique();
            b.ToTable("Permissoes", "sge");
            b.HasData(new
            {
                Id = 1L,
                Ativo = true,
                Codigo = "FUNCIONARIO_VISUALIZAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Visualizar funcionários",
                Excluido = false,
                Guid = new Guid("0eaf698d-1185-4a06-9b59-3b4d130db4a9")
            }, new
            {
                Id = 2L,
                Ativo = true,
                Codigo = "FUNCIONARIO_CRIAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Criar funcionários",
                Excluido = false,
                Guid = new Guid("c79230e7-b0e0-4bd7-ad93-88637d55ec47")
            }, new
            {
                Id = 3L,
                Ativo = true,
                Codigo = "FUNCIONARIO_EDITAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Editar funcionários",
                Excluido = false,
                Guid = new Guid("dd4f7ba8-63be-44dd-9b9d-820fb79d8563")
            }, new
            {
                Id = 4L,
                Ativo = true,
                Codigo = "PROFISSAO_VISUALIZAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Visualizar profissões",
                Excluido = false,
                Guid = new Guid("f5ee67be-705c-4426-9584-92e03bf06728")
            }, new
            {
                Id = 5L,
                Ativo = true,
                Codigo = "PROFISSAO_CRIAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Criar profissões",
                Excluido = false,
                Guid = new Guid("28e904dc-9d46-4f76-a735-681f8335d381")
            }, new
            {
                Id = 6L,
                Ativo = true,
                Codigo = "SETOR_VISUALIZAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Visualizar setores",
                Excluido = false,
                Guid = new Guid("163f7eb6-a167-457a-9974-e1ea759380c9")
            }, new
            {
                Id = 7L,
                Ativo = true,
                Codigo = "SETOR_EDITAR",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Editar setores",
                Excluido = false,
                Guid = new Guid("a4292aa5-27f2-4525-ad76-dc84dc95f19c")
            }, new
            {
                Id = 8L,
                Ativo = true,
                Codigo = "USUARIO_GERENCIAR_PERMISSOES",
                DataAtualizacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                DataCriacao = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                Descricao = "Gerenciar permissões de usuários",
                Excluido = false,
                Guid = new Guid("f47ed124-e9a8-4373-90c6-ba98e450ad5f")
            });
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<bool>("Bloqueado").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset?>("DataUltimoLogin").HasColumnType("datetimeoffset");
            b.Property<string>("Email").IsRequired().HasMaxLength(254)
                .HasColumnType("nvarchar(254)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<long?>("FuncionarioId").HasColumnType("bigint");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("SenhaHash").IsRequired().HasMaxLength(1000)
                .HasColumnType("nvarchar(1000)");
            b.Property<int>("TentativasLoginInvalidas").HasColumnType("int");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.Property<long>("VersaoPermissoes").HasColumnType("bigint");
            b.Property<long>("VersaoSessao").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("Email").IsUnique().HasFilter("[Excluido] = 0");
            b.HasIndex("FuncionarioId").IsUnique().HasFilter("[FuncionarioId] IS NOT NULL AND [Excluido] = 0");
            b.HasIndex("Guid").IsUnique();
            b.ToTable("Usuarios", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioPerfil", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<long>("PerfilId").HasColumnType("bigint");
            b.Property<long>("UsuarioId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("PerfilId");
            b.HasIndex("UsuarioId", "PerfilId").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("UsuariosPerfis", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioPermissao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<bool>("Concedida").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<long>("PermissaoId").HasColumnType("bigint");
            b.Property<long>("UsuarioId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("PermissaoId");
            b.HasIndex("UsuarioId", "PermissaoId").IsUnique().HasFilter("[Excluido] = 0");
            b.ToTable("UsuariosPermissoes", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioSessao", delegate (EntityTypeBuilder b)
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
            b.Property<long>("Id").UseIdentityColumn(1L);
            b.Property<string>("AccessTokenHash").IsRequired().HasMaxLength(128)
                .HasColumnType("nvarchar(128)");
            b.Property<bool>("Ativo").HasColumnType("bit");
            b.Property<DateTimeOffset>("DataAtualizacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset>("DataCriacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<DateTimeOffset?>("DataRevogacao").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<bool>("Excluido").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ExcluidoEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid?>("ExcluidoPor").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("ExpiraEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<Guid>("Guid").HasColumnType("uniqueidentifier");
            b.Property<string>("IpOrigem").HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("Jti").IsRequired().HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
            b.Property<string>("MotivoRevogacao").HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<string>("RefreshTokenHash").IsRequired().HasMaxLength(128)
                .HasColumnType("nvarchar(128)");
            b.Property<bool>("Revogado").HasColumnType("bit");
            b.Property<Guid>("SessionId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("UltimaAtividadeEm").HasPrecision(0).HasColumnType("datetimeoffset(0)");
            b.Property<string>("UserAgent").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<long>("UsuarioId").HasColumnType("bigint");
            b.Property<byte[]>("Versao").IsConcurrencyToken().IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");
            b.Property<long>("VersaoSessao").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("Guid").IsUnique();
            b.HasIndex("Jti").IsUnique();
            b.HasIndex("RefreshTokenHash").IsUnique();
            b.HasIndex("SessionId").IsUnique();
            b.HasIndex("UsuarioId").IsUnique().HasFilter("[Ativo] = 1 AND [Revogado] = 0 AND [Excluido] = 0");
            b.ToTable("UsuariosSessoes", "sge");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Auditoria.ApiRequestLog", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", null).WithMany().HasForeignKey("UsuarioId")
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Organizacoes.Setor", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Organizacoes.UnidadeHospitalar", "UnidadeHospitalar").WithMany().HasForeignKey("UnidadeHospitalarId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("UnidadeHospitalar");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Organizacoes.UnidadeHospitalar", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Organizacoes.Organizacao", "Organizacao").WithMany().HasForeignKey("OrganizacaoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Organizacao");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.Cargo", "Cargo").WithMany().HasForeignKey("CargoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.NivelProfissional", "Nivel").WithMany().HasForeignKey("NivelId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.Profissao", "Profissao").WithMany().HasForeignKey("ProfissaoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Organizacoes.UnidadeHospitalar", "UnidadeContratacao").WithMany().HasForeignKey("UnidadeContratacaoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Cargo");
            b.Navigation("Nivel");
            b.Navigation("Profissao");
            b.Navigation("UnidadeContratacao");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.FuncionarioSetor", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario", "Funcionario").WithMany().HasForeignKey("FuncionarioId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Organizacoes.Setor", "Setor").WithMany().HasForeignKey("SetorId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Funcionario");
            b.Navigation("Setor");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Pessoas.FuncionarioUnidadeAtuacao", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario", "Funcionario").WithMany().HasForeignKey("FuncionarioId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Organizacoes.UnidadeHospitalar", "UnidadeHospitalar").WithMany().HasForeignKey("UnidadeHospitalarId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Funcionario");
            b.Navigation("UnidadeHospitalar");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.PerfilPermissao", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Perfil", "Perfil").WithMany().HasForeignKey("PerfilId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Permissao", "Permissao").WithMany().HasForeignKey("PermissaoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Perfil");
            b.Navigation("Permissao");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario", "Funcionario").WithMany().HasForeignKey("FuncionarioId")
                .OnDelete(DeleteBehavior.Restrict);
            b.Navigation("Funcionario");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioPerfil", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Perfil", "Perfil").WithMany().HasForeignKey("PerfilId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", "Usuario").WithMany().HasForeignKey("UsuarioId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Perfil");
            b.Navigation("Usuario");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioPermissao", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Permissao", "Permissao").WithMany().HasForeignKey("PermissaoId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", "Usuario").WithMany().HasForeignKey("UsuarioId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Permissao");
            b.Navigation("Usuario");
        });
        modelBuilder.Entity("Sistema.Gestao.Empresarial.Domain.Seguranca.UsuarioSessao", delegate (EntityTypeBuilder b)
        {
            b.HasOne("Sistema.Gestao.Empresarial.Domain.Seguranca.Usuario", "Usuario").WithMany().HasForeignKey("UsuarioId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Usuario");
        });
    }
}
