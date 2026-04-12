using Server.DAL;

namespace Server.Models;

public class InventoryItem
{
    public string InventoryItemID { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int? ItemGrpID { get; set; }
    public string ItemGrpName { get; set; } = string.Empty;
    public string? BuyMethod { get; set; }
    public double? Price { get; set; }
    public int? SupplierID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? Whse01_QTY { get; set; }
    public int? Whse03_QTY { get; set; }
    public int? Whse90_QTY { get; set; }
    public int? OpenPurchaseRequestQty { get; set; }
    public int? OpenPurchaseOrderQty { get; set; }
    public int? ApprovedOrderQty { get; set; }
    public int? UnapprovedOrderQty { get; set; }
    public string? BodyPlane { get; set; }
    public DateTime? LastPODate { get; set; }

    // Calls DBservices to import inventory data from Excel and returns the import results summary
    public InventoryImportResult ImportFromExcel(string filePath)
    {
        DBservices dbs = new DBservices();
        return dbs.ImportInventoryItemsFromExcel(filePath);
    }
}

// Holds detailed results of the inventory import process, including ProductionItems sync statistics
public class InventoryImportResult
{
    public int ImportedRows { get; set; }
    public int DeletedProductionItems { get; set; }
    public int InsertedProductionItems { get; set; }
    public int UpdatedProductionItems { get; set; }
    public int FinalProductionItemsCount { get; set; }
}
