using System.ComponentModel.DataAnnotations;

namespace Sistema.Gestao.Empresarial.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 30)]
    public int AccessTokenLifetimeMinutes { get; init; } = 10;
}

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    [Range(30, 30)]
    public int InactivityTimeoutMinutes { get; init; } = 30;

    [Range(10, 300)]
    public int ActivityPersistenceIntervalSeconds { get; init; } = 60;

    public bool EnableSqlFallback { get; init; } = true;

    [Range(1, 90)]
    public int AbsoluteLifetimeDays { get; init; } = 7;

    [Range(3, 20)]
    public int MaximumFailedLoginAttempts { get; init; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; init; } = 15;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string Configuration { get; init; } = string.Empty;

    [Required]
    public string InstanceName { get; init; } = "sge";

    [Range(1, 30)]
    public int ConnectTimeoutSeconds { get; init; } = 5;
}

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    [Range(10, 3600)]
    public int PermissionTtlSeconds { get; init; } = 120;
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required]
    public string VirtualHost { get; init; } = "/";

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 500)]
    public int BatchSize { get; init; } = 50;

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; init; } = 5;

    [Range(10, 600)]
    public int LeaseSeconds { get; init; } = 60;

    [Range(5, 3600)]
    public int MaximumRetryDelaySeconds { get; init; } = 300;
}

public sealed class InboxOptions
{
    public const string SectionName = "Inbox";

    [Range(1, 20)]
    public int TransientRetryCount { get; init; } = 5;

    [Range(1, 300)]
    public int InitialRetryDelaySeconds { get; init; } = 2;

    [Range(5, 3600)]
    public int MaximumRetryDelaySeconds { get; init; } = 60;

    [Range(1, 256)]
    public int ConcurrentMessageLimit { get; init; } = 16;

    [Required, MaxLength(200)]
    public string QueueName { get; init; } = "sge-integration-events-v1";

    [Required, MaxLength(200)]
    public string ConsumerName { get; init; } = "IntegrationEventConsumer";
}

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    [Range(1, 30)]
    public int PersistenceTimeoutSeconds { get; init; } = 5;

    [Range(100, 100000)]
    public int ChannelCapacity { get; init; } = 5000;
}

public sealed class AuditRetentionOptions
{
    public const string SectionName = "AuditRetention";

    public bool Enabled { get; init; } = true;

    [Range(180, 3650)]
    public int HttpAccessLogDays { get; init; } = 180;

    [Range(365, 7300)]
    public int BusinessAuditDays { get; init; } = 1825;

    [Range(100, 1000)]
    public int BatchSize { get; init; } = 500;

    [Range(1, 168)]
    public int SweepIntervalHours { get; init; } = 24;

    [Range(1, 100)]
    public int MaximumBatchesPerSweep { get; init; } = 20;
}

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; init; } = true;

    [Required]
    public string OtlpEndpoint { get; init; } = string.Empty;

    [Range(0.0, 1.0)]
    public double SamplingRatio { get; init; } = 1.0;
}
