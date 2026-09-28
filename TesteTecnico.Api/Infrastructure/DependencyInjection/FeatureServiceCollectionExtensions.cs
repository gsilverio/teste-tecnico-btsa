using TesteTecnico.Api.Features.Accounts.GetAccounts;
using TesteTecnico.Api.Features.Accounts.GetAccount;
using TesteTecnico.Api.Features.Accounts.ManageOverdraft;
using TesteTecnico.Api.Features.Accounts.ManageStatus;
using TesteTecnico.Api.Features.TransferLimits.CreatePolicy;
using TesteTecnico.Api.Features.TransferLimits.DeletePolicy;
using TesteTecnico.Api.Features.TransferLimits.GetPolicies;
using TesteTecnico.Api.Features.TransferLimits.GetPolicy;
using TesteTecnico.Api.Features.TransferLimits.UpdatePolicy;
using TesteTecnico.Api.Features.Transfers.CancelTransfer;
using TesteTecnico.Api.Features.Transfers.ExecuteTransfer;
using TesteTecnico.Api.Features.Transfers.GetTransfer;
using TesteTecnico.Api.Features.Transfers.ListAccountTransfers;
using TesteTecnico.Api.Features.Transfers.RequestTransfer;
using TesteTecnico.Api.Features.Transfers.Shared;

namespace TesteTecnico.Api.Infrastructure.DependencyInjection;

/// <summary>Registra os handlers dos casos de uso HTTP.</summary>
public static class FeatureServiceCollectionExtensions
{
    /// <summary>Adiciona os handlers de contas e políticas de limite com lifetime scoped.</summary>
    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        services.AddScoped<GetAccountsQueryHandler>();
        services.AddScoped<GetAccountByIdQueryHandler>();
        services.AddScoped<GetOverdraftLimitQueryHandler>();
        services.AddScoped<SetOverdraftLimitCommandHandler>();
        services.AddScoped<ManageAccountStatusCommandHandler>();
        services.AddScoped<CreateTransferLimitPolicyCommandHandler>();
        services.AddScoped<GetTransferLimitPoliciesQueryHandler>();
        services.AddScoped<GetTransferLimitPolicyQueryHandler>();
        services.AddScoped<UpdateTransferLimitPolicyCommandHandler>();
        services.AddScoped<DeleteTransferLimitPolicyCommandHandler>();
        services.AddScoped<RequestTransferCommandHandler>();
        services.AddScoped<GetTransferQueryHandler>();
        services.AddScoped<ListAccountTransfersQueryHandler>();
        services.AddScoped<CancelTransferCommandHandler>();
        services.AddScoped<ExecuteTransferCommandHandler>();
        services.AddScoped<TransferLimitEvaluator>();
        return services;
    }
}
