#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成工单销控表及相关外产链路测试数据 SQL 脚本。

生成规则：
- 500 条工单销控表记录
- 每条主表记录平均 3 条明细，共约 1500 条明细
- 外产_生产、外产_入库、外产_发运、外产_领料、外产_BOM（精简模式:不生成排产分析单与产品资料）
- 外产_BOM 按“父记录 + 子记录”结构生成，以支撑前端“物料需求明细”展开
- 所有测试数据使用 TEST_ 前缀，便于识别和清理
"""

import random
import os
from datetime import datetime, timedelta

# ==================== 配置 ====================
MAIN_COUNT = 500          # 工单销控表记录数
DETAIL_PER_MAIN = 3       # 每条主表对应的明细数

# 精简开关：排产分析单(3万+真实大表) 与 产品资料(3.5万+真实大表)
# 工单查询并不 JOIN 这两张表，测试查询速度时无需插入，避免污染大真实表。
INCLUDE_SCHEDULING = False
INCLUDE_PRODUCT = False

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_SQL = os.path.join(OUTPUT_DIR, "workorder-sales-control-test-data.sql")
ROLLBACK_SQL = os.path.join(OUTPUT_DIR, "rollback-workorder-test-data.sql")

# 枚举值
WORKSHOPS = ["数控车间", "包装车间", "装配车间", "检测车间"]
PRODUCT_ATTRS = ["电磁阀半成品", "电磁阀", "线圈组件", "阀体组件"]
SPECS = ["20G5V.G1/2", "20G5V-5918(5918-AC24V-D-20VA)", "15G4V.G3/8", "25G6V.G3/4"]
LAYERS = ["1", "2", "3"]
SOURCES = ["自制", "外购", "外协"]
UNITS = ["个", "件", "套"]
USERS = ["TEST_USER", "ADMIN", "PMC01"]

# 固定时间
CREATE_TIME = datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def random_date(start_str="2026-06-01", end_str="2026-12-31"):
    """生成随机日期字符串。"""
    start = datetime.strptime(start_str, "%Y-%m-%d")
    end = datetime.strptime(end_str, "%Y-%m-%d")
    delta = end - start
    random_days = random.randint(0, delta.days)
    return (start + timedelta(days=random_days)).strftime("%Y-%m-%d")


def escape_sql(value):
    """转义 SQL 字符串值。"""
    if value is None:
        return "NULL"
    return "'" + str(value).replace("'", "''") + "'"


def generate_main_records():
    """生成工单销控表主表记录。"""
    records = []
    for i in range(1, MAIN_COUNT + 1):
        main_no = f"TEST-MAIN-{i:03d}"
        item_no = f"TEST-HH-{i:06d}"
        workshop = WORKSHOPS[(i - 1) % len(WORKSHOPS)]
        product_attr = PRODUCT_ATTRS[(i - 1) % len(PRODUCT_ATTRS)]
        spec = SPECS[(i - 1) % len(SPECS)]
        layer = LAYERS[(i - 1) % len(LAYERS)]
        product_name = f"测试产品-{item_no}"
        total = random.randint(20, 200)
        in_stock = random.randint(0, total)
        in_prod = total - in_stock
        complete_rate = round((in_stock / total) * 100, 2) if total > 0 else 0

        records.append({
            "编号": main_no,
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "车间名称": workshop,
            "工单单号": "",
            "商品属性": product_attr,
            "货号": item_no,
            "品名": product_name,
            "规格": spec,
            "工单总数": str(total),
            "已入库数": str(in_stock),
            "在产数量": str(in_prod),
            "齐套": "未分析" if random.random() > 0.3 else "已齐套",
            "配料": "未配料" if random.random() > 0.5 else "已配料",
            "交货日期": random_date(),
            "分析日期": random_date(),
            "生产完成率": f"{complete_rate}%",
            "层": layer,
            "排产用户": random.choice(USERS),
        })
    return records


def generate_detail_records(main_records):
    """生成工单销控表明细记录。"""
    details = []
    for main in main_records:
        main_index = int(main["编号"].split("-")[-1])
        group_index = main_index
        scheduling_no = f"TEST-PC-{group_index:03d}"

        for j in range(DETAIL_PER_MAIN):
            # 每条明细的(货号, 分析单号)必须唯一(唯一索引 UX_工单销控表明细_业务键)
            analysis_no = f"TEST-FX-{group_index:03d}-{j}"
            detail_no = f"TEST-DET-{main_index:03d}-{j}"
            # 工单单号：模拟 CalculateWorkOrder 逻辑，保留数字
            work_order = "".join(filter(str.isdigit, detail_no))
            produce = random.randint(5, 80)
            in_stock_detail = random.randint(0, produce)
            wait = produce - in_stock_detail

            details.append({
                "编号": detail_no,
                "用户编号": None,
                "用户铭": None,
                "修改状态": None,
                "创建时间": CREATE_TIME,
                "锁定用户": None,
                "审核过程": None,
                "打印": None,
                "货号": main["货号"],
                "品名": main["品名"],
                "规格": main["规格"],
                "工单单号": work_order,
                "排产编号": scheduling_no,
                "交货日期": main["交货日期"],
                "生产数": str(produce),
                "入库数": str(in_stock_detail),
                "待产数": str(wait),
                "父级编号": main["编号"],
                "分析单号": analysis_no,
                "排产用户": main["排产用户"],
            })
    return details


def generate_scheduling_analysis(main_records):
    """生成排产分析单记录。"""
    records = []
    seen = set()
    for main in main_records:
        main_index = int(main["编号"].split("-")[-1])
        analysis_no = f"TEST-FX-{main_index:03d}"
        scheduling_no = f"TEST-PC-{main_index:03d}"
        if analysis_no in seen:
            continue
        seen.add(analysis_no)
        records.append({
            "编号": f"TEST-SA-{main_index:03d}",
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "分析单号": analysis_no,
            "分析人": random.choice(USERS),
            "分析日期": main["分析日期"],
            "生产方式": random.choice(["自制", "外协"]),
            "客户简称": f"客户-{main_index:03d}",
            "排产编号": scheduling_no,
        })
    return records


def generate_product_data(main_records):
    """生成产品资料记录。"""
    records = []
    for main in main_records:
        main_index = int(main["编号"].split("-")[-1])
        records.append({
            "编号": f"TEST-PD-{main_index:06d}",
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": main["货号"],
            "中文品名": main["品名"],
            "中文规格": main["规格"],
            "产品类别": main["商品属性"],
            "工序名称": "默认工序",
            "生产车间": main["车间名称"],
            "产品属性": main["商品属性"],
            "制造方式": random.choice(SOURCES),
            "数量单位": random.choice(UNITS),
            "停用": "否",
        })
    return records


def generate_external_production(details):
    """生成外产_生产记录。"""
    records = []
    for d in details:
        detail_index = d["编号"].replace("TEST-DET-", "").replace("-", "")
        demand = random.randint(10, 100)
        produced = random.randint(0, demand)
        records.append({
            "编号": f"TEST-EP-{detail_index}",
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": d["货号"],
            "排产编号": d["排产编号"],
            "需求量": str(demand),
            "生产数量": str(produced),
            "分析单号": d["分析单号"],
            "工单单号": d["工单单号"],
            "来源": random.choice(SOURCES),
            "工序车间": random.choice(WORKSHOPS),
            "工序": "默认工序",
            "工单层级": d["父级编号"],
            "电压": random.choice(["AC220V", "DC24V", "AC110V"]),
            "线圈": random.choice(["铜线", "铝线"]),
            "订单数": str(random.randint(1, 10)),
            "单位": random.choice(UNITS),
            "仓库名称": "成品仓",
            "备注": None,
            "用量": str(round(random.uniform(0.5, 2.0), 2)),
        })
    return records


def generate_external_warehousing(details):
    """生成外产_入库记录（编号与明细编号相同）。"""
    records = []
    for d in details:
        demand = random.randint(10, 100)
        in_qty = int(d["入库数"])
        records.append({
            "编号": d["编号"],
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": d["货号"],
            "需求量": str(demand),
            "入库数量": str(in_qty),
            "分析单号": d["分析单号"],
            "工单单号": d["工单单号"],
        })
    return records


def generate_external_shipment(details):
    """生成外产_发运记录。"""
    records = []
    for d in details:
        detail_index = d["编号"].replace("TEST-DET-", "").replace("-", "")
        demand = random.randint(10, 100)
        shipped = random.randint(0, demand)
        records.append({
            "编号": f"TEST-ES-{detail_index}",
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": d["货号"],
            "排产编号": d["排产编号"],
            "需求量": str(demand),
            "发运数量": str(shipped),
            "分析单号": d["分析单号"],
        })
    return records


def generate_external_pick_material(main_records, bom_parents, bom_children):
    """生成外产_领料记录。

    与 外产_BOM 对齐，每个产品生成：
    - 1 条父记录（货号=产品货号，父级编号=NULL）
    - 每个子件 1 条子记录（货号=子件货号，父级编号=父记录.编号）

    前端 buildOutQtyMap 通过子记录的 父级编号 反查父记录的 货号，
    仅当父记录货号等于当前产品货号时，才将子记录的 出库数量 汇总为 已出库数。
    """
    records = []
    for main in main_records:
        main_index = int(main["编号"].split("-")[-1])
        main_no = main["编号"]
        item_no = main["货号"]
        parent_bom_id = bom_parents[main_no]

        # 父记录：代表成品领料汇总
        parent_pm_id = f"TEST-PM-PARENT-{main_index:03d}"
        records.append({
            "编号": parent_pm_id,
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": item_no,
            "需求量": "0",
            "出库数量": "0",
            "分析单号": None,
            "父级编号": None,
            "来源编号": parent_bom_id,
        })

        # 子记录：代表子件已出库
        for j, child in enumerate(bom_children[main_no]):
            demand = random.randint(10, 100)
            out_qty = random.randint(0, demand)
            records.append({
                "编号": f"TEST-PM-{main_index:03d}-{j}",
                "用户编号": None,
                "用户铭": None,
                "修改状态": None,
                "创建时间": CREATE_TIME,
                "锁定用户": None,
                "审核过程": None,
                "打印": None,
                "货号": child["货号"],
                "需求量": str(demand),
                "出库数量": str(out_qty),
                "分析单号": None,
                "父级编号": parent_pm_id,
                "来源编号": child["编号"],
            })
    return records


def generate_external_bom(main_records):
    """生成外产_BOM记录。

    每个工单销控表产品生成：
    - 1 条父记录（层=0，货号=产品货号，父级编号=NULL）
    - 2 条子记录（层=1，父级编号=父记录.编号）

    前端点击“工单总数”后，通过 GetExternalProductionBOMList(货号=产品货号)
    先定位父记录，再返回其子记录作为“物料需求明细”。
    """
    records = []
    bom_parents = {}      # main_no -> parent_bom_id
    bom_children = {}     # main_no -> [child_bom_records]

    for main in main_records:
        main_index = int(main["编号"].split("-")[-1])
        main_no = main["编号"]
        item_no = main["货号"]
        product_name = main["品名"]
        spec = main["规格"]

        # 父记录：level=0，代表成品本身
        parent_id = f"TEST-BOM-PARENT-{main_index:03d}"
        bom_parents[main_no] = parent_id
        records.append({
            "编号": parent_id,
            "用户编号": None,
            "用户铭": None,
            "修改状态": None,
            "创建时间": CREATE_TIME,
            "锁定用户": None,
            "审核过程": None,
            "打印": None,
            "货号": item_no,
            "层": "0",
            "品名": product_name,
            "规格": spec,
            "关联编号": None,
            "父级编号": None,
            "用量": "1",
            "仓库名称": "成品仓",
            "仓库数": str(random.randint(0, 500)),
            "生产数": main["工单总数"],
            "分析单号": None,
            "交货日期": main["交货日期"],
            "产品属性": main["商品属性"],
            "来源": "自制",
            "单位": "个",
            "备注": None,
        })

        # 子记录：level=1，代表子件/物料
        children = []
        child_specs = ["20G5V.G1/2", "AC24V"]
        child_attrs = ["电磁阀半成品", "线圈组件"]
        child_sources = ["自制", "外购"]
        for j in range(2):
            child_id = f"TEST-BOM-CHILD-{main_index:03d}-{j}"
            child_item_no = f"{item_no}-SUB{j}"
            child_name = f"子件{j}-{product_name}"

            child_record = {
                "编号": child_id,
                "用户编号": None,
                "用户铭": None,
                "修改状态": None,
                "创建时间": CREATE_TIME,
                "锁定用户": None,
                "审核过程": None,
                "打印": None,
                "货号": child_item_no,
                "层": "1",
                "品名": child_name,
                "规格": child_specs[j],
                "关联编号": None,
                "父级编号": parent_id,
                "用量": "1",
                "仓库名称": "半成品库",
                "仓库数": str(random.randint(0, 1000)),
                "生产数": main["工单总数"],
                "分析单号": None,
                "交货日期": main["交货日期"],
                "产品属性": child_attrs[j],
                "来源": child_sources[j],
                "单位": "个",
                "备注": None,
            }
            children.append(child_record)
            records.append(child_record)

        bom_children[main_no] = children

    return records, bom_parents, bom_children


def write_batch_insert(f, table_name, columns, records, batch_size=500):
    """分批写入 INSERT 语句。"""
    if not records:
        return
    col_list = ", ".join([f"[{c}]" for c in columns])

    for i in range(0, len(records), batch_size):
        batch = records[i:i + batch_size]
        values_list = []
        for r in batch:
            vals = ", ".join([escape_sql(r.get(c)) for c in columns])
            values_list.append(f"({vals})")

        f.write(f"INSERT INTO [{table_name}] ({col_list}) VALUES\n")
        f.write(",\n".join(values_list))
        f.write(";\n\n")


def generate_sql():
    """生成完整的 SQL 脚本。"""
    random.seed(42)  # 固定随机种子，确保可复现

    main_records = generate_main_records()
    detail_records = generate_detail_records(main_records)
    scheduling_records = generate_scheduling_analysis(main_records) if INCLUDE_SCHEDULING else []
    product_records = generate_product_data(main_records) if INCLUDE_PRODUCT else []
    ep_records = generate_external_production(detail_records)
    ew_records = generate_external_warehousing(detail_records)
    es_records = generate_external_shipment(detail_records)
    bom_records, bom_parents, bom_children = generate_external_bom(main_records)
    pm_records = generate_external_pick_material(main_records, bom_parents, bom_children)

    # 列定义（按实体属性顺序，包含 ERPBase 基类字段）
    main_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "车间名称", "工单单号", "商品属性", "货号", "品名", "规格", "工单总数", "已入库数",
        "在产数量", "齐套", "配料", "交货日期", "分析日期", "生产完成率", "层", "排产用户"
    ]

    detail_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "品名", "规格", "工单单号", "排产编号", "交货日期", "生产数", "入库数",
        "待产数", "父级编号", "分析单号", "排产用户"
    ]

    scheduling_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "分析单号", "分析人", "分析日期", "生产方式", "客户简称", "排产编号"
    ]

    product_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "中文品名", "中文规格", "产品类别", "工序名称", "生产车间", "产品属性",
        "制造方式", "数量单位", "停用"
    ]

    ep_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "排产编号", "需求量", "生产数量", "分析单号", "工单单号", "来源", "工序车间",
        "工序", "工单层级", "电压", "线圈", "订单数", "单位", "仓库名称", "备注", "用量"
    ]

    ew_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "需求量", "入库数量", "分析单号", "工单单号"
    ]

    es_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "排产编号", "需求量", "发运数量", "分析单号"
    ]

    pm_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "需求量", "出库数量", "分析单号", "父级编号", "来源编号"
    ]

    bom_columns = [
        "编号", "用户编号", "用户铭", "修改状态", "创建时间", "锁定用户", "审核过程", "打印",
        "货号", "层", "品名", "规格", "关联编号", "父级编号", "用量", "仓库名称", "仓库数",
        "生产数", "分析单号", "交货日期", "产品属性", "来源", "单位", "备注"
    ]

    with open(OUTPUT_SQL, "w", encoding="utf-8") as f:
        f.write("-- 工单销控表测试数据\n")
        f.write(f"-- 生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write("-- 数据量: 500 条工单销控表 + 约 1500 条明细 + 外产链路（精简:不含排产分析单/产品资料）\n")
        f.write("-- 外产_BOM: 500 条父记录 + 1000 条子记录，用于前端物料需求明细展开\n")
        f.write("-- 所有测试数据使用 TEST_ 前缀，便于识别和清理\n\n")
        # 工单销控表/明细 上的唯一索引为筛选索引(WHERE [货号] IS NOT NULL)，
        # 要求会话开启 QUOTED_IDENTIFIER / ANSI_NULLS 等选项(EF Core 默认开启)。
        f.write("SET QUOTED_IDENTIFIER ON;\n")
        f.write("SET ANSI_NULLS ON;\n")
        f.write("SET ANSI_PADDING ON;\n")
        f.write("SET ANSI_WARNINGS ON;\n")
        f.write("SET ARITHABORT ON;\n")
        f.write("SET CONCAT_NULL_YIELDS_NULL ON;\n")
        f.write("SET NUMERIC_ROUNDABORT OFF;\n")
        f.write("SET XACT_ABORT ON;\n")
        # 筛选索引要求会话开启 QUOTED_IDENTIFIER / ANSI_NULLS 等(EF Core 默认开启)
        f.write("SET NOCOUNT ON;\n")
        f.write("BEGIN TRANSACTION;\n\n")

        write_batch_insert(f, "工单销控表", main_columns, main_records)
        write_batch_insert(f, "工单销控表明细", detail_columns, detail_records)
        if INCLUDE_SCHEDULING:
            write_batch_insert(f, "排产分析单", scheduling_columns, scheduling_records)
        if INCLUDE_PRODUCT:
            write_batch_insert(f, "产品资料", product_columns, product_records)
        write_batch_insert(f, "外产_生产", ep_columns, ep_records)
        write_batch_insert(f, "外产_入库", ew_columns, ew_records)
        write_batch_insert(f, "外产_发运", es_columns, es_records)
        write_batch_insert(f, "外产_领料", pm_columns, pm_records)
        write_batch_insert(f, "外产_BOM", bom_columns, bom_records)

        f.write("COMMIT TRANSACTION;\n\n")

        # 验证查询
        f.write("-- 验证插入结果\n")
        f.write("SELECT '工单销控表' AS 表名, COUNT(*) AS 条数 FROM [工单销控表] WHERE [货号] LIKE 'TEST-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '工单销控表明细', COUNT(*) FROM [工单销控表明细] WHERE [货号] LIKE 'TEST-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_生产', COUNT(*) FROM [外产_生产] WHERE [货号] LIKE 'TEST-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_入库', COUNT(*) FROM [外产_入库] WHERE [编号] LIKE 'TEST-DET-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_发运', COUNT(*) FROM [外产_发运] WHERE [货号] LIKE 'TEST-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_领料(父)', COUNT(*) FROM [外产_领料] WHERE [编号] LIKE 'TEST-PM-PARENT-%'\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_领料(子)', COUNT(*) FROM [外产_领料] WHERE [编号] LIKE 'TEST-PM-%' AND [父级编号] IS NOT NULL\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_BOM(父)', COUNT(*) FROM [外产_BOM] WHERE [货号] LIKE 'TEST-HH-%' AND [父级编号] IS NULL\n")
        f.write("UNION ALL\n")
        f.write("SELECT '外产_BOM(子)', COUNT(*) FROM [外产_BOM] WHERE [父级编号] LIKE 'TEST-BOM-PARENT-%';\n")

    # 生成回滚脚本
    with open(ROLLBACK_SQL, "w", encoding="utf-8") as f:
        f.write("-- 按 TEST_ 前缀清理测试数据\n")
        f.write("-- 注意：请确认目标数据库后再执行！\n\n")
        # 筛选索引要求会话开启 QUOTED_IDENTIFIER / ANSI_NULLS 等(EF Core 默认开启)
        f.write("SET QUOTED_IDENTIFIER ON;\n")
        f.write("SET ANSI_NULLS ON;\n")
        f.write("SET ANSI_PADDING ON;\n")
        f.write("SET ANSI_WARNINGS ON;\n")
        f.write("SET ARITHABORT ON;\n")
        f.write("SET CONCAT_NULL_YIELDS_NULL ON;\n")
        f.write("SET NUMERIC_ROUNDABORT OFF;\n")
        f.write("SET XACT_ABORT ON;\n")
        f.write("SET NOCOUNT ON;\n")
        f.write("BEGIN TRANSACTION;\n\n")
        f.write("DELETE FROM [外产_BOM] WHERE [货号] LIKE 'TEST-%';\n")
        f.write("DELETE FROM [外产_领料] WHERE [编号] LIKE 'TEST-DET-%';\n")
        f.write("DELETE FROM [外产_发运] WHERE [货号] LIKE 'TEST-%';\n")
        f.write("DELETE FROM [外产_入库] WHERE [编号] LIKE 'TEST-DET-%';\n")
        f.write("DELETE FROM [外产_生产] WHERE [货号] LIKE 'TEST-%';\n")
        f.write("DELETE FROM [工单销控表明细] WHERE [货号] LIKE 'TEST-%';\n")
        f.write("DELETE FROM [工单销控表] WHERE [货号] LIKE 'TEST-%';\n")
        if INCLUDE_SCHEDULING:
            f.write("DELETE FROM [排产分析单] WHERE [分析单号] LIKE 'TEST-FX-%';\n")
        if INCLUDE_PRODUCT:
            f.write("DELETE FROM [产品资料] WHERE [货号] LIKE 'TEST-%';\n\n")
        f.write("COMMIT TRANSACTION;\n")

    print(f"已生成 SQL 脚本: {OUTPUT_SQL}")
    print(f"已生成回滚脚本: {ROLLBACK_SQL}")
    print(f"工单销控表: {len(main_records)} 条")
    print(f"工单销控表明细: {len(detail_records)} 条")
    print(f"排产分析单: {len(scheduling_records)} 条")
    print(f"产品资料: {len(product_records)} 条")
    print(f"外产_生产: {len(ep_records)} 条")
    print(f"外产_入库: {len(ew_records)} 条")
    print(f"外产_发运: {len(es_records)} 条")
    print(f"外产_领料: {len(pm_records)} 条")
    print(f"外产_BOM: {len(bom_records)} 条")


if __name__ == "__main__":
    generate_sql()
