using Microsoft.AspNetCore.Mvc;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Transfers.RequestTransfer;

/// <summary>Registra as rotas de solicitação de transferência.</summary>
public static class RequestTransferEndpoints
{
    /// <summary>Registra as rotas imediata e agendada.</summary>
    public static RouteGroupBuilder MapRequestTransferEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/transfers", async (
                TransferRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                RequestTransferCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, idempotencyKey, false, cancellationToken);
                return result.Match<IResult>(
                    Results.Ok,
                    error => error.ToHttpResult(httpContext));
            })
            .WithName("RequestTransfer")
            .WithTags("Transfers")
            .WithSummary("Solicita uma transferência imediata")
            .WithDescription("Aceita Idempotency-Key opcional. method deve ser Pix (com pixKey) ou BankAccount (com bankIspb, branch e accountNumber; checkDigit é opcional). Executa o processamento financeiro na própria requisição usando o mesmo executor atômico do consumidor RabbitMQ e retorna o estado final da transferência.")
            .Audit(AuditActionType.RequestTransfer)
            .Produces<TransferResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/transfers/scheduled", async (
                TransferRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                RequestTransferCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, idempotencyKey, true, cancellationToken);
                return result.Match<IResult>(
                    response => Results.Accepted(
                        $"/api/transfers/{response.Id}",
                        new TransferAcceptedResponse(response.Id, response.Status, response.CreatedAt, response.ScheduledAt)),
                    error => error.ToHttpResult(httpContext));
            })
            .WithName("ScheduleTransfer")
            .WithTags("Transfers")
            .WithSummary("Agenda uma transferência")
            .WithDescription("Aceita Idempotency-Key opcional e exige scheduledAt futuro com offset. method deve ser Pix (com pixKey) ou BankAccount (com bankIspb, branch e accountNumber; checkDigit é opcional). Hangfire publica a mensagem no RabbitMQ na data informada.")
            .Audit(AuditActionType.ScheduleTransfer)
            .Produces<TransferAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
