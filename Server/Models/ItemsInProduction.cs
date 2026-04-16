using Server.DAL;
using System;
using System.Collections.Generic;
using System.Linq;

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
            if (Progress >= 100) return double.MaxValue;

            double remainingWorkHours = GetRemainingWorkHours();

            if (PlaneID?.Project?.DueDate == null)
            {
                return 9999 + remainingWorkHours;
            }

            // חישוב שעות עבודה נטו (במקום שעות קלנדריות)
            double netWorkHoursUntilDeadline = CalculateNetWorkHours(DateTime.Now, PlaneID.Project.DueDate.Value);

            // Slack Time ריאליסטי: שעות עבודה שנותרו בלו"ז פחות שעות עבודה שנדרשות לייצור
            double slackTime = netWorkHoursUntilDeadline - remainingWorkHours;

            int priorityWeight = (PlaneID.Project.PriorityLevel == 1) ? -18 : 0; // עדיפות פרויקט שווה ערך ליומיים עבודה

            return slackTime + priorityWeight;
        }

        // פונקציית עזר לחישוב שעות עבודה נטו (9 שעות ביום, ללא סופי שבוע)
        private double CalculateNetWorkHours(DateTime start, DateTime end)
        {
            if (end < start) return 0;

            int workDays = 0;
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                // אם זה לא שישי או שבת (בהנחה שעובדים א-ה)
                if (date.DayOfWeek != DayOfWeek.Friday && date.DayOfWeek != DayOfWeek.Saturday)
                {
                    workDays++;
                }
            }

            // חישוב גס: ימי עבודה כפול 9 שעות
            // הערה: לדיוק מירבי אפשר להפחית את השעות שכבר עברו היום
            double totalWorkHours = workDays * 9.0;

            return totalWorkHours;
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

        public List<ItemInProduction> SortItemsByUrgency(List<ItemInProduction> items)
        {
            return items.OrderBy(item =>
            {
                var currentStage = item.Stages.FirstOrDefault(s => s.Status != null && s.Status.ProductionStatusID != 4);
                if (currentStage?.ManualPriority != null)
                {
                    return (double)currentStage.ManualPriority.Value - 10000;
                }
                return item.GetUrgencyScore();
            }).ToList();
        }
    } // סיום מחלקת ItemInProduction

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
} // סיום ה-Namespace