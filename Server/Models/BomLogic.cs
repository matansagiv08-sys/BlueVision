using Server.DAL;

namespace Server.Models;

public class BomLogic
{
    public List<BomRow> GetBomRows(
        int page = 1,
        int pageSize = 100,
        int? planeTypeId = null,
        string? search = null,
        string? measureUnit = null,
        string? warehouse = null,
        int? bomLevel = null,
        bool? hasChild = null,
        string? buyMethod = null,
        string? bodyPlane = null)
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomRows(page, pageSize, planeTypeId, search, measureUnit, warehouse, bomLevel, hasChild, buyMethod, bodyPlane);
    }

    public List<object> GetBomPlaneOptions()
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomPlaneOptions();
    }

    public object GetBomFilterOptions(int? planeTypeId = null)
    {
        DBservices dbs = new DBservices();
        var optionsData = dbs.GetBomFilterOptions(planeTypeId);
        return new 
        {
            MeasureUnits = optionsData.MeasureUnits,
            Warehouses = optionsData.Warehouses,
            BomLevels = optionsData.BomLevels,
            HasChildOptions = optionsData.HasChildOptions,
            BuyMethods = optionsData.BuyMethods,
            BodyPlanes = optionsData.BodyPlanes
        };
    }
}
