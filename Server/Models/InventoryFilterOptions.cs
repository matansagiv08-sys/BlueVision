namespace Server.Models;

public class InventoryFilterOptions
{
    public List<InventoryPlatformOption> Platforms { get; set; } = new List<InventoryPlatformOption>();
    public List<InventoryGroupOption> Groups { get; set; } = new List<InventoryGroupOption>();
    public List<string> BuyMethods { get; set; } = new List<string>();
    public List<InventorySupplierOption> Suppliers { get; set; } = new List<InventorySupplierOption>();
    public List<string> BodyPlanes { get; set; } = new List<string>();
}

public class InventoryPlatformOption
{
    public int PlaneTypeID { get; set; }
    public string PlaneTypeName { get; set; } = string.Empty;
}

public class InventoryGroupOption
{
    public int ItemGrpID { get; set; }
    public string ItemGrpName { get; set; } = string.Empty;
}

public class InventorySupplierOption
{
    public int SupplierID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
}
