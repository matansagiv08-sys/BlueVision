using System.Diagnostics;
using Server.DAL;

namespace Server.Models;

public class InventoryCheck
{
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

        //Aggregates and cleans the input by grouping requests per plane type and summing their quantities into a dictionary.
        Dictionary<int, int> planeRequests = request.Requests
            .Where(r => r != null && r.PlaneTypeID > 0 && r.Quantity > 0)
            .GroupBy(r => r.PlaneTypeID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (planeRequests.Count == 0)
        {
            return response;
        }

        List<int> planeTypeIds = planeRequests.Keys.ToList();

        DBservices dbs = new DBservices();
        Dictionary<int, string> planeTypeNames = dbs.GetPlaneTypeNames(planeTypeIds);
        List<BomRow> bomRows = dbs.GetBomRowsForPlanes(planeTypeIds, targetBodyPlane);

        Dictionary<string, AggregatedBomNeed> needsByItem = new Dictionary<string, AggregatedBomNeed>(StringComparer.OrdinalIgnoreCase);
        bool debugBomExplosion = false;

        //This loop goes plane type by plane type
        foreach (KeyValuePair<int, int> requestEntry in planeRequests)
        {
            int planeTypeId = requestEntry.Key;
            int requestedQty = requestEntry.Value;

            //the row order is important for the upward scan calculation, so we order them here
            List<BomRow> rowsForPlane = bomRows
                .Where(r => r.PlaneTypeID == planeTypeId)
                .OrderBy(r => r.RowOrder)
                .ToList();

            if (rowsForPlane.Count == 0)
            {
                continue;
            }

            //This loop goes row by row through the BOM and does 2 filterr - buymethod B and itemsID exists
            for (int rowIndex = 0; rowIndex < rowsForPlane.Count; rowIndex++)
            {
                BomRow row = rowsForPlane[rowIndex];

                if (!string.Equals(row.BuyMethod?.Trim(), "B", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string itemId = (row.InventoryItemID ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                //calculate the effective quantity required for this buy row by doing an upward scan through the BOM levels to multiply the quantities along the path
                decimal effectiveQty = CalculateBuyRowRequiredQtyByUpwardScan(rowsForPlane, rowIndex, requestedQty, debugBomExplosion);

                //aggregate the effective quantity into the needsByItem dictionary, if the item already has an entry, we sum the required quantity and add the plane type to the contributing planes set, otherwise we create a new entry
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
                existing.PlaneTypeIDs.Add(planeTypeId);
            }
        }

        if (needsByItem.Count == 0)
        {
            return response;
        }

        //gets current stock for all items in the needs list with a single query to the database and stores it in a dictionary for quick access
        Dictionary<string, InventorySnapshot> stockByItem = dbs.GetInventorySnapshotsForItems(needsByItem.Keys.ToList());

        //compares required quantity vs stock
        foreach (AggregatedBomNeed need in needsByItem.Values)
        {
            stockByItem.TryGetValue(need.InventoryItemID, out InventorySnapshot? stock);

            decimal totalStock = stock?.TotalStock ?? 0m;
            decimal shortage = need.RequiredQty - totalStock;

            if (shortage <= 0)
            {
                continue;
            }

            string itemName = need.ItemName;
            if (string.IsNullOrWhiteSpace(itemName) && !string.IsNullOrWhiteSpace(stock?.ItemName))
            {
                itemName = stock.ItemName;
            }

            string planeNames = string.Join(", ", need.PlaneTypeIDs
                .Distinct()
                .OrderBy(id => id)
                .Select(id => planeTypeNames.TryGetValue(id, out string? name) ? name : id.ToString()));

            //for each item that has a shortage, we create an InventoryCheckShortageItem object with all the relevant information and add it to the response list
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

        //sort the response items by shortage quantity desc, then by item id asc
        response.Items = response.Items
            .OrderByDescending(i => i.ShortageQty)
            .ThenBy(i => i.InventoryItemID)
            .ToList();

        //calculate the summary fields for the response which are displayed at the top of the client page
        response.TotalShortageItems = response.Items.Count;
        response.TotalShortageUnits = Decimal.Round(response.Items.Sum(i => i.ShortageQty), 4);
        response.TotalEstimatedCost = Decimal.Round(response.Items.Sum(i => (decimal)(i.Price ?? 0d) * i.ShortageQty), 2);

        return response;
    }

    //How many units of this buy item are needed for the requested number of planes, based on the BOM hierarchy and the quantities specified at each level.
    private static decimal CalculateBuyRowRequiredQtyByUpwardScan(
        List<BomRow> rowsForPlane,
        int currentRowIndex,
        int requestedPlaneQty,
        bool debugBomExplosion)
    {
        BomRow currentRow = rowsForPlane[currentRowIndex];
        int currentLevel = currentRow.BomLevel <= 0 ? 1 : currentRow.BomLevel;
        decimal effectiveQty = currentRow.Quantity ?? 0m;

        if (debugBomExplosion)
        {
            Debug.WriteLine($"[BOM UPWARD] Start Item={currentRow.InventoryItemID} RowOrder={currentRow.RowOrder} Level={currentLevel} RowQty={currentRow.Quantity ?? 0m}");
        }

        //searching for the parent level which is -1 than the current level in an upward scan
        int targetLevel = currentLevel - 1;
        int searchIndex = currentRowIndex - 1;

        while (targetLevel >= 1)
        {
            int foundIndex = -1;

            //loops in an upward scan looking for the first row that matches the target level
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

            //multiplies the parent row quantity with the quantity calculated so far
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

        effectiveQty *= requestedPlaneQty;

        if (debugBomExplosion)
        {
            Debug.WriteLine($"[BOM UPWARD] Final Item={currentRow.InventoryItemID} RequestedPlanes={requestedPlaneQty} EffectiveQty={effectiveQty}");
        }

        return effectiveQty;
    }
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
    public string Mode { get; set; } = "uav";
    public List<InventoryCheckPlaneRequest> Requests { get; set; } = new List<InventoryCheckPlaneRequest>();
}

public class InventoryCheckPlaneRequest
{
    public int PlaneTypeID { get; set; }
    public int Quantity { get; set; }
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
