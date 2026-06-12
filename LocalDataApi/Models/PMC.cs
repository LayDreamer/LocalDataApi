﻿﻿﻿﻿﻿﻿using System.ComponentModel.DataAnnotations.Schema;
using static SKIT.FlurlHttpClient.Wechat.Work.Models.CgibinAgentBatchSetWorkbenchDataRequest.Types;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外销合同产品
    /// </summary>
    public class PMCProductInfo : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 层 { get; set; }
        public string? 货号 { get; set; }
        public string? 中文品名 { get; set; }
        public string? 中文规格 { get; set; }
        public string? 父编号 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 来源编号 { get; set; }
        public string? 来源 { get; set; }
        public string? 工单单号 { get; set; }
        public string? 线圈 { get; set; }
        public string? 电压 { get; set; }
        public string? 交货日期 { get; set; }
        public string? 排产用户 { get; set; }
        public string? 状态 { get; set; }

        public string? 数量 { get; set; }

        public string? 发运数量 { get; set; }

        public string? 入库数量 { get; set; }

        public string? 在产需求量 { get; set; }
    }

    /// <summary>
    /// 外销合同客户产品
    /// </summary>
    public class PMCUserProductInfo : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 货号 { get; set; }
        public string? 数量 { get; set; }
        public string? 数量单位 { get; set; }
        public string? 金额 { get; set; }
        public string? 合同单价 { get; set; }
        public string? 中文品名 { get; set; }
        public string? 中文规格 { get; set; }
        public string? 电压 { get; set; }       
        public string? 序号 { get; set; } 
        public string? 货好日期 { get; set; }     
    }


    public class PMCBasicInfo : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 合同状态 { get; set; }
        public string? 客户公司 { get; set; }
        public string? 签订日期 { get; set; }
        public string? 交货日期 { get; set; }
        public string? 业务员 { get; set; }

    }

    /// <summary>
    /// 产品资料
    /// </summary>
    public class ProductData : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 中文品名 { get; set; }
        public string? 中文规格 { get; set; }
        public string? 产品类别 { get; set; }
        public string? 工序名称 { get; set; }
        public string? 生产车间 { get; set; }
        public string? 产品属性 { get; set; }
        public string? 制造方式 { get; set; }//作为来源依据
        public string? 数量单位 { get; set; }//作为单位依据
        public string? 停用 { get; set; }
    }

    //产品资料装配
    public class ProductDataAssembly : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 创建日期 { get; set; }
        public string? 创建人 { get; set; }

    }

    //产品资料装配清单
    public class ProductDataAssemblyList : ERPBase
    {
        public string? 主编号 { get; set; }

        public string? 货号 { get; set; }
        public string? 主货号 { get; set; }

        public string? 用量 { get; set; }

        public string? 单位 { get; set; }

        public string? 来源 { get; set; }
    }

    //仓库货品
    public class WarehouseGoods : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 品名 { get; set; }
        public string? 规格 { get; set; }
        public string? 数量 { get; set; }
        public string? 单位 { get; set; }

        public string? 单价 { get; set; }

        public string? 金额 { get; set; }
        public string? 订单号 { get; set; }

        public string? 仓库名 { get; set; }

        public string? 商品属性 { get; set; }
        public string? 库存上限 { get; set; }
        public string? 库存下限 { get; set; }
        public string? 来源 { get; set; }
    }


    //在产需求量
    public class ProductionDemand
    {
        public string? 货号 { get; set; }
        public string? 成品货号 { get; set; }
        public string? 排产编号 { get; set; }
        public double? 需求量 { get; set; }
    }

    //在途数
    public class InTransitQuantity
    {
        public string? 货号 { get; set; }
        public string? 成品货号 { get; set; }
        public string? 排产编号 { get; set; }
        public double? 在产量 { get; set; }
    }

    //外产_订单(信息交期评审)
    public class PMCDeliveryReview : ERPBase
    {
        //合同号
        public string? 合同号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 数量 { get; set; }
        public string? 货号 { get; set; }
        public string? 中文品名 { get; set; }
        public string? 中文规格 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 来源编号 { get; set; }
        public string? 来源 { get; set; }
        public string? 工单单号 { get; set; }
        public string? 线圈货号 { get; set; }
        public string? 电压 { get; set; }
        public string? 交货日期 { get; set; }
        
        public string? 排产用户 { get; set; }
        public string? 状态 { get; set; } //待评审,评审通过,评审驳回
        public string? 物料货号 { get; set; }
        public string? 备注 { get; set; }
    }

    //排产分析单
    public class SchedulingAnalysis : ERPBase
    {
        public string? 分析单号 { get; set; }
        public string? 分析人 { get; set; }
        public string? 分析日期 { get; set; }
        public string? 生产方式 { get; set; }
        public string? 客户简称 { get; set; }
        public string? 排产编号 { get; set; }
    }

    //产品销控表
    public class PMCSalesControl : ERPBase
    {
        //合同号
        public string? 合同号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 层 { get; set; }
        public string? 货号 { get; set; }
        public string? 中文品名 { get; set; }
        public string? 中文规格 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 父级货号 { get; set; }
        public string? 物料货号 { get; set; }
        public string? 订单总需求 { get; set; }

        public string? 仓库数 { get; set; }

        public string? 在产数 { get; set; }
        public string? 初始可用量 { get; set; }

        public string? 缺量 { get; set; }
        public string? 交货计划 { get; set; }

        public string? 商品属性 { get; set; }
    }

    //交货计划
    public class DeliveryPlan : ERPBase
    {
        public string? 合同号 { get; set; }
        
        public string? 交货日期 { get; set; }

        public string? 交货数量 { get; set; }

        public string? 发运数量 { get; set; }
        public string? 待发数量 { get; set; }
        public string? 状态 { get; set; }

        public string? 排产用户 { get; set; }

        public string? 销控编号 { get; set; }   
    }   
    
    /// <summary>
    /// 工单销控表
    /// </summary>
    public class WorkOrderSalesControl : ERPBase
    {
        public string? 车间名称 { get; set; }
        public string? 商品属性 { get; set; }
        public string? 货号 { get; set; }
        public string? 品名 { get; set; }
        public string? 规格 { get; set; }
        public string? 工单总数 { get; set; }
        public string? 已入库数 { get; set; }
        public string? 在产数量 { get; set; }
        public string? 齐套 { get; set; }
        public string? 配料 { get; set; }
        public string? 分析日期 { get; set; }
        public string? 生产完成率 { get; set; }
        public string? 交货计划 { get; set; }
        public string? 层 { get; set; }
    }

    /// <summary>
    /// 工单销控表明细
    /// </summary>
    public class WorkOrderSalesControlDetail : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 品名 { get; set; }
        public string? 规格 { get; set; }
        public string? 用量 { get; set; }
        public string? 需求数 { get; set; }
        public string? 已出库数 { get; set; }
        public string? 缺料数 { get; set; }
        public string? 仓库名称 { get; set; }
        public string? 仓库数 { get; set; }
        public string? 仓库缺料 { get; set; }
        public string? 父级编号 { get; set; }
    }   

    /// <summary>
    /// 成品销控表明细
    /// </summary>
    public class ProductSalesControlDetail : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 业务员 { get; set; }
        public string? 交货日期 { get; set; }
        public string? 订单数量 { get; set; }
        public string? 已发数量 { get; set; }
        public string? 待发数量 { get; set; }
        public string? 状态 { get; set; }
        public string? 货号 { get; set; }
        public string? 品名 { get; set; }
        public string? 规格 { get; set; }
        public string? 父级编号 { get; set; }
    }
}