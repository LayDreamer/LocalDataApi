namespace LocalDataApi.Dto
{
    public sealed class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public required int Total { get; init; }
        public required int Page { get; init; }
        public required int PageSize { get; init; }
    }

    public abstract class PagedRequestDtoBase
    {
        private int _page = 1;
        private int _pageSize = 10;
        private const int MaxPageSize = 100;

        /// <summary>
        /// 当前页码，从1开始
        /// </summary>
        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        /// <summary>
        /// 每页条数，限制最大值为 MaxPageSize
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : (value < 1 ? 1 : value);
        }

        /// <summary>
        /// 排序字段，格式如 "字段名 asc" 或 "字段名 desc"
        /// </summary>
        public string? Sorting { get; set; }
    }
}
