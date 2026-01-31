using LocalDataApi.Models;

namespace LocalDataApi.Dto
{
    public class BLFParameterCreateDto
    {
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
        public List<CurrentFlowRateCreateDto>? CurrentFlowRateCurve { get; set; }
        //起始电压
        public string? StartingVoltage { get; set; }
        //最大流量
        public string? MaximumFlowRate { get; set; }
        //滞回
        public string? Hysteresis { get; set; }
        //压力流量曲线
        public List<PressureFlowRateCreateDto>? PressureFlowRate { get; set; }
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
}
