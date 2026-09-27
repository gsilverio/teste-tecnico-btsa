namespace TesteTecnico.Api.Common.Pagination;

/// <summary>Parâmetros validados para consultas paginadas.</summary>
public sealed record PaginationRequest(int Page, int PageSize)
{
    /// <summary>Número de página usado quando a consulta não informa um valor.</summary>
    public const int DefaultPage = 1;

    /// <summary>Tamanho de página usado quando a consulta não informa um valor.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Maior número de página aceito pela API.</summary>
    public const int MaximumPage = 1_000_000;

    /// <summary>Maior quantidade de itens aceita por página.</summary>
    public const int MaximumPageSize = 100;

    /// <summary>Posição inicial dos resultados para uso em consultas EF Core.</summary>
    public int Offset => checked((Page - 1) * PageSize);

    /// <summary>Indica se os parâmetros respeitam os limites públicos da API.</summary>
    public bool IsValid => Page is >= DefaultPage and <= MaximumPage
        && PageSize is >= 1 and <= MaximumPageSize;

    /// <summary>Cria os parâmetros aplicando os valores padrão às entradas ausentes.</summary>
    /// <param name="page">Número opcional da página.</param>
    /// <param name="pageSize">Tamanho opcional da página.</param>
    /// <returns>Parâmetros de paginação, que devem ser verificados com <see cref="IsValid"/>.</returns>
    public static PaginationRequest FromQuery(int? page, int? pageSize) =>
        new(page ?? DefaultPage, pageSize ?? DefaultPageSize);
}
