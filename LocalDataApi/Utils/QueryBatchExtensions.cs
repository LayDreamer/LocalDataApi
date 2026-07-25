using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Utils;

/// <summary>
/// EF Core 查询分批扩展。
/// 将"内存集合 Contains 实体字段"的查询按批次执行，避免超大 IN 列表导致单条 SQL 文本过长。
/// 每批将集合作为常量传入，EF 会生成 IN (值1, 值2, ...) 常量列表；
/// 即便未启用 TranslateParameterizedCollectionsToConstants，常量集合也不会生成 OPENJSON，因此兼容低版本 SQL Server。
/// 当集合数量不超过 threshold 时自动退化为单次查询，避免小列表产生多余的分批往返。
/// </summary>
public static class QueryBatchExtensions
{
    /// <summary>
    /// 按 keySelector 指定的字段，分批执行 IN 查询并合并结果。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">匹配字段类型</typeparam>
    /// <typeparam name="TResult">投影结果类型</typeparam>
    /// <param name="source">已附加固定过滤条件（如 AsNoTracking、固定 Where）的 IQueryable</param>
    /// <param name="keys">内存中的匹配键值集合</param>
    /// <param name="keySelector">实体字段选择器，如 e => e.货号</param>
    /// <param name="selector">结果投影，如 e => new { e.货号, e.生产类型 }</param>
    /// <param name="threshold">不超过此数量时单次查询（默认 1000），避免小列表额外分批</param>
    /// <param name="batchSize">分批时每批大小（默认 1000）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<List<TResult>> WhereInBatchesAsync<TEntity, TKey, TResult>(
        this IQueryable<TEntity> source,
        IEnumerable<TKey> keys,
        Expression<Func<TEntity, TKey>> keySelector,
        Expression<Func<TEntity, TResult>> selector,
        int threshold = 1000,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var keyList = keys as List<TKey> ?? keys.ToList();
        if (keyList.Count == 0)
        {
            return new List<TResult>();
        }

        // 小集合：单次查询即可，生成 IN (常量) 列表
        if (keyList.Count <= threshold)
        {
            var singlePredicate = BuildContainsPredicate(keyList, keySelector);
            return await source.Where(singlePredicate).Select(selector).ToListAsync(cancellationToken);
        }

        // 大集合：分批查询并合并，控制单条 SQL 的 IN 列表长度
        var result = new List<TResult>(keyList.Count);
        for (int i = 0; i < keyList.Count; i += batchSize)
        {
            var batch = keyList.Skip(i).Take(batchSize).ToList();
            var batchPredicate = BuildContainsPredicate(batch, keySelector);
            var batchResult = await source.Where(batchPredicate).Select(selector).ToListAsync(cancellationToken);
            result.AddRange(batchResult);
        }

        return result;
    }

    private static Expression<Func<TEntity, bool>> BuildContainsPredicate<TEntity, TKey>(
        List<TKey> values,
        Expression<Func<TEntity, TKey>> keySelector)
    {
        var entityParameter = keySelector.Parameters[0];
        var containsMethod = typeof(List<TKey>).GetMethod("Contains", new[] { typeof(TKey) })
            ?? throw new InvalidOperationException("未能获取 List<T>.Contains 方法");
        var valuesConstant = Expression.Constant(values, typeof(List<TKey>));
        var containsCall = Expression.Call(valuesConstant, containsMethod, keySelector.Body);
        return Expression.Lambda<Func<TEntity, bool>>(containsCall, entityParameter);
    }

    /// <summary>
    /// 双字段 IN 查询分批重载：以 keySelector1 对应的集合作为分批驱动（控制单条 SQL 长度），
    /// keySelector2 对应的集合作为全量 IN 条件（两者 AND 组合）。
    /// 适用于如 "货号 IN 列表A AND 合同号 IN 列表B" 的场景。
    /// 注意：SQL 中 AND 双 IN 无法对两个集合同时分批（否则会改变结果集），故仅对第一个集合分批。
    /// </summary>
    public static async Task<List<TResult>> WhereInBatchesAsync<TEntity, TKey1, TKey2, TResult>(
        this IQueryable<TEntity> source,
        IEnumerable<TKey1> keys1,
        Expression<Func<TEntity, TKey1>> keySelector1,
        IEnumerable<TKey2> keys2,
        Expression<Func<TEntity, TKey2>> keySelector2,
        Expression<Func<TEntity, TResult>> selector,
        int threshold = 1000,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var keyList1 = keys1 as List<TKey1> ?? keys1.ToList();
        var keyList2 = keys2 as List<TKey2> ?? keys2.ToList();
        if (keyList1.Count == 0 || keyList2.Count == 0)
        {
            return new List<TResult>();
        }

        if (keyList1.Count <= threshold)
        {
            var predicate = BuildAndPredicate(keyList1, keySelector1, keyList2, keySelector2);
            return await source.Where(predicate).Select(selector).ToListAsync(cancellationToken);
        }

        var result = new List<TResult>(keyList1.Count);
        for (int i = 0; i < keyList1.Count; i += batchSize)
        {
            var batch = keyList1.Skip(i).Take(batchSize).ToList();
            var predicate = BuildAndPredicate(batch, keySelector1, keyList2, keySelector2);
            var batchResult = await source.Where(predicate).Select(selector).ToListAsync(cancellationToken);
            result.AddRange(batchResult);
        }

        return result;
    }

    private static Expression<Func<TEntity, bool>> BuildAndPredicate<TEntity, TKey1, TKey2>(
        List<TKey1> values1,
        Expression<Func<TEntity, TKey1>> keySelector1,
        List<TKey2> values2,
        Expression<Func<TEntity, TKey2>> keySelector2)
    {
        var predicate1 = BuildContainsPredicate(values1, keySelector1);
        var predicate2 = BuildContainsPredicate(values2, keySelector2);
        // 两个谓词各自引用独立的参数实例，需统一为同一个 ParameterExpression
        var replacedBody2 = new ParameterReplacer(predicate2.Parameters[0], predicate1.Parameters[0])
            .Visit(predicate2.Body);
        var andAlso = Expression.AndAlso(predicate1.Body, replacedBody2!);
        return Expression.Lambda<Func<TEntity, bool>>(andAlso, predicate1.Parameters[0]);
    }

    /// <summary>
    /// 将表达式树中的某个参数替换为另一个参数（用于合并多个谓词时统一参数）。
    /// </summary>
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam;
        private readonly ParameterExpression _newParam;

        public ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam)
        {
            _oldParam = oldParam;
            _newParam = newParam;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParam ? _newParam : base.VisitParameter(node);
    }
}
