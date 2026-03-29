using Server.DAL;

namespace Server.Models;

public class InventoryItem
{
    public string InventoryItemID { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int? ItemGrpID { get; set; }
    public string? BuyMethod { get; set; }
    public double? Price { get; set; }
    public int? SupplierID { get; set; }
    public int? Whse01_QTY { get; set; }
    public int? Whse03_QTY { get; set; }
    public int? Whse90_QTY { get; set; }
    public int? OpenPurchaseRequestQty { get; set; }
    public int? OpenPurchaseOrderQty { get; set; }
    public int? ApprovedOrderQty { get; set; }
    public int? UnapprovedOrderQty { get; set; }
    public string? BodyPlane { get; set; }
    public DateTime? LastPODate { get; set; }

    public int ImportFromExcel(string filePath)
    {
        DBservices dbs = new DBservices();
        return dbs.ImportInventoryItemsFromExcel(filePath);
    }
}
