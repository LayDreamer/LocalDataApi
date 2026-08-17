using System;
using Xunit;

namespace LocalDataApi.Tests.Platform;

/// <summary>
/// 环境依赖型 Fact:仅当环境变量 ConnectionStrings__DefaultConnection(SQL Server 实例)存在时才执行;
/// 缺失时通过 xUnit 的 Skip 机制【显式跳过】,在测试报告中表现为 Skipped 而非静默 Passed,
/// 避免 CI 未注入连接串时误判为已验证 UPDLOCK+ROWLOCK 行锁路径。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Skip = "SQL Server 并发集成测试已跳过:请设置环境变量 ConnectionStrings__DefaultConnection 以在 CI 中真实验证 UPDLOCK+ROWLOCK 行锁路径。";
        }
    }
}
