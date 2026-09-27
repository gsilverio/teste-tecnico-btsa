using TesteTecnico.Api.Features.TransferLimits.CreatePolicy;
using TesteTecnico.Api.Features.TransferLimits.DeletePolicy;
using TesteTecnico.Api.Features.TransferLimits.GetPolicies;
using TesteTecnico.Api.Features.TransferLimits.GetPolicy;
using TesteTecnico.Api.Features.TransferLimits.UpdatePolicy;

namespace TesteTecnico.Api.Features.TransferLimits;

/// <summary>Registro das rotas dos casos de uso de políticas de limite.</summary>
public static class TransferLimitPoliciesEndpointRegistration
{
    /// <summary>Delega os verbos CRUD aos respectivos slices de política.</summary>
    public static RouteGroupBuilder MapTransferLimitPolicyEndpoints(this RouteGroupBuilder group) =>
        group.MapCreateTransferLimitPolicyEndpoints()
            .MapGetTransferLimitPoliciesEndpoints()
            .MapGetTransferLimitPolicyEndpoints()
            .MapUpdateTransferLimitPolicyEndpoints()
            .MapDeleteTransferLimitPolicyEndpoints();
}
