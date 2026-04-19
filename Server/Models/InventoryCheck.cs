using System.Diagnostics;
using Server.DAL;

namespace Server.Models;

public class InventoryCheck
{

    //פונקציה שמחשבת כמה צריך מכל פריט בעץ המוצר 
    public InventoryCheckResponse Calculate(InventoryCheckRequest request)
    {
        InventoryCheckResponse response = new InventoryCheckResponse();

        if (request == null)
        {
            return response;
        }

        string mode = (request.Mode ?? "uav").Trim().ToLowerInvariant();
        response.Mode = mode;
        string targetBodyPlane = mode == "body" ? "B" : "P";

        //סינון ערכים לא תקינים מתוך בקשת המשתמש, קיבוץ לפי סוג המטוס וסכימה של הכמויות 
        Dictionary<int, int> planeRequests = request.Requests
            .Where(r => r != null && r.PlaneTypeID > 0 && r.Quantity > 0)
            .GroupBy(r => r.PlaneTypeID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (planeRequests.Count == 0)
        {
            return response;
        }
        //יצירת רשימה של ID של סוגי המטוסים הנדרשים
        List<int> planeTypeIds = planeRequests.Keys.ToList();
        //שליפה מבסיס הנתונים את שמות המטוסים
        DBservices dbs = new DBservices();
        Dictionary<int, string> planeTypeNames = dbs.GetPlaneTypeNames(planeTypeIds);
        //שליפת כל השורות מעץ המוצר שרלוונטיות למטוסים ולמצב שנבחר
        List<BomRow> bomRows = dbs.GetBomRowsForPlanes(planeTypeIds, targetBodyPlane);
        //יצירת מילון שמכיל כמה חלקים צריך מכל פריט 
        Dictionary<string, AggregatedBomNeed> needsByItem = new Dictionary<string, AggregatedBomNeed>(StringComparer.OrdinalIgnoreCase);
        bool debugBomExplosion = false;

        //עבור כל מטוס וכמות שנבחרה ע"י המשתמש
        foreach (KeyValuePair<int, int> requestEntry in planeRequests)
        {
            int planeTypeId = requestEntry.Key;
            int requestedQty = requestEntry.Value;
            //שליפה מהרשימה רק את השורות השייכות למטוס הסספציפי
            List<BomRow> rowsForPlane = bomRows
                .Where(r => r.PlaneTypeID == planeTypeId)
                .OrderBy(r => r.RowOrder)
                .ToList();

            if (rowsForPlane.Count == 0)
            {
                continue;
            }
            //מעבר על כל שורה בעץ המוצר של המטוס
            for (int rowIndex = 0; rowIndex < rowsForPlane.Count; rowIndex++)
            {
                BomRow row = rowsForPlane[rowIndex];
                //סינון הפריטים שיש לקנות בלבד ולא לייצר
                if (!string.Equals(row.BuyMethod?.Trim(), "B", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string itemId = (row.InventoryItemID ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }
                //הפונקציה סורקת את העץ אחורה וסוכמת את כל הכמויות הנדרשות מכל הרמות
                decimal effectiveQty = CalculateBuyRowRequiredQtyByUpwardScan(rowsForPlane, rowIndex, requestedQty, debugBomExplosion);
                //הכנסת הנתונים למילון הכללי, אם עדיין לא קיים מוסיפים ואם קיים מעדכנים את הכמות
                if (!needsByItem.TryGetValue(itemId, out AggregatedBomNeed? existing))
                {
                    existing = new AggregatedBomNeed
                    {
                        InventoryItemID = itemId,
                        ItemName = row.ItemName ?? string.Empty,
                        MeasureUnit = string.IsNullOrWhiteSpace(row.MeasureUnit) ? "each" : row.MeasureUnit.Trim()
                    };
                    needsByItem[itemId] = existing;
                }
                existing.RequiredQty += effectiveQty;
                //שמירה עבור איזה מטוס הפריט נדרש
                existing.PlaneTypeIDs.Add(planeTypeId);
            }
        }
        //אם המילון ריק - לא קיימים פריטים בעץ 
        if (needsByItem.Count == 0)
        {
            return response;
        }
        //שלית מצב המלאי הנוכחי
        Dictionary<string, InventorySnapshot> stockByItem = dbs.GetInventorySnapshotsForItems(needsByItem.Keys.ToList());
        //עבור כל פריט שנדרש לרכוש עבור יציר המטוס נבדוק האם קיים במלאי
        foreach (AggregatedBomNeed need in needsByItem.Values)
        {
            stockByItem.TryGetValue(need.InventoryItemID, out InventorySnapshot? stock);

            decimal totalStock = stock?.TotalStock ?? 0m;
            decimal shortage = need.RequiredQty - totalStock; //חישוב החוסר
            //אם אין חוסר מדלגים
            if (shortage <= 0)
            {
                continue;
            }

            string itemName = need.ItemName;
            if (string.IsNullOrWhiteSpace(itemName) && !string.IsNullOrWhiteSpace(stock?.ItemName))
            {
                itemName = stock.ItemName;
            }
            //בניית רשימת שמות המטוסים שזקוקים לפריט 
            string planeNames = string.Join(", ", need.PlaneTypeIDs
                .Distinct()
                .OrderBy(id => id)
                .Select(id => planeTypeNames.TryGetValue(id, out string? name) ? name : id.ToString()));
            //יצירת אובייקט חסר 
            InventoryCheckShortageItem item = new InventoryCheckShortageItem
            {
                InventoryItemID = need.InventoryItemID,
                ItemName = itemName,
                MeasureUnit = string.IsNullOrWhiteSpace(need.MeasureUnit) ? "each" : need.MeasureUnit,
                RequiredQty = Decimal.Round(need.RequiredQty, 4),
                TotalStock = Decimal.Round(totalStock, 4),
                ShortageQty = Decimal.Round(shortage, 4),
                SupplierName = stock?.SupplierName ?? string.Empty,
                Price = stock?.Price,
                IsSharedAcrossPlanes = need.PlaneTypeIDs.Count > 1,
                ContributingPlaneTypes = planeNames
            };

            response.Items.Add(item);
        }
        //מיון הרשימה לפי כמות חוסר
        response.Items = response.Items
            .OrderByDescending(i => i.ShortageQty)
            .ThenBy(i => i.InventoryItemID)
            .ToList();
        //סיכומים כלליים לראש הדף
        response.TotalShortageItems = response.Items.Count;
        response.TotalShortageUnits = Decimal.Round(response.Items.Sum(i => i.ShortageQty), 4);
        response.TotalEstimatedCost = Decimal.Round(response.Items.Sum(i => (decimal)(i.Price ?? 0d) * i.ShortageQty), 2);

        return response;
    }
    
    //חישוב כמות עבור הפריט הנמוך ביותר בעץ
    private static decimal CalculateBuyRowRequiredQtyByUpwardScan(
        List<BomRow> rowsForPlane, //שורות עץ המוצר של המטוס
        int currentRowIndex,//המיקום של הפריט הנוכחי בתוך הרשימה
        int requestedPlaneQty,// כמה מטוסים צריך מסוג זה
        bool debugBomExplosion)
    {
        //שורת הפריט הנבדקת
        BomRow currentRow = rowsForPlane[currentRowIndex];
        //קביעת הרמה בעץ
        int currentLevel = currentRow.BomLevel <= 0 ? 1 : currentRow.BomLevel;
        //הכמות הראשונית עבור מוצר זה
        decimal effectiveQty = currentRow.Quantity ?? 0m;
        
        if (debugBomExplosion)
        {
            Debug.WriteLine($"[BOM UPWARD] Start Item={currentRow.InventoryItemID} RowOrder={currentRow.RowOrder} Level={currentLevel} RowQty={currentRow.Quantity ?? 0m}");
        }
        
        int targetLevel = currentLevel - 1; //רמה הבאה
        int searchIndex = currentRowIndex - 1; //רמת החיפוש הנוכחית
        //הלולאה עוברת על כל הרמות עד שמגיעה לרשורש העץ - המוצר עצמו 
        while (targetLevel >= 1)
        {
            int foundIndex = -1;
            //חיפוש האב הקרוב ביותר לרמה הנוכחית
            for (int i = searchIndex; i >= 0; i--)
            {
                int candidateLevel = rowsForPlane[i].BomLevel <= 0 ? 1 : rowsForPlane[i].BomLevel;
                if (candidateLevel == targetLevel)
                {
                    foundIndex = i;
                    break;
                }
            }
            
            if (foundIndex < 0)
            {
                if (debugBomExplosion)
                {
                    Debug.WriteLine($"[BOM UPWARD] Item={currentRow.InventoryItemID} Missing ancestor at level={targetLevel}");
                }
                break;
            }
            //הכפלת הכמות של האבא בכמות המצטרבת עבור הפריט
            BomRow ancestor = rowsForPlane[foundIndex];
            decimal ancestorQty = ancestor.Quantity ?? 0m;
            effectiveQty *= ancestorQty;

            if (debugBomExplosion)
            {
                Debug.WriteLine($"[BOM UPWARD] Item={currentRow.InventoryItemID} AncestorLevel={targetLevel} AncestorRowOrder={ancestor.RowOrder} AncestorItem={ancestor.InventoryItemID} AncestorQty={ancestorQty} RunningQty={effectiveQty}");
            }
            searchIndex = foundIndex - 1;
            targetLevel--;
        }
        //הכפלה בכמות המטוסים
        effectiveQty *= requestedPlaneQty;

        if (debugBomExplosion)
        {
            Debug.WriteLine($"[BOM UPWARD] Final Item={currentRow.InventoryItemID} RequestedPlanes={requestedPlaneQty} EffectiveQty={effectiveQty}");
        }

        return effectiveQty;
    }

    //מחלקת עזר לפריט עבור חישוב הכמות הנדרשת מתוך העץ
    private class AggregatedBomNeed
    {
        public string InventoryItemID { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string MeasureUnit { get; set; } = "each";
        public decimal RequiredQty { get; set; }
        public HashSet<int> PlaneTypeIDs { get; set; } = new HashSet<int>();
    }

    public class InventorySnapshot
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal TotalStock { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public double? Price { get; set; }
    }
}

public class InventoryCheckRequest
{
    public string Mode { get; set; } = "uav"; //גוף או מטוס

    //רשימה של מסוגי מטוסים והכמות שלהם לבדיקת המלאי
    public List<InventoryCheckPlaneRequest> Requests { get; set; } = new List<InventoryCheckPlaneRequest>();
}

public class InventoryCheckPlaneRequest
{
    public int PlaneTypeID { get; set; } //סוג המטוס
    public int Quantity { get; set; } // כמות מסוג זה
}

public class InventoryCheckResponse
{
    public string Mode { get; set; } = "uav";
    public int TotalShortageItems { get; set; }
    public decimal TotalShortageUnits { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<InventoryCheckShortageItem> Items { get; set; } = new List<InventoryCheckShortageItem>();
}

public class InventoryCheckShortageItem
{
    public string InventoryItemID { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string MeasureUnit { get; set; } = "each";
    public decimal RequiredQty { get; set; }
    public decimal TotalStock { get; set; }
    public decimal ShortageQty { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public double? Price { get; set; }
    public bool IsSharedAcrossPlanes { get; set; }
    public string ContributingPlaneTypes { get; set; } = string.Empty;
}
