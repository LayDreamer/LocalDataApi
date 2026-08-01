namespace LocalDataApi.Dto
{
    public class ReturnDeliveryReviewRequestDto
    {
        public string ReviewId { get; set; } = string.Empty;
    }

    public class ReturnDeliveryReviewResultDto
    {
        public string ReviewId { get; set; } = string.Empty;
        public string SchedulingNo { get; set; } = string.Empty;
        public List<string> AnalysisNos { get; set; } = new();
        public int ReviewDeletedCount { get; set; }
        public int SchedulingAnalysisDeletedCount { get; set; }
        public int BomDeletedCount { get; set; }
        public int WorkOrderDetailDeletedCount { get; set; }
        public int PickMaterialDeletedCount { get; set; }
        public int WarehousingDeletedCount { get; set; }
        public int ProductionDeletedCount { get; set; }
        public int ShipmentDeletedCount { get; set; }
        public int WorkOrderUpdatedCount { get; set; }
        public int WorkOrderDeletedCount { get; set; }
    }
}
