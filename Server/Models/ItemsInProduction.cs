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

        public double Progress
        {
            get
            {
                if (Stages == null || Stages.Count == 0) return 0;
                double doneStagesCount = Stages.Count(s => s.Status != null && s.Status.ProductionStatusID == 4);
                return (doneStagesCount / Stages.Count) * 100;
            }
        }

        // מאפיין עזר 
        public bool IsFullyDone => Progress >= 100;

        //  מאפיין מחושב למציאת התחנה הנוכחית
        public ProductionItemStage CurrentStage
        {
            get
            {
                if (Stages == null || Stages.Count == 0) return null;

                var activeStage = Stages.OrderBy(s => s.Stage.ProductionStageID)
                                        .FirstOrDefault(s => s.Status != null && s.Status.ProductionStatusID != 4);

                return activeStage ?? Stages.OrderByDescending(s => s.Stage.ProductionStageID).FirstOrDefault();
            }
        }

        public List<ItemInProduction> GetBoardData()
        {
            DBservices dbs = new DBservices();
            return dbs.GetTasksBoard(); 
        }

    }
}
