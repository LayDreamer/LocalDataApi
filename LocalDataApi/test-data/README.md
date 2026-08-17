# 工单销控表测试数据

## 文件说明

| 文件 | 说明 |
|---|---|
| `generate-workorder-test-data.py` | Python 数据生成脚本，可复用、可调整数据量 |
| `workorder-sales-control-test-data.sql` | 可直接在 SQL Server 执行的测试数据 INSERT 脚本 |
| `rollback-workorder-test-data.sql` | 按 `TEST_` 前缀清理测试数据的回滚脚本 |

## 数据规模

- **工单销控表**：500 条
- **工单销控表明细**：1500 条（每条主表 3 条明细）
- **外产_生产**：1500 条
- **外产_入库**：1500 条
- **外产_发运**：1500 条
- **外产_领料**：1500 条（500 条父记录 + 1000 条子记录）
- **外产_BOM**：1500 条（500 条父记录 + 1000 条子记录）

> 注：排产分析单、产品资料为真实大表（各 3 万+ 条），本测试数据未插入，避免污染。

所有测试数据均使用 `TEST_` 前缀，便于识别和清理，不会影响现有业务数据。

## 使用步骤

### 1. 检查现有数据是否冲突

在执行插入脚本前，建议先确认目标数据库中没有以 `TEST_` 开头的测试数据：

```sql
SELECT COUNT(*) FROM [工单销控表] WHERE [货号] LIKE 'TEST-%';
```

如果返回值大于 0，说明已存在测试数据，请先执行回滚脚本清理，或修改生成脚本中的前缀。

### 2. 执行插入脚本

在 SQL Server Management Studio (SSMS) 中：

1. 连接到目标数据库服务器。
2. 选择要插入测试数据的数据库。
3. 打开 `workorder-sales-control-test-data.sql`。
4. 点击"执行"。

脚本已使用事务包裹，若发生错误会自动回滚。

### 3. 验证结果

脚本末尾包含验证查询，执行后会显示各表插入的测试数据条数。也可以单独执行：

```sql
SELECT '工单销控表' AS 表名, COUNT(*) AS 条数 FROM [工单销控表] WHERE [货号] LIKE 'TEST-%'
UNION ALL
SELECT '工单销控表明细', COUNT(*) FROM [工单销控表明细] WHERE [货号] LIKE 'TEST-%'
UNION ALL
SELECT '外产_生产', COUNT(*) FROM [外产_生产] WHERE [货号] LIKE 'TEST-%'
UNION ALL
SELECT '外产_入库', COUNT(*) FROM [外产_入库] WHERE [编号] LIKE 'TEST-DET-%'
UNION ALL
SELECT '外产_发运', COUNT(*) FROM [外产_发运] WHERE [货号] LIKE 'TEST-%'
UNION ALL
SELECT '外产_领料(父)', COUNT(*) FROM [外产_领料] WHERE [编号] LIKE 'TEST-PM-PARENT-%'
UNION ALL
SELECT '外产_领料(子)', COUNT(*) FROM [外产_领料] WHERE [编号] LIKE 'TEST-PM-%' AND [父级编号] IS NOT NULL
UNION ALL
SELECT '外产_BOM(父)', COUNT(*) FROM [外产_BOM] WHERE [货号] LIKE 'TEST-HH-%' AND [父级编号] IS NULL
UNION ALL
SELECT '外产_BOM(子)', COUNT(*) FROM [外产_BOM] WHERE [父级编号] LIKE 'TEST-BOM-PARENT-%';
```

## 回滚测试数据

如需清理测试数据，执行 `rollback-workorder-test-data.sql`：

```sql
-- 注意：请确认目标数据库后再执行！
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DELETE FROM [外产_BOM] WHERE [货号] LIKE 'TEST-%';
DELETE FROM [外产_领料] WHERE [编号] LIKE 'TEST-PM-%';
DELETE FROM [外产_发运] WHERE [货号] LIKE 'TEST-%';
DELETE FROM [外产_入库] WHERE [编号] LIKE 'TEST-DET-%';
DELETE FROM [外产_生产] WHERE [货号] LIKE 'TEST-%';
DELETE FROM [工单销控表明细] WHERE [货号] LIKE 'TEST-%';
DELETE FROM [工单销控表] WHERE [货号] LIKE 'TEST-%';

COMMIT TRANSACTION;
```

## 重新生成数据

如需调整数据量或字段规则，编辑 `generate-workorder-test-data.py` 顶部的配置，然后重新运行：

```bash
python generate-workorder-test-data.py
```

可调整的配置项：

- `MAIN_COUNT`：工单销控表记录数
- `DETAIL_PER_MAIN`：每条主表的明细数
- 枚举值：车间名称、商品属性、规格等

## 数据关联说明

- 工单销控表.`编号` = `TEST-MAIN-{序号}`
- 工单销控表.`货号` = `TEST-HH-{序号}`（唯一）
- 工单销控表明细.`父级编号` = 工单销控表.`编号`
- 工单销控表明细.`编号` = `TEST-DET-{主表序号}-{明细序号}`
- 工单销控表明细.`分析单号` = `TEST-FX-{主表序号}-{明细序号}`（保证唯一索引）
- 工单销控表明细.`排产编号` = `TEST-PC-{主表序号}`
- 外产_入库.`编号` = 工单销控表明细.`编号`

### 物料需求明细（前端点击“工单总数”展开）

前端 `WorkOrderTracking.vue` 的 `generateMaterialDetail` 调用链路：

1. `GetExternalProductionBOMList(货号=工单销控表.货号)`
   - 先查 `外产_BOM` 中 货号=产品货号、父级编号=NULL 的父记录
   - 再返回该父记录下的所有子记录（层=1）
2. 对子记录的 货号 批量查询 `外产_领料`
3. 通过 外产_领料.`父级编号` 反查父领料记录，仅当父领料.`货号`=产品货号时汇总 出库数量
4. 计算：需求数=工单总数×用量，缺料数=需求数-已出库数，仓库缺料=max(0, 缺料数-仓库数)

因此测试数据需要满足：

- **外产_BOM 父记录**：`货号` = `TEST-HH-{序号}`，`层` = `0`，`父级编号` = NULL
- **外产_BOM 子记录**：`父级编号` = 父记录.`编号`，`层` = `1`，并携带 `品名/规格/用量/仓库名称/仓库数/产品属性/来源/单位`
- **外产_领料 父记录**：`货号` = `TEST-HH-{序号}`，`父级编号` = NULL
- **外产_领料 子记录**：`货号` = 子件货号，`父级编号` = 父领料记录.`编号`
