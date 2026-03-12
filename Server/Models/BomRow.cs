namespace Server.Models;

public class BomRow
{
    public int BomSerialID { get; set; }
    public int PlaneTypeID { get; set; }
    public string? PlaneTypeName { get; set; }
    public int RowOrder { get; set; }
    public string InventoryItemID { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public decimal? Quantity { get; set; }
    public string? MeasureUnit { get; set; }
    public string? Warehouse { get; set; }
    public int BomLevel { get; set; }
    public bool? HasChild { get; set; }
    public string? HasChildRaw { get; set; }
    public string? BuyMethod { get; set; }
    public string? BodyPlane { get; set; }
}
