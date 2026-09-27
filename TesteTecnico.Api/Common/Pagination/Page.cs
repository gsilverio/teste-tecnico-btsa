using System.Text.Json.Serialization;

namespace TesteTecnico.Api.Common.Pagination;

/// <summary>Representa uma página de resultados e seus metadados de navegação.</summary>
/// <typeparam name="T">Tipo dos itens contidos na página.</typeparam>
public sealed record Page<T>
{
    private Page(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        PageNumber = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>Itens retornados para a página solicitada.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Número da página, começando em 1.</summary>
    [JsonPropertyName("page")]
    public int PageNumber { get; }

    /// <summary>Quantidade máxima de itens solicitada por página.</summary>
    public int PageSize { get; }

    /// <summary>Quantidade total de itens que correspondem à consulta.</summary>
    public int TotalCount { get; }

    /// <summary>Quantidade total de páginas; zero quando a consulta não possui itens.</summary>
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)(((long)TotalCount + PageSize - 1) / PageSize);

    /// <summary>Indica se existe uma página anterior.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Indica se existe uma próxima página.</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Cria uma página validando os metadados e o tamanho do conjunto de itens.</summary>
    /// <param name="items">Itens materializados da página.</param>
    /// <param name="page">Número da página, começando em 1.</param>
    /// <param name="pageSize">Tamanho solicitado para a página.</param>
    /// <param name="totalCount">Quantidade total de itens da consulta.</param>
    /// <returns>A página validada.</returns>
    /// <exception cref="ArgumentNullException">Quando <paramref name="items"/> é nulo.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Quando algum metadado é inválido.</exception>
    /// <exception cref="ArgumentException">Quando a página contém mais itens que o tamanho informado.</exception>
    public static Page<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        if (items.Count > pageSize)
        {
            throw new ArgumentException("A quantidade de itens não pode exceder o tamanho da página.", nameof(items));
        }

        return new Page<T>(items, page, pageSize, totalCount);
    }
}
