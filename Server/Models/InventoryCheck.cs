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

        List<RequestDemandInput> requestDemandRows = request.Requests
            .Where(r => r != null && r.PlaneTypeID > 0 && r.Quantity > 0)
            .Select((r, idx) => new RequestDemandInput
            {
                RequestIndex = idx,
                PlaneTypeID = r.PlaneTypeID,
                Quantity = r.Quantity,
                IsHighPriority = r.IsHighPriority
            })
            .ToList();

        Dictionary<int, List<RequestDemandInput>> requestDemandRowsByPlaneType = requestDemandRows
            .GroupBy(r => r.PlaneTypeID)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, decimal> requestQtyByPlaneType = planeRequests
            .ToDictionary(kvp => kvp.Key, kvp => Convert.ToDecimal(kvp.Value));

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
                if (!existing.RequiredQtyByPlaneTypeID.ContainsKey(planeTypeId))
                {
                    existing.RequiredQtyByPlaneTypeID[planeTypeId] = 0m;
                }
                existing.RequiredQtyByPlaneTypeID[planeTypeId] += effectiveQty;

                if (requestDemandRowsByPlaneType.TryGetValue(planeTypeId, out List<RequestDemandInput>? demandRows)
                    && demandRows.Count > 0
                    && requestedQty > 0)
                {
                    foreach (RequestDemandInput demandRow in demandRows)
                    {
                        decimal demandShare = effectiveQty * demandRow.Quantity / requestedQty;

                        if (!existing.RequiredQtyByRequestIndex.TryGetValue(demandRow.RequestIndex, out RequestDemandAllocation? allocation))
                        {
                            allocation = new RequestDemandAllocation
                            {
                                PlaneTypeID = demandRow.PlaneTypeID,
                                IsHighPriority = demandRow.IsHighPriority
                            };
                            existing.RequiredQtyByRequestIndex[demandRow.RequestIndex] = allocation;
                        }

                        allocation.RequiredQty += demandShare;
                    }
                }
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

            Dictionary<string, decimal> shortageByPlane = BuildShortageByPlane(
                need,
                totalStock,
                requestQtyByPlaneType,
                planeTypeNames);

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
                ContributingPlaneTypes = planeNames,
                ShortageByPlane = shortageByPlane
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

    private static Dictionary<string, decimal> BuildShortageByPlane(
        AggregatedBomNeed need,
        decimal totalStock,
        Dictionary<int, decimal> requestQtyByPlaneType,
        Dictionary<int, string> planeTypeNames)
    {
        Dictionary<string, decimal> shortageByPlane = new Dictionary<string, decimal>();

        List<int> contributingPlaneIds = need.PlaneTypeIDs
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (contributingPlaneIds.Count == 0 || need.RequiredQty <= 0)
        {
            return shortageByPlane;
        }

        Dictionary<int, decimal> requestWeights = new Dictionary<int, decimal>();
        decimal totalRequestWeight = 0m;
        foreach (int planeId in contributingPlaneIds)
        {
            decimal weight = requestQtyByPlaneType.TryGetValue(planeId, out decimal reqQty) ? reqQty : 0m;
            requestWeights[planeId] = weight;
            totalRequestWeight += weight;
        }

        if (totalRequestWeight <= 0)
        {
            foreach (int planeId in contributingPlaneIds)
            {
                decimal fallbackWeight = need.RequiredQtyByPlaneTypeID.TryGetValue(planeId, out decimal reqQty)
                    ? reqQty
                    : 0m;
                requestWeights[planeId] = fallbackWeight;
            }
            totalRequestWeight = requestWeights.Values.Sum();
        }

        if (totalRequestWeight <= 0)
        {
            decimal evenWeight = 1m;
            foreach (int planeId in contributingPlaneIds)
            {
                requestWeights[planeId] = evenWeight;
            }
            totalRequestWeight = contributingPlaneIds.Count;
        }

        Dictionary<int, decimal> requiredByPlane = new Dictionary<int, decimal>();
        foreach (int planeId in contributingPlaneIds)
        {
            decimal ratio = requestWeights[planeId] / totalRequestWeight;
            requiredByPlane[planeId] = Decimal.Round(need.RequiredQty * ratio, 6);
        }

        int maxRequiredPlane = contributingPlaneIds
            .OrderByDescending(id => requiredByPlane[id])
            .ThenBy(id => id)
            .First();

        decimal requiredDelta = need.RequiredQty - requiredByPlane.Values.Sum();
        requiredByPlane[maxRequiredPlane] = Decimal.Round(requiredByPlane[maxRequiredPlane] + requiredDelta, 6);

        if (need.RequiredQtyByRequestIndex.Count > 0)
        {
            List<RequestDemandAllocation> demandAllocations = need.RequiredQtyByRequestIndex.Values
                .Where(v => v.RequiredQty > 0)
                .Select(v => new RequestDemandAllocation
                {
                    PlaneTypeID = v.PlaneTypeID,
                    IsHighPriority = v.IsHighPriority,
                    RequiredQty = Decimal.Round(v.RequiredQty, 6),
                    AllocatedQty = 0m
                })
                .ToList();

            List<RequestDemandAllocation> highPriorityDemands = demandAllocations.Where(d => d.IsHighPriority).ToList();
            List<RequestDemandAllocation> normalDemands = demandAllocations.Where(d => !d.IsHighPriority).ToList();

            decimal remainingStock = totalStock;

            AllocateStockForDemandGroup(highPriorityDemands, ref remainingStock);
            AllocateStockForDemandGroup(normalDemands, ref remainingStock);

            foreach (RequestDemandAllocation demand in demandAllocations)
            {
                decimal shortage = demand.RequiredQty - demand.AllocatedQty;
                if (shortage < 0)
                {
                    shortage = 0;
                }

                string planeName = planeTypeNames.TryGetValue(demand.PlaneTypeID, out string? name)
                    ? name
                    : demand.PlaneTypeID.ToString();

                if (!shortageByPlane.ContainsKey(planeName))
                {
                    shortageByPlane[planeName] = 0m;
                }

                shortageByPlane[planeName] += shortage;
            }
        }
        else
        {
            Dictionary<int, decimal> allocatedByPlane = new Dictionary<int, decimal>();
            foreach (int planeId in contributingPlaneIds)
            {
                decimal proportionalAllocation = (requiredByPlane[planeId] / need.RequiredQty) * totalStock;
                allocatedByPlane[planeId] = Math.Floor(proportionalAllocation);
            }

            decimal leftoverStock = totalStock - allocatedByPlane.Values.Sum();
            if (leftoverStock > 0)
            {
                allocatedByPlane[maxRequiredPlane] += leftoverStock;
            }

            foreach (int planeId in contributingPlaneIds)
            {
                decimal shortage = requiredByPlane[planeId] - allocatedByPlane[planeId];
                if (shortage < 0)
                {
                    shortage = 0;
                }

                string planeName = planeTypeNames.TryGetValue(planeId, out string? name)
                    ? name
                    : planeId.ToString();

                shortageByPlane[planeName] = shortage;
            }
        }

        List<string> keys = shortageByPlane.Keys.ToList();
        foreach (string key in keys)
        {
            shortageByPlane[key] = Decimal.Round(shortageByPlane[key], 2);
        }

        return shortageByPlane;
    }

    private static void AllocateStockForDemandGroup(List<RequestDemandAllocation> demands, ref decimal remainingStock)
    {
        if (demands.Count == 0 || remainingStock <= 0)
        {
            return;
        }

        decimal totalRequired = demands.Sum(d => d.RequiredQty);
        if (totalRequired <= 0)
        {
            return;
        }

        if (remainingStock >= totalRequired)
        {
            foreach (RequestDemandAllocation demand in demands)
            {
                demand.AllocatedQty = demand.RequiredQty;
            }
            remainingStock -= totalRequired;
            return;
        }

        foreach (RequestDemandAllocation demand in demands)
        {
            decimal proportional = (demand.RequiredQty / totalRequired) * remainingStock;
            demand.AllocatedQty = Math.Floor(proportional);
        }

        decimal allocatedTotal = demands.Sum(d => d.AllocatedQty);
        decimal leftover = remainingStock - allocatedTotal;
        if (leftover > 0)
        {
            RequestDemandAllocation maxDemand = demands
                .OrderByDescending(d => d.RequiredQty)
                .ThenBy(d => d.PlaneTypeID)
                .First();
            maxDemand.AllocatedQty += leftover;
        }

        remainingStock = 0;
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
        public Dictionary<int, decimal> RequiredQtyByPlaneTypeID { get; set; } = new Dictionary<int, decimal>();
        public Dictionary<int, RequestDemandAllocation> RequiredQtyByRequestIndex { get; set; } = new Dictionary<int, RequestDemandAllocation>();
    }

    private class RequestDemandInput
    {
        public int RequestIndex { get; set; }
        public int PlaneTypeID { get; set; }
        public decimal Quantity { get; set; }
        public bool IsHighPriority { get; set; }
    }

    private class RequestDemandAllocation
    {
        public int PlaneTypeID { get; set; }
        public bool IsHighPriority { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal AllocatedQty { get; set; }
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
    public bool IsHighPriority { get; set; }
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
    public Dictionary<string, decimal> ShortageByPlane { get; set; } = new Dictionary<string, decimal>();
}
