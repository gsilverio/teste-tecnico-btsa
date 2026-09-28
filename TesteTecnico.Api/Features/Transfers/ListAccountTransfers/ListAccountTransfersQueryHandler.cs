using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.ListAccountTransfers;

/// <summary>Lista transferências enviadas e recebidas por uma conta.</summary>
public sealed class ListAccountTransfersQueryHandler(AppDbContext dbContext)
{
    public async Task<Result<Page<AccountTransferSummaryResponse>>> HandleAsync(
        Guid accountId,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId, cancellationToken))
        {
            return Result<Page<AccountTransferSummaryResponse>>.Failure(
                new Error("account.not_found", "A conta informada não foi encontrada."));
        }

        var query = dbContext.Transfers.AsNoTracking()
            .Where(transfer => transfer.SourceAccountId == accountId || transfer.DestinationAccountId == accountId);
        var totalCount = await query.CountAsync(cancellationToken);
        var transfers = await query
            .OrderByDescending(transfer => transfer.CreatedAt)
            .ThenByDescending(transfer => transfer.Id)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(transfer => new
            {
                transfer.Id,
                transfer.SourceAccountId,
                transfer.DestinationAccountId,
                transfer.Amount,
                transfer.Method,
                transfer.Status,
                transfer.CreatedAt,
                transfer.ScheduledAt,
                transfer.FailureCode
            })
            .ToArrayAsync(cancellationToken);

        var items = transfers.Select(transfer => new AccountTransferSummaryResponse(
            transfer.Id,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.Amount,
            transfer.Method.ToString(),
            transfer.Status.ToString(),
            transfer.CreatedAt,
            transfer.ScheduledAt,
            transfer.FailureCode)).ToArray();

        return Result<Page<AccountTransferSummaryResponse>>.Success(
            Page<AccountTransferSummaryResponse>.Create(items, pagination.Page, pagination.PageSize, totalCount));
    }
}

/// <summary>Resumo de uma transferência vinculada à conta consultada.</summary>
public sealed record AccountTransferSummaryResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Method,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt,
    string? FailureCode);
