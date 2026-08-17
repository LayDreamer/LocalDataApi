using LocalDataApi.Application.Platform;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LocalDataApi.Tests.Platform;

/// <summary>
/// SQL Server 真实并发集成测试(P0-02 闭环):
/// 验证 NumberRuleService 的 UPDLOCK + ROWLOCK 行锁路径在真实 SQL Server 上的并发安全。
/// 依赖环境变量 ConnectionStrings__DefaultConnection(SQL Server 实例);
/// 使用独立测试库(自动创建/销毁),不触碰业务库。
/// 注意: 缺少该环境变量时,本测试会【显式跳过(Skipped)】而非静默通过(Passed);
///       CI 必须注入 ConnectionStrings__DefaultConnection 才会真正执行并验证行锁路径,否则视为未覆盖。
/// </summary>
public sealed class NumberRuleSqlServerConcurrencyTests
{
    private const int ConcurrencyCount = 60;

    [SqlServerFact]
    public async Task SqlServer_ConcurrentGenerate_AllUnique_AndFinalSequenceCorrect()
    {
        var baseConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;

        var testDbName = $"wp02_nr_test_{Guid.NewGuid():N}"[..30];
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(new SqlConnectionStringBuilder(baseConn) { InitialCatalog = testDbName }.ConnectionString)
            .Options;

        try
        {
            // 1. 建库
            await using (var master = new SqlConnection(
                new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" }.ConnectionString))
            {
                await master.OpenAsync();
                await using var createCmd = master.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE [{testDbName}]";
                await createCmd.ExecuteNonQueryAsync();
            }

            // 2. 建表 + 播种规则(独立测试库)
            await using (var db = new AppDbContext(options))
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE dbo.Sys_NumberRule (
                        Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        RuleCode nvarchar(64) NOT NULL,
                        RuleName nvarchar(128) NOT NULL,
                        Prefix nvarchar(32) NULL,
                        DateFormat nvarchar(16) NULL,
                        SequenceLength int NOT NULL,
                        CurrentSequence bigint NOT NULL,
                        PeriodType int NOT NULL,
                        LastResetDate datetime2 NULL,
                        Status tinyint NOT NULL,
                        Description nvarchar(max) NULL,
                        CreateTime datetime2 NOT NULL,
                        UpdateTime datetime2 NULL);
                    CREATE UNIQUE INDEX UX_Sys_NumberRule_RuleCode ON dbo.Sys_NumberRule(RuleCode);");
                await db.NumberRules.AddAsync(new NumberRule
                {
                    RuleCode = "ConcurrencyTest", RuleName = "SQL并发测试", Prefix = "CT-",
                    DateFormat = null, SequenceLength = 5, CurrentSequence = 0,
                    PeriodType = 0, Status = 1, CreateTime = DateTime.Now
                });
                await db.SaveChangesAsync();
            }

            // 3. 两个完全独立的 DbContext + Service 实例(不共享任何状态,仅同一测试库)
            await using var ctxA = new AppDbContext(options);
            await using var ctxB = new AppDbContext(options);
            var serviceA = new NumberRuleService(ctxA);
            var serviceB = new NumberRuleService(ctxB);

            var tasks = Enumerable.Range(0, ConcurrencyCount)
                .Select(i => (i % 2 == 0 ? serviceA : serviceB).GetNextCodeAsync("ConcurrencyTest"));
            var codes = await Task.WhenAll(tasks);

            // 4. 断言:编号全部唯一(UPDLOCK 串行化)
            Assert.Equal(ConcurrencyCount, codes.Distinct().Count());
            Assert.All(codes, code => Assert.StartsWith("CT-", code));

            // 5. 断言:CurrentSequence 终值 = 并发次数(无丢失更新)
            await using (var verify = new AppDbContext(options))
            {
                var rule = await verify.NumberRules.SingleAsync(r => r.RuleCode == "ConcurrencyTest");
                Assert.Equal(ConcurrencyCount, rule.CurrentSequence);
            }
        }
        finally
        {
            // 6. 清理:删除测试库
            try
            {
                await using var master = new SqlConnection(
                    new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" }.ConnectionString);
                await master.OpenAsync();
                await using var dropCmd = master.CreateCommand();
                dropCmd.CommandText = $"""
                    IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{testDbName}')
                    BEGIN
                        ALTER DATABASE [{testDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{testDbName}];
                    END
                    """;
                await dropCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // 清理失败不影响测试结论(遗留测试库可手动删除)
            }
        }
    }
}
