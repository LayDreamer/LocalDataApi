using System.ComponentModel.DataAnnotations;

namespace LocalDataApi.Domain.Blf
{
    public class BLFParameter
    {
        public Guid Id { get; set; }

        //工单号
        public string? WorkOrderNumber { get; set; }
        //比例阀编号
        public string? BLFNumber { get; set; }
        //线圈电阻
        public string? CoilResistance { get; set; }
        //绝缘电阻
        public string? InsulationResistance { get; set; }
        //绝缘强度
        public string? InsulationStrength { get; set; }
        //耐压强度
        public string? WithstandVoltageStrength { get; set; }
        //内泄露
        public string? InternalLeakage { get; set; }
        //外泄漏
        public string? ExternalLeakage { get; set; }
        //电流流量曲线
        public List<CurrentFlowRate>? CurrentFlowRateCurve { get; set; }
        //起始电流
        public string? StartingCurrent { get; set; }
        //最大流量
        public string? MaximumFlowRate { get; set; }
        //滞回
        public string? Hysteresis { get; set; }
        //压力流量曲线
        public List<PressureFlowRate>? PressureFlowRateCurve { get; set; }
        //闭环波动0.5%
        public string? ClosedLoopFluctuation1 { get; set; }
        //闭环波动25%
        public string? ClosedLoopFluctuation2 { get; set; }
        //闭环波动75%
        public string? ClosedLoopFluctuation3 { get; set; }
        //闭环波动100%
        public string? ClosedLoopFluctuation4 { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime ModifyDate { get; set; }
    }

    /// <summary>
    /// 电流流量
    /// </summary>
    public class CurrentFlowRate
    {
        public Guid Id { get; set; }
        public float Current { get; set; }
        public float FlowRate { get; set; }
    }

    /// <summary>
    /// 压力流量
    /// </summary>
    public class PressureFlowRate
    {
        public Guid Id { get; set; }
        public float Pressure { get; set; }
        public float FlowRate { get; set; }
    }

}
