using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.DependencyInjection;
using TesteTecnico.Api.Infrastructure.Endpoints;
using TesteTecnico.Api.Infrastructure.Jobs;
using TesteTecnico.Api.Infrastructure.Messaging;
using TesteTecnico.Api.Infrastructure.Persistence;

const string logOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("SourceContext", "TesteTecnico.Api.Program")
    .WriteTo.Console(outputTemplate: logOutputTemplate)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting {ApplicationName}", typeof(Program).Assembly.GetName().Name);

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        var lokiUrl = builder.Configuration["Observability:LokiUrl"];
        if (!string.IsNullOrWhiteSpace(lokiUrl))
        {
            loggerConfiguration.WriteTo.GrafanaLoki(lokiUrl,
            [
                new LokiLabel { Key = "app", Value = "teste-tecnico-btsa-api" },
                new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
            ]);
        }
    });

    builder.Services.AddOpenApi();
    builder.Services.AddFeatureHandlers();
    builder.Services.AddSingleton(TimeProvider.System);
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("A conexão ConnectionStrings:DefaultConnection não foi configurada.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        AppDbContext.ConfigureNpgsql(options, connectionString));

    builder.Services.AddHangfire(configuration =>
        configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);

    builder.Services
        .AddOptions<TransferRulesOptions>()
        .Bind(builder.Configuration.GetSection(TransferRulesOptions.SectionName))
        .Validate(options => options.WindowMinutes is > 0 and <= 1440, "TransferRules:WindowMinutes deve estar entre 1 e 1440.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.TimeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZoneId, out _), "TransferRules:TimeZoneId deve identificar um fuso horário instalado.")
        .Validate(options => options.DayStart < options.NightStart, "TransferRules:DayStart deve ser anterior a NightStart.")
        .ValidateOnStart();

    builder.Services.AddScoped<TransferMessagePublisher>();
    builder.Services.AddScoped<TransferDispatchJob>();

    builder.Services
        .AddOptions<RabbitMqOptions>()
        .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<RabbitMqConnection>();
    builder.Services.AddSingleton<IHostedService>(serviceProvider =>
        serviceProvider.GetRequiredService<RabbitMqConnection>());
    builder.Services.AddHostedService<TransferQueueConsumer>();
    builder.Services.AddHostedService<TransferOutboxRecoveryService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (context, _, exception) =>
            context.RequestAborted.IsCancellationRequested || context.Response.StatusCode == 499
                ? LogEventLevel.Debug
                : exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : context.Response.StatusCode >= StatusCodes.Status400BadRequest
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseRouting();
    app.UseMiddleware<AuditLoggingMiddleware>();

    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Browser navigation and React StrictMode can cancel an in-flight request.
            // Stop it quietly instead of surfacing a developer exception page.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }

            app.Logger.LogDebug(
                "Request {RequestMethod} {RequestPath} was cancelled by the client",
                context.Request.Method,
                context.Request.Path);
        }
    });

    app.UseHttpMetrics();

    if (app.Environment.IsDevelopment())
    {
        app.MapAllEndpoints();
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Teste Técnico BTSA API v1");
            options.RoutePrefix = "swagger";
        });
        app.UseHangfireDashboard("/hangfire");
    }

    app.MapMetrics();

    // The local Vite dev server proxies /api over HTTP. Redirecting those proxied
    // requests to the self-signed HTTPS development endpoint breaks browser calls.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        app.Logger.LogInformation("Applying pending database migrations");
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations completed");

        if (app.Environment.IsDevelopment())
        {
            await DevelopmentDataSeeder.SeedAsync(dbContext, app.Logger);
        }
    }

    await app.RunAsync();
    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
