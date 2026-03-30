using Server.DAL;

namespace Server.Models
{
    public class ItemInProduction
    {
        public int SerialNumber { get; set; }
        public ProductionItem ProductionItem { get; set; } 
        public Plane PlaneID { get; set; }
        public int PriorityLevel { get; set; }
        public int WorkOrderID { get; set; }
        public int PlannedQty { get; set; }
        public string Comments { get; set; }
        public List<ProductionItemStage> Stages { get; set; } = new List<ProductionItemStage>();

        public ItemInProduction() { }

        public List<ItemInProduction> GetBoardData()
        {
            DBservices dbs = new DBservices();
            return dbs.GetTasksBoard(); 
        }

    }
}
