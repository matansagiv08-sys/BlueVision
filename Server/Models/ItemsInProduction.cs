using Server.DAL;
using System.Text.Json.Nodes;

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

        public object GetInitialFormData()
        {
            DBservices dbs = new DBservices();
            return new
            {
                ProductionItems = dbs.GetProductionItems(),
                Projects = dbs.GetProjects(),
                PlaneTypes = dbs.GetPlaneTypes(),
                ExistingWorkOrders = dbs.GetUniqueWorkOrders(),
                Priorities = dbs.GetPriorityLevels(),
                Planes = dbs.GetPlanes()
            };
        }

        public int InsertItem(JsonObject itemData)
        {
            DBservices dbs = new DBservices();
            return dbs.InsertItemInProduction(itemData);
        }

        public int UpdateStatus(int serial, string itemID, int stageID, int statusID, string comment, DateTime? userTime)
        {
            DBservices dbs = new DBservices();
            return dbs.UpdateStageStatus(serial, itemID, stageID, statusID, comment, userTime);
        }

    }
}
