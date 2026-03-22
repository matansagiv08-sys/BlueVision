namespace Server.Models
{
    public class TaskBoardRow
    {
        public int WorkOrderID { get; set; }
        public string ProductionItemID { get; set; }
        public int SerialNumber { get; set; }
        public string PlaneTypeName { get; set; }
        public int PlannedQty { get; set; }

        // רשימה של הסטטוסים של התחנות עבור הפריט הזה
        public List<StageStatusDTO> Stages { get; set; } = new List<StageStatusDTO>();
    }

    public class StageStatusDTO
    {
        public string StageName { get; set; }
        public string StatusName { get; set; }
    }
}
