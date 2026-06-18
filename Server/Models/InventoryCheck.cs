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

        List<RequestDemandInput> validRequestRows = request.Requests
            .Where(r => r != null && r.PlaneTypeID > 0 && r.Quantity > 0)
            .Select((r, idx) => new RequestDemandInput
            {
                RequestIndex = idx,
                PlaneTypeID = r.PlaneTypeID,
                Quantity = r.Quantity,
                IsHighPriority = r.IsHighPriority
            })
            .ToList();

        //Identical UAV/body requests with the same priority are one demand group for allocation.
        List<RequestDemandInput> requestDemandRows = validRequestRows
            .GroupBy(r => new { r.PlaneTypeID, r.IsHighPriority })
            .Select(g => new RequestDemandInput
            {
                RequestIndex = g.Min(x => x.RequestIndex),
                PlaneTypeID = g.Key.PlaneTypeID,
                Quantity = g.Sum(x => x.Quantity),
                IsHighPriority = g.Key.IsHighPriority
            })
            .OrderBy(r => r.RequestIndex)
            .ToList();

        //Aggregates and cleans the input by grouping requests per plane type and summing their quantities into a dictionary.
        Dictionary<int, int> planeRequests = requestDemandRows
            .GroupBy(r => r.PlaneTypeID)
            .ToDictionary(g => g.Key, g => Convert.ToInt32(g.Sum(x => x.Quantity)));

        Dictionary<int, List<RequestDemandInput>> requestDemandRowsByPlaneType = requestDemandRows
            .GroupBy(r => r.PlaneTypeID)
            .ToDictionary(g => g.Key, g => g.ToList());

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
                        MeasureUnit = string.IsNullOrWhiteSpace(row.MeasureUnit) ? null : row.MeasureUnit.Trim()
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
                                RequestIndex = demandRow.RequestIndex,
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
        ProductionAllocation productionAllocation = BuildProductionAllocation(requestDemandRows, needsByItem.Values, stockByItem);

        //compares unfulfilled required quantity vs remaining stock after complete UAV/body allocation
        foreach (AggregatedBomNeed need in needsByItem.Values)
        {
            stockByItem.TryGetValue(need.InventoryItemID, out InventorySnapshot? stock);

            decimal totalStock = productionAllocation.RemainingStockByItem.TryGetValue(need.InventoryItemID, out decimal remainingStock)
                ? remainingStock
                : 0m;
            decimal requiredQty = CalculateUnfulfilledRequiredQty(need, requestDemandRows, productionAllocation);
            decimal shortage = requiredQty - totalStock;

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
                requestDemandRows,
                productionAllocation,
                planeTypeNames);

            //for each item that has a shortage, we create an InventoryCheckShortageItem object with all the relevant information and add it to the response list
                InventoryCheckShortageItem item = new InventoryCheckShortageItem
                {
                    InventoryItemID = need.InventoryItemID,
                    ItemName = itemName,
                    MeasureUnit = string.IsNullOrWhiteSpace(need.MeasureUnit) ? null : need.MeasureUnit,
                RequiredQty = Decimal.Round(requiredQty, 4),
                TotalStock = Decimal.Round(totalStock, 4),
                ShortageQty = Decimal.Round(shortage, 4),
                SupplierName = stock?.SupplierName ?? string.Empty,
                Price = stock?.Price,
                OpenPurchaseRequestQty = stock?.OpenPurchaseRequestQty ?? 0,
                OpenPurchaseOrderQty = stock?.OpenPurchaseOrderQty ?? 0,
                ApprovedOrderQty = stock?.ApprovedOrderQty ?? 0,
                UnapprovedOrderQty = stock?.UnapprovedOrderQty ?? 0,
                IsSharedAcrossPlanes = need.PlaneTypeIDs.Count > 1,
                ContributingPlaneTypes = planeNames,
                ShortageByPlane = shortageByPlane
            };

            response.Items.Add(item);
        }

        response.ReadyToProduceRows = BuildReadyToProduceRows(
            requestDemandRows,
            productionAllocation,
            planeTypeNames);

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

    private static List<InventoryCheckReadyToProduceItem> BuildReadyToProduceRows(
        List<RequestDemandInput> requestRows,
        ProductionAllocation productionAllocation,
        Dictionary<int, string> planeTypeNames)
    {
        List<InventoryCheckReadyToProduceItem> readyRows = new List<InventoryCheckReadyToProduceItem>();

        if (requestRows == null || requestRows.Count == 0)
        {
            return readyRows;
        }

        foreach (RequestDemandInput request in requestRows.OrderBy(r => r.RequestIndex))
        {
            string planeName = planeTypeNames.TryGetValue(request.PlaneTypeID, out string? name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : request.PlaneTypeID.ToString();

            decimal readyQtyValue = productionAllocation.FulfilledQtyByRequestIndex.TryGetValue(request.RequestIndex, out decimal fulfilledQty)
                ? fulfilledQty
                : 0m;

            int readyQty = Convert.ToInt32(Math.Max(0m, Math.Min(request.Quantity, readyQtyValue)));

            readyRows.Add(new InventoryCheckReadyToProduceItem
            {
                PlaneTypeID = request.PlaneTypeID,
                PlaneTypeName = planeName,
                RequestedQty = Convert.ToInt32(request.Quantity),
                ReadyQty = readyQty
            });
        }

        return readyRows;
    }

    private static ProductionAllocation BuildProductionAllocation(
        List<RequestDemandInput> requestRows,
        IEnumerable<AggregatedBomNeed> needs,
        Dictionary<string, InventorySnapshot> stockByItem)
    {
        ProductionAllocation result = new ProductionAllocation();
        Dictionary<int, RequestDemandInput> requestByIndex = requestRows.ToDictionary(r => r.RequestIndex);
        Dictionary<int, List<RequestDemandAllocation>> requirementsByRequestIndex = new Dictionary<int, List<RequestDemandAllocation>>();

        foreach (AggregatedBomNeed need in needs)
        {
            decimal stock = stockByItem.TryGetValue(need.InventoryItemID, out InventorySnapshot? snapshot)
                ? snapshot.TotalStock
                : 0m;
            result.RemainingStockByItem[need.InventoryItemID] = stock;

            foreach (RequestDemandAllocation allocation in need.RequiredQtyByRequestIndex.Values)
            {
                if (!requestByIndex.ContainsKey(allocation.RequestIndex) || allocation.RequiredQty <= 0)
                {
                    continue;
                }

                if (!requirementsByRequestIndex.TryGetValue(allocation.RequestIndex, out List<RequestDemandAllocation>? requestRequirements))
                {
                    requestRequirements = new List<RequestDemandAllocation>();
                    requirementsByRequestIndex[allocation.RequestIndex] = requestRequirements;
                }

                requestRequirements.Add(new RequestDemandAllocation
                {
                    RequestIndex = allocation.RequestIndex,
                    PlaneTypeID = allocation.PlaneTypeID,
                    IsHighPriority = allocation.IsHighPriority,
                    InventoryItemID = need.InventoryItemID,
                    RequiredQty = allocation.RequiredQty
                });
            }
        }

        foreach (RequestDemandInput request in requestRows)
        {
            result.FulfilledQtyByRequestIndex[request.RequestIndex] = 0m;
        }

        IEnumerable<RequestDemandInput> orderedRequests = requestRows
            .OrderByDescending(r => r.IsHighPriority)
            .ThenBy(r => r.RequestIndex);

        foreach (RequestDemandInput request in orderedRequests)
        {
            if (!requirementsByRequestIndex.TryGetValue(request.RequestIndex, out List<RequestDemandAllocation>? requirements)
                || requirements.Count == 0
                || request.Quantity <= 0)
            {
                continue;
            }

            decimal producibleQty = request.Quantity;
            foreach (RequestDemandAllocation requirement in requirements)
            {
                decimal requiredPerUnit = requirement.RequiredQty / request.Quantity;
                if (requiredPerUnit <= 0)
                {
                    continue;
                }

                decimal stock = result.RemainingStockByItem.TryGetValue(requirement.InventoryItemID, out decimal remainingStock)
                    ? remainingStock
                    : 0m;
                producibleQty = Math.Min(producibleQty, Math.Floor(stock / requiredPerUnit));
            }

            producibleQty = Math.Max(0m, Math.Min(request.Quantity, producibleQty));
            result.FulfilledQtyByRequestIndex[request.RequestIndex] = producibleQty;

            if (producibleQty <= 0)
            {
                continue;
            }

            foreach (RequestDemandAllocation requirement in requirements)
            {
                decimal requiredPerUnit = requirement.RequiredQty / request.Quantity;
                decimal consumedQty = requiredPerUnit * producibleQty;
                result.RemainingStockByItem[requirement.InventoryItemID] = Math.Max(0m, result.RemainingStockByItem[requirement.InventoryItemID] - consumedQty);
            }
        }

        return result;
    }

    private static decimal CalculateUnfulfilledRequiredQty(
        AggregatedBomNeed need,
        List<RequestDemandInput> requestRows,
        ProductionAllocation productionAllocation)
    {
        Dictionary<int, RequestDemandInput> requestByIndex = requestRows.ToDictionary(r => r.RequestIndex);
        decimal requiredQty = 0m;

        foreach (RequestDemandAllocation allocation in need.RequiredQtyByRequestIndex.Values)
        {
            if (!requestByIndex.TryGetValue(allocation.RequestIndex, out RequestDemandInput? request) || request.Quantity <= 0)
            {
                continue;
            }

            decimal fulfilledQty = productionAllocation.FulfilledQtyByRequestIndex.TryGetValue(allocation.RequestIndex, out decimal value)
                ? value
                : 0m;
            decimal unfulfilledQty = Math.Max(0m, request.Quantity - fulfilledQty);
            requiredQty += (allocation.RequiredQty / request.Quantity) * unfulfilledQty;
        }

        return requiredQty;
    }

    private static Dictionary<string, decimal> BuildShortageByPlane(
        AggregatedBomNeed need,
        decimal totalStock,
        List<RequestDemandInput> requestRows,
        ProductionAllocation productionAllocation,
        Dictionary<int, string> planeTypeNames)
    {
        Dictionary<string, decimal> shortageByPlane = new Dictionary<string, decimal>();
        Dictionary<int, RequestDemandInput> requestByIndex = requestRows.ToDictionary(r => r.RequestIndex);
        List<RequestDemandAllocation> unfulfilledDemands = new List<RequestDemandAllocation>();

        foreach (RequestDemandAllocation allocation in need.RequiredQtyByRequestIndex.Values)
        {
            if (!requestByIndex.TryGetValue(allocation.RequestIndex, out RequestDemandInput? request) || request.Quantity <= 0)
            {
                continue;
            }

            decimal fulfilledQty = productionAllocation.FulfilledQtyByRequestIndex.TryGetValue(allocation.RequestIndex, out decimal value)
                ? value
                : 0m;
            decimal unfulfilledQty = Math.Max(0m, request.Quantity - fulfilledQty);
            decimal requiredQty = (allocation.RequiredQty / request.Quantity) * unfulfilledQty;
            if (requiredQty <= 0)
            {
                continue;
            }

            unfulfilledDemands.Add(new RequestDemandAllocation
            {
                RequestIndex = allocation.RequestIndex,
                PlaneTypeID = allocation.PlaneTypeID,
                IsHighPriority = allocation.IsHighPriority,
                RequiredQty = requiredQty,
                AllocatedQty = 0m
            });
        }

        decimal remainingStock = totalStock;
        foreach (RequestDemandAllocation demand in unfulfilledDemands
            .OrderByDescending(d => d.IsHighPriority)
            .ThenBy(d => d.RequestIndex))
        {
            decimal allocatedQty = Math.Min(demand.RequiredQty, remainingStock);
            remainingStock -= allocatedQty;
            decimal shortage = Math.Max(0m, demand.RequiredQty - allocatedQty);

            string planeName = planeTypeNames.TryGetValue(demand.PlaneTypeID, out string? name)
                ? name
                : demand.PlaneTypeID.ToString();

            if (!shortageByPlane.ContainsKey(planeName))
            {
                shortageByPlane[planeName] = 0m;
            }

            shortageByPlane[planeName] += shortage;
        }

        List<string> keys = shortageByPlane.Keys.ToList();
        foreach (string key in keys)
        {
            shortageByPlane[key] = Decimal.Round(shortageByPlane[key], 2);
        }

        return shortageByPlane;
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
        public string? MeasureUnit { get; set; }
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
        public int RequestIndex { get; set; }
        public int PlaneTypeID { get; set; }
        public bool IsHighPriority { get; set; }
        public string InventoryItemID { get; set; } = string.Empty;
        public decimal RequiredQty { get; set; }
        public decimal AllocatedQty { get; set; }
    }

    private class ProductionAllocation
    {
        public Dictionary<int, decimal> FulfilledQtyByRequestIndex { get; set; } = new Dictionary<int, decimal>();
        public Dictionary<string, decimal> RemainingStockByItem { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    public class InventorySnapshot
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal TotalStock { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public double? Price { get; set; }
        public decimal OpenPurchaseRequestQty { get; set; }
        public decimal OpenPurchaseOrderQty { get; set; }
        public decimal ApprovedOrderQty { get; set; }
        public decimal UnapprovedOrderQty { get; set; }
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
    public List<InventoryCheckReadyToProduceItem> ReadyToProduceRows { get; set; } = new List<InventoryCheckReadyToProduceItem>();
    public List<InventoryCheckShortageItem> Items { get; set; } = new List<InventoryCheckShortageItem>();
}

public class InventoryCheckReadyToProduceItem
{
    public int PlaneTypeID { get; set; }
    public string PlaneTypeName { get; set; } = string.Empty;
    public int RequestedQty { get; set; }
    public int ReadyQty { get; set; }
}

public class InventoryCheckShortageItem
{
    public string InventoryItemID { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? MeasureUnit { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal TotalStock { get; set; }
    public decimal ShortageQty { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public double? Price { get; set; }
    public decimal OpenPurchaseRequestQty { get; set; }
    public decimal OpenPurchaseOrderQty { get; set; }
    public decimal ApprovedOrderQty { get; set; }
    public decimal UnapprovedOrderQty { get; set; }
    public bool IsSharedAcrossPlanes { get; set; }
    public string ContributingPlaneTypes { get; set; } = string.Empty;
    public Dictionary<string, decimal> ShortageByPlane { get; set; } = new Dictionary<string, decimal>();
}
