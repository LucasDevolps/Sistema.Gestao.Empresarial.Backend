using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public DbSet<Organizacao> Organizacoes => Set<Organizacao>();
    public DbSet<UnidadeHospitalar> UnidadesHospitalares => Set<UnidadeHospitalar>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<Profissao> Profissoes => Set<Profissao>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<NivelProfissional> NiveisProfissionais => Set<NivelProfissional>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<FuncionarioUnidadeAtuacao> FuncionariosUnidadesAtuacao => Set<FuncionarioUnidadeAtuacao>();
    public DbSet<FuncionarioSetor> FuncionariosSetores => Set<FuncionarioSetor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioSessao> UsuariosSessoes => Set<UsuarioSessao>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<PerfilPermissao> PerfisPermissoes => Set<PerfilPermissao>();
    public DbSet<UsuarioPerfil> UsuariosPerfis => Set<UsuarioPerfil>();
    public DbSet<UsuarioPermissao> UsuariosPermissoes => Set<UsuarioPermissao>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<MessageAuditLog> MessageAuditLogs => Set<MessageAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sge");
        modelBuilder.HasSequence<long>("SequenciaMatriculaFuncionario", "sge")
            .StartsAt(1)
            .IncrementsBy(1);

        ConfigurarOrganizacao(modelBuilder.Entity<Organizacao>());
        ConfigurarUnidade(modelBuilder.Entity<UnidadeHospitalar>());
        ConfigurarSetor(modelBuilder.Entity<Setor>());
        ConfigurarProfissao(modelBuilder.Entity<Profissao>());
        ConfigurarCargo(modelBuilder.Entity<Cargo>());
        ConfigurarNivel(modelBuilder.Entity<NivelProfissional>());
        ConfigurarFuncionario(modelBuilder.Entity<Funcionario>(), Database.IsRelational());
        ConfigurarAtuacao(modelBuilder.Entity<FuncionarioUnidadeAtuacao>());
        ConfigurarFuncionarioSetor(modelBuilder.Entity<FuncionarioSetor>());
        ConfigurarUsuario(modelBuilder.Entity<Usuario>());
        ConfigurarUsuarioSessao(modelBuilder.Entity<UsuarioSessao>());
        ConfigurarPerfil(modelBuilder.Entity<Perfil>());
        ConfigurarPermissao(modelBuilder.Entity<Permissao>());
        ConfigurarPerfilPermissao(modelBuilder.Entity<PerfilPermissao>());
        ConfigurarUsuarioPerfil(modelBuilder.Entity<UsuarioPerfil>());
        ConfigurarUsuarioPermissao(modelBuilder.Entity<UsuarioPermissao>());
        ConfigurarAuditLog(modelBuilder.Entity<AuditLog>());
        ConfigurarApiRequestLog(modelBuilder.Entity<ApiRequestLog>());
        ConfigurarOutbox(modelBuilder.Entity<OutboxMessage>());
        ConfigurarInbox(modelBuilder.Entity<InboxMessage>());
        ConfigurarMessageAuditLog(modelBuilder.Entity<MessageAuditLog>());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(IEntidadeExcluivel).IsAssignableFrom(x.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var deleted = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(bool)],
                parameter,
                Expression.Constant(nameof(IEntidadeExcluivel.Excluido)));
            entityType.SetQueryFilter(Expression.Lambda(Expression.Not(deleted), parameter));
        }

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepararAlteracoes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepararAlteracoes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepararAlteracoes()
    {
        if (ChangeTracker.Entries().Any(x => x.State == EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Exclusão física é proibida. Use uma operação explícita de inativação ou exclusão lógica.");
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<EntidadeAuditavel>().Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.MarcarAtualizacao(now);
        }
    }

    private static void ConfigurarBase<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> builder)
        where T : EntidadeAuditavel
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.HasIndex(x => x.Guid).IsUnique();
        builder.Property(x => x.Guid).ValueGeneratedNever();
        builder.Property(x => x.DataCriacao).HasPrecision(0);
        builder.Property(x => x.DataAtualizacao).HasPrecision(0);
        builder.Property(x => x.ExcluidoEm).HasPrecision(0);
        builder.Property(x => x.Versao).IsRowVersion();
    }

    private static void ConfigurarOrganizacao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Organizacao> builder)
    {
        builder.ToTable("Organizacoes");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Nome).IsUnique().HasFilter("[Excluido] = 0");
    }

    private static void ConfigurarUnidade(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UnidadeHospitalar> builder)
    {
        builder.ToTable("UnidadesHospitalares");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.OrganizacaoId, x.Nome }).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.Organizacao).WithMany().HasForeignKey(x => x.OrganizacaoId);
    }

    private static void ConfigurarSetor(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Setor> builder)
    {
        builder.ToTable("Setores");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => new { x.UnidadeHospitalarId, x.Nome }).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.UnidadeHospitalar).WithMany().HasForeignKey(x => x.UnidadeHospitalarId);
    }

    private static void ConfigurarProfissao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Profissao> builder)
    {
        builder.ToTable("Profissoes");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.HasIndex(x => x.Nome).IsUnique().HasFilter("[Excluido] = 0");
    }

    private static void ConfigurarCargo(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("Cargos");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.HasIndex(x => x.Nome).IsUnique().HasFilter("[Excluido] = 0");
    }

    private static void ConfigurarNivel(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<NivelProfissional> builder)
    {
        builder.ToTable("NiveisProfissionais");
        ConfigurarBase(builder);
        builder.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Codigo).IsUnique().HasFilter("[Excluido] = 0");

        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            SeedNivel(1, "870D89D7-153A-46EB-93E4-A2E08E966D19", "JR", "Júnior", 1, seedDate),
            SeedNivel(2, "9D77BFD3-DC47-44E5-A4CA-B62497CD5864", "PL", "Pleno", 2, seedDate),
            SeedNivel(3, "E6E15AE5-FF9B-4A07-884A-5E66F805BFE0", "SR", "Sênior", 3, seedDate));
    }

    private static object SeedNivel(long id, string guid, string codigo, string nome, int ordem, DateTimeOffset date) =>
        new
        {
            Id = id,
            Guid = Guid.Parse(guid),
            Codigo = codigo,
            Nome = nome,
            Ordem = ordem,
            Ativo = true,
            Excluido = false,
            DataCriacao = date,
            DataAtualizacao = date,
            ExcluidoEm = (DateTimeOffset?)null,
            ExcluidoPor = (Guid?)null
        };

    private static void ConfigurarFuncionario(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Funcionario> builder,
        bool relationalDatabase)
    {
        builder.ToTable("Funcionarios");
        ConfigurarBase(builder);
        builder.Property(x => x.Matricula)
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('FUN', RIGHT(REPLICATE('0', 9) + CONVERT(varchar(20), NEXT VALUE FOR [sge].[SequenciaMatriculaFuncionario]), 9))")
            .ValueGeneratedOnAdd();
        if (!relationalDatabase)
        {
            // O provider InMemory não executa defaults SQL; a matrícula real continua sendo gerada pela SEQUENCE.
            builder.Property(x => x.Matricula).IsRequired(false);
        }
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(30);
        builder.Property(x => x.DataAdmissao).HasColumnType("date");
        builder.HasIndex(x => x.Matricula).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.Profissao).WithMany().HasForeignKey(x => x.ProfissaoId);
        builder.HasOne(x => x.Cargo).WithMany().HasForeignKey(x => x.CargoId);
        builder.HasOne(x => x.Nivel).WithMany().HasForeignKey(x => x.NivelId);
        builder.HasOne(x => x.UnidadeContratacao).WithMany().HasForeignKey(x => x.UnidadeContratacaoId);
    }

    private static void ConfigurarAtuacao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<FuncionarioUnidadeAtuacao> builder)
    {
        builder.ToTable("FuncionariosUnidadesAtuacao");
        ConfigurarBase(builder);
        builder.Property(x => x.DataInicio).HasColumnType("date");
        builder.Property(x => x.DataFim).HasColumnType("date");
        builder.HasIndex(x => new { x.FuncionarioId, x.UnidadeHospitalarId })
            .IsUnique()
            .HasFilter("[Ativo] = 1 AND [Excluido] = 0");
        builder.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId);
        builder.HasOne(x => x.UnidadeHospitalar).WithMany().HasForeignKey(x => x.UnidadeHospitalarId);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FuncionarioUnidadeAtuacao_Periodo",
            "[DataFim] IS NULL OR [DataFim] >= [DataInicio]"));
    }

    private static void ConfigurarFuncionarioSetor(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<FuncionarioSetor> builder)
    {
        builder.ToTable("FuncionariosSetores");
        ConfigurarBase(builder);
        builder.Property(x => x.DataInicio).HasColumnType("date");
        builder.Property(x => x.DataFim).HasColumnType("date");
        builder.HasIndex(x => new { x.FuncionarioId, x.SetorId })
            .IsUnique()
            .HasFilter("[Ativo] = 1 AND [Excluido] = 0");
        builder.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId);
        builder.HasOne(x => x.Setor).WithMany().HasForeignKey(x => x.SetorId);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FuncionarioSetor_Periodo",
            "[DataFim] IS NULL OR [DataFim] >= [DataInicio]"));
    }

    private static void ConfigurarUsuario(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        ConfigurarBase(builder);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.SenhaHash).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasIndex(x => x.FuncionarioId).IsUnique().HasFilter("[FuncionarioId] IS NOT NULL AND [Excluido] = 0");
        builder.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId);
    }

    private static void ConfigurarUsuarioSessao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UsuarioSessao> builder)
    {
        builder.ToTable("UsuariosSessoes");
        ConfigurarBase(builder);
        builder.Property(x => x.Jti).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AccessTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MotivoRevogacao).HasMaxLength(200);
        builder.Property(x => x.IpOrigem).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.UltimaAtividadeEm).HasPrecision(0);
        builder.Property(x => x.ExpiraEm).HasPrecision(0);
        builder.Property(x => x.DataRevogacao).HasPrecision(0);
        builder.HasIndex(x => x.SessionId).IsUnique();
        builder.HasIndex(x => x.Jti).IsUnique();
        builder.HasIndex(x => x.RefreshTokenHash).IsUnique();
        builder.HasIndex(x => x.UsuarioId)
            .IsUnique()
            .HasFilter("[Ativo] = 1 AND [Revogado] = 0 AND [Excluido] = 0");
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarPerfil(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfis");
        ConfigurarBase(builder);
        builder.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.HasIndex(x => x.Nome).IsUnique().HasFilter("[Excluido] = 0");
    }

    private static void ConfigurarPermissao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("Permissoes");
        ConfigurarBase(builder);
        builder.Property(x => x.Codigo).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.Codigo).IsUnique().HasFilter("[Excluido] = 0");

        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            SeedPermissao(1, "0EAF698D-1185-4A06-9B59-3B4D130DB4A9", "FUNCIONARIO_VISUALIZAR", "Visualizar funcionários", seedDate),
            SeedPermissao(2, "C79230E7-B0E0-4BD7-AD93-88637D55EC47", "FUNCIONARIO_CRIAR", "Criar funcionários", seedDate),
            SeedPermissao(3, "DD4F7BA8-63BE-44DD-9B9D-820FB79D8563", "FUNCIONARIO_EDITAR", "Editar funcionários", seedDate),
            SeedPermissao(4, "F5EE67BE-705C-4426-9584-92E03BF06728", "PROFISSAO_VISUALIZAR", "Visualizar profissões", seedDate),
            SeedPermissao(5, "28E904DC-9D46-4F76-A735-681F8335D381", "PROFISSAO_CRIAR", "Criar profissões", seedDate),
            SeedPermissao(6, "163F7EB6-A167-457A-9974-E1EA759380C9", "SETOR_VISUALIZAR", "Visualizar setores", seedDate),
            SeedPermissao(7, "A4292AA5-27F2-4525-AD76-DC84DC95F19C", "SETOR_EDITAR", "Editar setores", seedDate),
            SeedPermissao(8, "F47ED124-E9A8-4373-90C6-BA98E450AD5F", "USUARIO_GERENCIAR_PERMISSOES", "Gerenciar permissões de usuários", seedDate),
            SeedPermissao(9, "659C28FD-A7D5-489E-BD32-C809601EE3F0", "PROFISSAO_EDITAR", "Editar profissões", seedDate),
            SeedPermissao(10, "B21B733D-30F3-4F28-8477-D5CE7517AE96", "CARGO_VISUALIZAR", "Visualizar cargos", seedDate),
            SeedPermissao(11, "80A43A7A-4615-49C5-BBA8-08488439C687", "CARGO_CRIAR", "Criar cargos", seedDate),
            SeedPermissao(12, "F45D589B-3234-4334-8CB7-15C4CA1AE245", "CARGO_EDITAR", "Editar cargos", seedDate),
            SeedPermissao(13, "8F611DFC-3AF0-4FC0-9A97-EE76DE6E846D", "NIVEL_PROFISSIONAL_VISUALIZAR", "Visualizar níveis profissionais", seedDate));
    }

    private static object SeedPermissao(long id, string guid, string codigo, string descricao, DateTimeOffset date) =>
        new
        {
            Id = id,
            Guid = Guid.Parse(guid),
            Codigo = codigo,
            Descricao = descricao,
            Ativo = true,
            Excluido = false,
            DataCriacao = date,
            DataAtualizacao = date,
            ExcluidoEm = (DateTimeOffset?)null,
            ExcluidoPor = (Guid?)null
        };

    private static void ConfigurarPerfilPermissao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PerfilPermissao> builder)
    {
        builder.ToTable("PerfisPermissoes");
        ConfigurarBase(builder);
        builder.HasIndex(x => new { x.PerfilId, x.PermissaoId }).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.Perfil).WithMany().HasForeignKey(x => x.PerfilId);
        builder.HasOne(x => x.Permissao).WithMany().HasForeignKey(x => x.PermissaoId);
    }

    private static void ConfigurarUsuarioPerfil(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UsuarioPerfil> builder)
    {
        builder.ToTable("UsuariosPerfis");
        ConfigurarBase(builder);
        builder.HasIndex(x => new { x.UsuarioId, x.PerfilId }).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
        builder.HasOne(x => x.Perfil).WithMany().HasForeignKey(x => x.PerfilId);
    }

    private static void ConfigurarUsuarioPermissao(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UsuarioPermissao> builder)
    {
        builder.ToTable("UsuariosPermissoes");
        ConfigurarBase(builder);
        builder.HasIndex(x => new { x.UsuarioId, x.PermissaoId }).IsUnique().HasFilter("[Excluido] = 0");
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
        builder.HasOne(x => x.Permissao).WithMany().HasForeignKey(x => x.PermissaoId);
    }

    private static void ConfigurarAuditLog(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        ConfigurarBase(builder);
        builder.Property(x => x.Entidade).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Acao).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ValorAnterior).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ValorNovo).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Ip).HasMaxLength(64);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.Entidade, x.EntidadeGuid });
    }

    private static void ConfigurarApiRequestLog(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ApiRequestLog> builder)
    {
        builder.ToTable("ApiRequestLogs");
        ConfigurarBase(builder);
        builder.Property(x => x.DataHoraInicio).HasPrecision(3);
        builder.Property(x => x.DataHoraFim).HasPrecision(3);
        builder.Property(x => x.MetodoHttp).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.QueryString).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RequestHeaders).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ResponseHeaders).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IpOrigem).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.Ambiente).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Exception).HasMaxLength(1000);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.TraceId);
        builder.HasIndex(x => x.DataHoraInicio);
        builder.HasIndex(x => new { x.UsuarioId, x.DataHoraInicio });
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarOutbox(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        ConfigurarBase(builder);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Producer).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.WorkerId).HasMaxLength(200);
        builder.Property(x => x.Erro).HasColumnType("nvarchar(max)");
        builder.Property(x => x.UltimaTentativaEm).HasPrecision(3);
        builder.Property(x => x.ProximaTentativaEm).HasPrecision(3);
        builder.Property(x => x.BloqueadoAte).HasPrecision(3);
        builder.HasIndex(x => x.MessageId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ProximaTentativaEm, x.BloqueadoAte, x.OccurredAt });
        builder.HasIndex(x => x.CorrelationId);
    }

    private static void ConfigurarInbox(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        ConfigurarBase(builder);
        builder.Property(x => x.Consumer).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Resultado).HasMaxLength(2000);
        builder.Property(x => x.Erro).HasMaxLength(4000);
        builder.Property(x => x.RecebidoEm).HasPrecision(3);
        builder.Property(x => x.ProcessadoEm).HasPrecision(3);
        builder.Property(x => x.UltimaTentativaEm).HasPrecision(3);
        builder.HasIndex(x => new { x.MessageId, x.Consumer }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.RecebidoEm });
        builder.HasIndex(x => x.CorrelationId);
    }

    private static void ConfigurarMessageAuditLog(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MessageAuditLog> builder)
    {
        builder.ToTable("MessageAuditLogs");
        ConfigurarBase(builder);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Consumer).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Detalhe).HasMaxLength(4000);
        builder.Property(x => x.OcorridoEm).HasPrecision(3);
        builder.HasIndex(x => new { x.MessageId, x.Consumer, x.Tentativa });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.OcorridoEm);
    }
}
