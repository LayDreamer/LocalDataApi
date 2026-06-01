namespace LocalDataApi.Dto
{
    public class ScanWarehousingDto
    {
        /// <summary>扫码结果（货号/条码等）</summary>
        public string? 扫码内容 { get; set; }

        /// <summary>本次入库数量，默认1</summary>
        public string? 入库数量 { get; set; } = "1";

        /// <summary>仓库名称（如：零件仓库）</summary>
        public string? 仓库名 { get; set; }

        /// <summary>备注</summary>
        public string? 备注 { get; set; }
    }
}
