using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Infrastructure.Audit;

/// <summary>Persiste de forma síncrona a auditoria dos endpoints anotados.</summary>
public sealed class AuditLoggingMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuditLoggingMiddleware> logger)
{
    private const int MaximumCapturedErrorBytes = 16 * 1024;
    private const int MaximumErrorMessageLength = 2048;

    /// <summary>Executa a rota e registra seu resultado em um escopo de persistência isolado.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var auditAction = context.GetEndpoint()?.Metadata.GetMetadata<AuditActionAttribute>();
        if (auditAction is null)
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        var captureStream = new AuditResponseCaptureStream(originalBody, MaximumCapturedErrorBytes);
        context.Response.Body = captureStream;
        Exception? unhandledException = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            unhandledException = exception;
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
            var statusCode = unhandledException is null ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;
            var errorMessage = unhandledException is null
                ? ReadProblemDetails(captureStream, statusCode)
                : "Uma falha interna impediu a conclusão da requisição.";
            await PersistAuditAsync(context, auditAction.ActionType, statusCode, errorMessage);
        }
    }

    private async Task PersistAuditAsync(
        HttpContext context,
        AuditActionType actionType,
        int statusCode,
        string? errorMessage)
    {
        try
        {
            var routeValues = context.Request.RouteValues
                .Where(pair => pair.Value is not null && Guid.TryParse(pair.Value.ToString(), out _))
                .ToDictionary(pair => pair.Key, pair => pair.Value!.ToString());
            var payloadJson = JsonSerializer.Serialize(new { routeValues });
            var endpoint = context.GetEndpoint() as RouteEndpoint;
            var routeTemplate = endpoint?.RoutePattern.RawText ?? "/unknown";
            var transferId = context.Request.RouteValues.TryGetValue("transferId", out var rawTransferId)
                && Guid.TryParse(rawTransferId?.ToString(), out var parsedTransferId)
                    ? parsedTransferId
                    : (Guid?)null;
            var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            if (actorId?.Length > 200)
            {
                actorId = actorId[..200];
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.AuditEntries.Add(AuditEntry.FromApiRequest(
                actionType,
                actorId,
                context.Request.Method,
                routeTemplate,
                statusCode,
                transferId,
                payloadJson,
                Truncate(errorMessage),
                timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not persist API audit action {AuditAction} for {RequestPath}", actionType, context.Request.Path);
        }
    }

    private static string? ReadProblemDetails(AuditResponseCaptureStream response, int statusCode)
    {
        if (statusCode < StatusCodes.Status400BadRequest)
        {
            return null;
        }

        var body = response.GetCapturedText();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var propertyName in new[] { "detail", "title" })
            {
                if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to a bounded response excerpt if the response is not ProblemDetails JSON.
        }

        return body;
    }

    private static string? Truncate(string? value) =>
        value is { Length: > MaximumErrorMessageLength } ? value[..MaximumErrorMessageLength] : value;

    private sealed class AuditResponseCaptureStream(Stream response, int maximumCaptureBytes) : Stream
    {
        private readonly MemoryStream captured = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => response.CanWrite;
        public override long Length => response.Length;
        public override long Position { get => response.Position; set => throw new NotSupportedException(); }

        public override void Flush() => response.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => response.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            response.Write(buffer, offset, count);
            Capture(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            response.Write(buffer);
            Capture(buffer);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await response.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
            Capture(buffer.AsSpan(offset, count));
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await response.WriteAsync(buffer, cancellationToken);
            Capture(buffer.Span);
        }

        public string GetCapturedText() => System.Text.Encoding.UTF8.GetString(captured.ToArray());

        private void Capture(ReadOnlySpan<byte> bytes)
        {
            var remaining = maximumCaptureBytes - (int)captured.Length;
            if (remaining > 0)
            {
                captured.Write(bytes[..Math.Min(remaining, bytes.Length)]);
            }
        }
    }
}
