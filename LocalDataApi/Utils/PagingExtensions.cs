using LocalDataApi.Dto;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Utils;

public static class PagingExtensions
{
    private const int MaxRowsPerPagedQuery = 10_000;

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> orderedQuery,
        PagedRequestDtoBase request,
        CancellationToken cancellationToken = default)
    {
        var total = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery.ToPageItemsAsync(request, cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public static async Task<IReadOnlyList<T>> ToPageItemsAsync<T>(
        this IQueryable<T> orderedQuery,
        PagedRequestDtoBase request,
        CancellationToken cancellationToken = default)
    {
        var offset = checked((request.Page - 1) * request.PageSize);
        var rowsToRead = checked(offset + request.PageSize);

        if (rowsToRead > MaxRowsPerPagedQuery)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Page),
                $"分页位置过深，单次最多扫描 {MaxRowsPerPagedQuery} 行，请增加筛选条件。");
        }

        // SQL Server 2008 does not support OFFSET/FETCH, and current EF Core
        // versions no longer emit ROW_NUMBER paging for that server. Limit the
        // server result to the requested page boundary, then slice that bounded
        // window in memory. This keeps normal early-page requests efficient and
        // prevents an unbounded full-table materialization.
        var window = await orderedQuery
            .Take(rowsToRead)
            .ToListAsync(cancellationToken);
        return window.Skip(offset).Take(request.PageSize).ToList();
    }
}
