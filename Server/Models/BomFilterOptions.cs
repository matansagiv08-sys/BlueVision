namespace Server.Models;

public class BomPlaneOption
{
    public int PlaneTypeID { get; set; }
    public string PlaneTypeName { get; set; } = string.Empty;
}

public class BomFilterOptions
{
    public List<string> MeasureUnits { get; set; } = new List<string>();
    public List<string> Warehouses { get; set; } = new List<string>();
    public List<int> BomLevels { get; set; } = new List<int>();
    public List<bool> HasChildOptions { get; set; } = new List<bool>();
    public List<string> BuyMethods { get; set; } = new List<string>();
    public List<string> BodyPlanes { get; set; } = new List<string>();
}
