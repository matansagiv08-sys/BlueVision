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
            // הגנה: אם הפריט הסתיים, הוא עובר לסוף
            if (Stages == null || !Stages.Any(s => s.Status == null || s.Status.ProductionStatusID != 4))
                return double.MaxValue;

            // 1. חישוב שעות עבודה שנותרו (סיכום TargetDuration של תחנות עתידיות)
            double remainingWorkHours = GetRemainingWorkHours();
            if (remainingWorkHours == 0) remainingWorkHours = 1; // מניעת חילוק ב-0

            // 2. חישוב זמן קלנדרי נותר בימי עבודה (9 שעות ביום)
            // שים לב לנתיב: PlaneID -> Project -> DueDate
            DateTime? dueDate = PlaneID?.Project?.DueDate;

            if (dueDate == null) return 0; // אם אין תאריך יעד, אין ציון (זו כנראה הסיבה ל-0)

            double hoursUntilDue = (dueDate.Value - DateTime.Now).TotalHours;
            // המרה לימי עבודה זמינים (לפי 9 שעות עבודה ביום)
            double availableWorkHours = (hoursUntilDue / 24) * 9;

            // 3. חישוב מדד ה-Critical Ratio
            double criticalRatio = remainingWorkHours / Math.Max(availableWorkHours, 0.5);

            // 4. שקלול שלוש רמות העדיפות (Hierarchy Weights)
            // אנחנו הופכים את ה-PriorityLevel (שבו 1 הוא הכי גבוה) למשקולת חיובית
            double projectWeight = (6 - (PlaneID?.Project?.PriorityLevel ?? 3)) * 50; // משקל גבוה לפרויקט
            double planeWeight = (6 - (PlaneID?.PriorityLevel ?? 3)) * 20;            // משקל בינוני למטוס
            double itemWeight = (6 - PriorityLevel) * 10;                            // משקל לחלק עצמו

            // 5. לוגיקת "איחור וודאי" (Hard Constraint)
            if (DateTime.Now > dueDate.Value)
            {
                // ציון ענק ששומר על היחס בין המאחרים
                return 200000 + (remainingWorkHours * 10) + projectWeight + planeWeight;
            }

            // הציון הסופי המשלב דחיפות זמן ועדיפות ניהולית
            return (criticalRatio * 100) + projectWeight + planeWeight + itemWeight;
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

            DBservices dbs = new DBservices();
            return dbs.UpdateStageStatus(serial, itemID, stageID, statusID, comment, userTime, resetFuture);
        }

        public List<ItemInProduction> SortItemsByUrgency(List<ItemInProduction> items)
        {
            return items.OrderBy(item =>
            {
                var currentStage = item.Stages.FirstOrDefault(s => s.Status != null && s.Status.ProductionStatusID != 4);

                // 1. עוגן ידני (המשתמש קבע מיקום 5 - הוא נשאר ב-5)
                if (currentStage?.ManualPriority != null && currentStage.ManualPriority > 0)
                    return (double)currentStage.ManualPriority.Value - 1000000;

                // 2. אם אין ידני - האלגוריתם מחשב לפי הסדר:
                // א. עדיפות פרויקט
                // ב. עדיפות מטוס
                // ג. ציון דחיפות (זמן/עבודה)
                return item.GetUrgencyScore();
            }).ToList();
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
} // סיום ה-Namespace