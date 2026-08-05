namespace LocalDataApi.Domain.Common;

/// <summary>
/// 领域仓储抽象。当前遗留 ERP 数据库以 DbContext 直查为主,
/// 此接口为后续引入独立仓储实现预留边界(Infrastructure 层实现)。
/// </summary>
/// <typeparam name="TEntity">聚合/实体类型</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    Task<List<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);
}

/// <summary>
/// 工作单元抽象:对事务边界的显式声明,由 Infrastructure 层基于 DbContext 实现。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
