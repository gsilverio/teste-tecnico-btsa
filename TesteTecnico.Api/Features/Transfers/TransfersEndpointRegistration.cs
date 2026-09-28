using TesteTecnico.Api.Features.Transfers.CancelTransfer;
using TesteTecnico.Api.Features.Transfers.GetTransfer;
using TesteTecnico.Api.Features.Transfers.ListAccountTransfers;
using TesteTecnico.Api.Features.Transfers.RequestTransfer;

namespace TesteTecnico.Api.Features.Transfers;

/// <summary>Delega o registro HTTP aos slices de transferência.</summary>
public static class TransfersEndpointRegistration
{
    /// <summary>Registra criação, agendamento, consulta e cancelamento.</summary>
    public static RouteGroupBuilder MapTransferEndpoints(this RouteGroupBuilder group) =>
        group.MapRequestTransferEndpoints()
            .MapGetTransferEndpoints()
            .MapListAccountTransfersEndpoints()
            .MapCancelTransferEndpoints();
}
