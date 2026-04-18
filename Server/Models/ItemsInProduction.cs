using Server.DAL;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Models
{
    public class ItemInProduction
    {
        //משקלים עבור לוגיקת סדר העבודה 
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
        //סימן השאלה קיים כדי שהמערכת תוכל לקחת ערך ריק ולא תכניס תאריך שיהרוס את האלגוריתם
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

        //ערך מחושב שנותן את התחנה הנוכחית שפריט נמצא בה כרגע
        public ProductionItemStage CurrentStage
        {
            get
            {
                //בדיקה האם באמת קיימים שלבים לפריט
                if (Stages == null || Stages.Count == 0) return null;
                //סידור התחנות לפי סדר העבודה, ולוקחת את התחנה הראשונה שעונה על התנאי
                //התנאי: עצירה בתחנה הראשונה שהיא לא בסטטוס "בוצע" - 4
                var activeStage = Stages.OrderBy(s => s.Stage.StageOrder)
                                        .FirstOrDefault(s => s.Status != null && s.Status.ProductionStatusID != 4);
                //אם קיים ערך במשתנה שאינו ריק - תחזיר אותו, אם לא אז הפריט מוכן לכן נחזיר את התחנה האחרונה
                return activeStage ?? Stages.OrderByDescending(s => s.Stage.StageOrder).FirstOrDefault();
            }
        }

        public double CalculatedScore
        {
            get
            {
                return GetUrgencyScore();
            }
        }

        //שליפת נתוני לוח משימות וניהול סדר עבודה
        public List<ItemInProduction> GetBoardData()
        {
            DBservices dbs = new DBservices();
            return dbs.GetTasksBoard();
        }

        //שליפת נתונים קיימים עבור טופס הוספת פריט חדש
        public object GetInitialFormData()
        {
            DBservices dbs = new DBservices();
            return new
            {
                productionItems = dbs.GetProductionItems(),
                projects = dbs.GetProjects(),
                planeTypes = dbs.GetPlaneTypes(),
                existingWorkOrders = dbs.GetUniqueWorkOrders(),
                priorities = dbs.GetPriorityLevels(),
                planes = dbs.GetPlanes()
            };
        }

        //הוספת פריט לייצור
        public int InsertItem(InsertItemInProductionRequest? itemData)
        {
            DBservices dbs = new DBservices();
            return dbs.InsertItemInProduction(itemData ?? new InsertItemInProductionRequest());
        }

        //עדכון סטטוס לפריט בתחנה
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
            var initialSort = items
            .Select(item => new
            {
                Item = item,
                Score = item.GetUrgencyScore(),
                ItemDueDate = item.ItemDueDate ?? DateTime.MaxValue
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ItemDueDate)
            .ThenBy(x => x.Item.WorkOrderID)
            .ThenBy(x => x.Item.SerialNumber)
            .Select(x => x.Item)
            .ToList();

            // 2. זיהוי פריטים שיש להם מיקום ידני (ManualPriority)
            // אנחנו מניחים שבשלב זה לכל פריט יש Stage נוכחי ב-CurrentStage
            var manualItems = initialSort
                .Where(i => i.CurrentStage != null && i.CurrentStage.ManualPriority.HasValue && i.CurrentStage.ManualPriority > 0)
                .OrderBy(i => i.CurrentStage.ManualPriority)
                .ToList();

            // 3. הסרת הפריטים הידניים מהרשימה הכללית כדי שנוכל לשבץ אותם מחדש
            foreach (var item in manualItems)
            {
                initialSort.Remove(item);
            }

            // 4. שיבוץ מחדש במיקום המדויק
            foreach (var item in manualItems)
            {
                // המשתמש קבע מקום 1, 2, 3... ב-List זה אינדקס 0, 1, 2...
                int targetIndex = item.CurrentStage.ManualPriority.Value - 1;

                // הגנה: אם המיקום גבוה ממספר הפריטים, שים בסוף
                if (targetIndex >= initialSort.Count)
                {
                    initialSort.Add(item);
                }
                else
                {
                    initialSort.Insert(Math.Max(0, targetIndex), item);
                }
            }

            return initialSort;
        }
    }

    //DTO 
    //מחלקת עזר להוספת פריט חדש לייצור
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


    //DTO
    //מחלקת עזר לעריכת סטטוס לפריט בתחנה
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
