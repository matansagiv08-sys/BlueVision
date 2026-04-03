namespace Server.Models;

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
