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

        public ItemsInProductionInitialFormData GetInitialFormData()
        {
            DBservices dbs = new DBservices();
            return new ItemsInProductionInitialFormData
            {
                ProductionItems = dbs.GetProductionItems(),
                Projects = dbs.GetProjects(),
                PlaneTypes = dbs.GetPlaneTypes(),
                ExistingWorkOrders = dbs.GetUniqueWorkOrders(),
                Priorities = dbs.GetPriorityLevels(),
                Planes = dbs.GetPlanes()
            };
        }

        public int InsertItem(InsertItemInProductionRequest? itemData)
        {
            DBservices dbs = new DBservices();
            return dbs.InsertItemInProduction(itemData ?? new InsertItemInProductionRequest());
        }

        public int UpdateStatus(UpdateProductionStatusRequest? data)
        {
            int serial = data?.SerialNumber ?? 0;
            string? itemID = data?.ProductionItemID;
            int stageID = data?.ProductionStageID ?? 0;
            int statusID = data?.ProductionStatusID ?? 0;
            string? comment = data?.Comment;
            DateTime? userTime = data?.UserTime;
            bool resetFuture = data?.ResetFuture ?? false;

            DBservices dbs = new DBservices();
            return dbs.UpdateStageStatus(serial, itemID, stageID, statusID, comment, userTime, resetFuture);
        }

    }

    public class InsertItemInProductionRequest
    {
        public string? ProjectName { get; set; }
        public string? PlaneID { get; set; }
        public string? ProductionItemID { get; set; }
        public string? WorkOrderID { get; set; }
        public int? SerialNumber { get; set; }
        public int? PlaneTypeID { get; set; }
        public int? PriorityID { get; set; }
        public int? Quantity { get; set; }
        public string? Comments { get; set; }
    }

    public class UpdateProductionStatusRequest
    {
        public int? SerialNumber { get; set; }
        public string? ProductionItemID { get; set; }
        public int? ProductionStageID { get; set; }
        public int? ProductionStatusID { get; set; }
        public string? Comment { get; set; }
        public DateTime? UserTime { get; set; }
        public bool? ResetFuture { get; set; }
    }
}
