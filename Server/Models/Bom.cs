using Server.DAL;

namespace Server.Models;

public class Bom
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

    public List<BomPlaneOption> GetBomPlaneOptions()
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomPlaneOptions();
    }

    public BomFilterOptions GetBomFilterOptions(int? planeTypeId = null)
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomFilterOptions(planeTypeId);
    }
}
