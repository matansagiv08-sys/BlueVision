using Server.DAL;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Models
{
    public class ItemInProduction
    {
        private const double ProjectDueDateWeight = 0.35;
        private const double ItemDueDateWeight = 0.25;
        private const double ItemPriorityWeight = 0.25;
        private const double ProjectPriorityWeight = 0.15;
        private const int DefaultPriorityLevel = 3;
        private const int MinPriorityLevel = 1;
        private const int MaxPriorityLevel = 5;

        public int SerialNumber { get; set; }
        public ProductionItem ProductionItem { get; set; }
        public Plane PlaneID { get; set; }
        public int PriorityLevel { get; set; }
        public int WorkOrderID { get; set; }
        public string? ProjectName { get; set; }
        public string TailNumber { get; set; } = string.Empty;
        public DateTime? ItemDueDate { get; set; }
        public int PlannedQty { get; set; }
        public string Comments { get; set; }
        public List<ProductionItemStage> Stages { get; set; } = new List<ProductionItemStage>();

        public ItemInProduction() { }

        // מחשב כמה שעות עבודה נותרו לפריט על בסיס זמני היעד של התחנות שטרם בוצעו
        public double GetRemainingWorkHours()
        {
            if (Stages == null || Stages.Count == 0) return 0;

            return Stages
                .Where(s => s.Status == null || s.Status.ProductionStatusID != 4)
                .Sum(s => s.Stage != null ? s.Stage.TargetDuration.TotalHours : 1.0);
        }

        // מחשב את ציון הדחיפות של הפריט
        public double GetUrgencyScore()
        {
            if (IsFullyDone)
            {
                return 0;
            }

            double projectDueDateScore = CalculateDateUrgencyScore(PlaneID?.Project?.DueDate);
            double itemDueDateScore = CalculateDateUrgencyScore(ItemDueDate);
            double itemPriorityScore = NormalizePriorityScore(PriorityLevel);
            double projectPriorityScore = NormalizePriorityScore(PlaneID?.Project?.PriorityLevel ?? DefaultPriorityLevel);

            return (projectDueDateScore * ProjectDueDateWeight)
                 + (itemDueDateScore * ItemDueDateWeight)
                 + (itemPriorityScore * ItemPriorityWeight)
                 + (projectPriorityScore * ProjectPriorityWeight);
        }

        private static double CalculateDateUrgencyScore(DateTime? dueDate)
        {
            if (!dueDate.HasValue)
            {
                return 0;
            }

            double daysUntilDue = (dueDate.Value.Date - DateTime.Today).TotalDays;
            double clampedDays = Math.Max(daysUntilDue, 0);
            return 1.0 / (1.0 + clampedDays);
        }

        private static double NormalizePriorityScore(int priorityLevel)
        {
            int safePriority = Math.Max(MinPriorityLevel, Math.Min(MaxPriorityLevel, priorityLevel));
            return 1.0 / safePriority;
        }


        public double Progress
        {
            get
            {
                if (Stages == null || Stages.Count == 0) return 0;
                double doneStagesCount = Stages.Count(s => s.Status != null && s.Status.ProductionStatusID == 4);
                return (doneStagesCount / Stages.Count) * 100;
            }
        }

        public bool IsFullyDone => Progress >= 100;

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

        public double CalculatedScore
        {
            get
            {
                return GetUrgencyScore();
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

            if (serial <= 0 || string.IsNullOrWhiteSpace(itemID) || stageID <= 0 || statusID <= 0)
            {
                return 0;
            }

            DBservices dbs = new DBservices();
            return dbs.UpdateStageStatus(serial, itemID, stageID, statusID, comment, userTime, resetFuture);
        }

        public List<ItemInProduction> SortItemsByUrgency(List<ItemInProduction> items)
        {
            return items
                .Select(item => new
                {
                    Item = item,
                    Score = item.GetUrgencyScore(),
                    ItemDueDate = item.ItemDueDate ?? DateTime.MaxValue,
                    WorkOrderID = item.WorkOrderID <= 0 ? int.MaxValue : item.WorkOrderID,
                    item.SerialNumber
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.ItemDueDate)
                .ThenBy(x => x.WorkOrderID)
                .ThenBy(x => x.SerialNumber)
                .Select(x => x.Item)
                .ToList();
        }
    }

    public class InsertItemInProductionRequest
    {
        public string? ProjectName { get; set; }
        public string? PlaneID { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ProjectDueDate { get; set; }
        public string? ProductionItemID { get; set; }
        public string? WorkOrderID { get; set; }
        public int? SerialNumber { get; set; }
        public int? PlaneTypeID { get; set; }
        public int? PriorityID { get; set; }
        public int? ProjectPriorityLevel { get; set; }
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
} // סיום ה-Namespace
