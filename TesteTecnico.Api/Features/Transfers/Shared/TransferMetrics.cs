using Prometheus;

namespace TesteTecnico.Api.Features.Transfers.Shared;

/// <summary>Contadores financeiros com dimensões de baixa cardinalidade para Prometheus.</summary>
public static class TransferMetrics
{
    private static readonly Counter Executions = Metrics.CreateCounter(
        "btsa_transfer_executions_total",
        "Total de execuções financeiras por resultado.",
        new CounterConfiguration { LabelNames = ["outcome"] });

    private static readonly Counter QueueFailures = Metrics.CreateCounter(
        "btsa_transfer_queue_failures_total",
        "Falhas técnicas de processamento na fila de transferências.",
        new CounterConfiguration { LabelNames = ["outcome"] });

    /// <summary>Registra conclusão ou rejeição definitiva por regra de negócio.</summary>
    public static void RecordOutcome(string outcome) => Executions.WithLabels(outcome).Inc();

    /// <summary>Registra retry técnico ou encaminhamento para dead-letter.</summary>
    public static void RecordQueueFailure(bool deadLettered) =>
        QueueFailures.WithLabels(deadLettered ? "dead_letter" : "retry").Inc();
}
