using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2016.Excel;
using Server.Models;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Server.DAL;

public class DBservices
{
    private SqlConnection connect(string conString)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        string? connectionString = configuration.GetConnectionString(conString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Connection string '{conString}' was not found in appsettings.json");
        }

        SqlConnection con = new SqlConnection(connectionString);
        con.Open();
        return con;
    }


    // Create the SqlCommand
    private SqlCommand CreateCommandWithStoredProcedureGeneral(String spName, SqlConnection con, Dictionary<string, object> paramDic)
    {

        SqlCommand cmd = new SqlCommand(); // create the command object

        cmd.Connection = con;              // assign the connection to the command object

        cmd.CommandText = spName;      // can be Select, Insert, Update, Delete 

        cmd.CommandTimeout = 10;           // Time to wait for the execution' The default is 30 seconds

        cmd.CommandType = System.Data.CommandType.StoredProcedure; // the type of the command, can also be text

        if (paramDic != null)
            foreach (KeyValuePair<string, object> param in paramDic)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value);

            }
        return cmd;
    }

    public List<InventoryItem> GetInventoryItems(
        int page = 1,
        int pageSize = 100,
        string? search = null,
        string? stockStatus = "all",
        int? planeTypeId = null,
        int? itemGrpID = null,
        string? buyMethod = null,
        int? supplierID = null,
        string? bodyPlane = null,
        DateTime? lastPODate = null)
    {
        List<InventoryItem> items = new List<InventoryItem>();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;

        using SqlConnection con = connect("myProjDB");
        Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@Page", page },
            { "@PageSize", pageSize },
            { "@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim() },
            { "@StockStatus", string.IsNullOrWhiteSpace(stockStatus) ? "all" : stockStatus.Trim() },
            { "@PlaneTypeID", planeTypeId.HasValue ? planeTypeId.Value : DBNull.Value },
            { "@ItemGrpID", itemGrpID.HasValue ? itemGrpID.Value : DBNull.Value },
            { "@BuyMethod", string.IsNullOrWhiteSpace(buyMethod) ? DBNull.Value : buyMethod.Trim() },
            { "@SupplierID", supplierID.HasValue ? supplierID.Value : DBNull.Value },
            { "@BodyPlane", string.IsNullOrWhiteSpace(bodyPlane) ? DBNull.Value : bodyPlane.Trim() },
            { "@LastPODate", lastPODate.HasValue ? lastPODate.Value.Date : DBNull.Value }
        };

        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInventoryItems_GetPaged", con, paramDic);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        using SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            InventoryItem item = new InventoryItem
            {
                InventoryItemID = reader["InventoryItemID"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                ItemGrpID = reader["ItemGrpID"] == DBNull.Value ? null : Convert.ToInt32(reader["ItemGrpID"]),
                ItemGrpName = reader["ItemGrpName"] == DBNull.Value ? null : reader["ItemGrpName"].ToString(),
                BuyMethod = reader["BuyMethod"] == DBNull.Value ? null : reader["BuyMethod"].ToString(),
                Price = reader["Price"] == DBNull.Value ? null : Convert.ToDouble(reader["Price"]),
                SupplierID = reader["SupplierID"] == DBNull.Value ? null : Convert.ToInt32(reader["SupplierID"]),
                SupplierName = reader["SupplierName"] == DBNull.Value ? string.Empty : reader["SupplierName"].ToString() ?? string.Empty,
                Whse01_QTY = reader["Whse01_QTY"] == DBNull.Value ? null : Convert.ToInt32(reader["Whse01_QTY"]),
                Whse03_QTY = reader["Whse03_QTY"] == DBNull.Value ? null : Convert.ToInt32(reader["Whse03_QTY"]),
                Whse90_QTY = reader["Whse90_QTY"] == DBNull.Value ? null : Convert.ToInt32(reader["Whse90_QTY"]),
                OpenPurchaseRequestQty = reader["OpenPurchaseRequestQty"] == DBNull.Value ? null : Convert.ToInt32(reader["OpenPurchaseRequestQty"]),
                OpenPurchaseOrderQty = reader["OpenPurchaseOrderQty"] == DBNull.Value ? null : Convert.ToInt32(reader["OpenPurchaseOrderQty"]),
                ApprovedOrderQty = reader["ApprovedOrderQty"] == DBNull.Value ? null : Convert.ToInt32(reader["ApprovedOrderQty"]),
                UnapprovedOrderQty = reader["UnapprovedOrderQty"] == DBNull.Value ? null : Convert.ToInt32(reader["UnapprovedOrderQty"]),
                BodyPlane = reader["BodyPlane"] == DBNull.Value ? null : reader["BodyPlane"].ToString(),
                LastPODate = reader["LastPODate"] == DBNull.Value ? null : Convert.ToDateTime(reader["LastPODate"])
            };

            items.Add(item);
        }

        return items;
    }

    public List<BomPlaneOption> GetBomPlaneOptions()
    {
        List<BomPlaneOption> options = new List<BomPlaneOption>();

        using SqlConnection con = connect("myProjDB");
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetPlaneOptions", con, null);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        using SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            options.Add(new BomPlaneOption
            {
                PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                PlaneTypeName = reader["PlaneTypeName"]?.ToString() ?? string.Empty
            });
        }

        return options;
    }

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
        List<BomRow> rows = new List<BomRow>();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;

        using SqlConnection con = connect("myProjDB");
        Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@Page", page },
            { "@PageSize", pageSize },
            { "@PlaneTypeID", planeTypeId.HasValue ? planeTypeId.Value : DBNull.Value },
            { "@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim() },
            { "@MeasureUnit", string.IsNullOrWhiteSpace(measureUnit) ? DBNull.Value : measureUnit.Trim() },
            { "@Warehouse", string.IsNullOrWhiteSpace(warehouse) ? DBNull.Value : warehouse.Trim() },
            { "@BomLevel", bomLevel.HasValue ? bomLevel.Value : DBNull.Value },
            { "@HasChild", hasChild.HasValue ? hasChild.Value : DBNull.Value },
            { "@BuyMethod", string.IsNullOrWhiteSpace(buyMethod) ? DBNull.Value : buyMethod.Trim() },
            { "@BodyPlane", string.IsNullOrWhiteSpace(bodyPlane) ? DBNull.Value : bodyPlane.Trim() }
        };

        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetPagedRows", con, paramDic);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        using SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new BomRow
            {
                BomSerialID = Convert.ToInt32(reader["BomSerialID"]),
                PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                RowOrder = Convert.ToInt32(reader["RowOrder"]),
                InventoryItemID = reader["InventoryItemID"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                Quantity = reader["Quantity"] == DBNull.Value ? null : Convert.ToDecimal(reader["Quantity"]),
                MeasureUnit = reader["MeasureUnit"] == DBNull.Value ? null : reader["MeasureUnit"].ToString(),
                Warehouse = reader["Warehouse"] == DBNull.Value ? null : reader["Warehouse"].ToString(),
                BomLevel = reader["BomLevel"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BomLevel"]),
                HasChild = reader["HasChild"] == DBNull.Value ? false : Convert.ToBoolean(reader["HasChild"]),
                BuyMethod = reader["BuyMethod"] == DBNull.Value ? null : reader["BuyMethod"].ToString(),
                BodyPlane = reader["BodyPlane"] == DBNull.Value ? null : reader["BodyPlane"].ToString()
            });
        }

        return rows;
    }

    public BomFilterOptions GetBomFilterOptions(int? planeTypeId = null)
    {
        BomFilterOptions options = new BomFilterOptions();

        using SqlConnection con = connect("myProjDB");
        Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@PlaneTypeID", planeTypeId.HasValue ? planeTypeId.Value : DBNull.Value }
        };

        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetFilterOptions", con, paramDic);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        using SqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read()) options.MeasureUnits.Add(reader["Value"]?.ToString() ?? string.Empty);

        if (reader.NextResult())
        {
            while (reader.Read()) options.Warehouses.Add(reader["Value"]?.ToString() ?? string.Empty);
        }

        if (reader.NextResult())
        {
            while (reader.Read()) options.BomLevels.Add(Convert.ToInt32(reader["BomLevel"]));
        }

        if (reader.NextResult())
        {
            while (reader.Read()) options.HasChildOptions.Add(Convert.ToBoolean(reader["HasChild"]));
        }

        if (reader.NextResult())
        {
            while (reader.Read()) options.BuyMethods.Add(reader["Value"]?.ToString() ?? string.Empty);
        }

        if (reader.NextResult())
        {
            while (reader.Read()) options.BodyPlanes.Add(reader["Value"]?.ToString() ?? string.Empty);
        }

        return options;
    }

    public Dictionary<int, string> GetPlaneTypeNames(List<int> planeTypeIds)
    {
        Dictionary<int, string> planeTypeNames = new Dictionary<int, string>();

        if (planeTypeIds == null || planeTypeIds.Count == 0)
        {
            return planeTypeNames;
        }

        using SqlConnection con = connect("myProjDB");

        string idsCsv = string.Join(",", planeTypeIds.Select((_, i) => $"@PlaneTypeID{i}"));

        StringBuilder planeTypesSql = new StringBuilder();
        planeTypesSql.Append("SELECT PlaneTypeID, PlaneTypeName FROM PlaneTypes WHERE PlaneTypeID IN (");
        planeTypesSql.Append(idsCsv);
        planeTypesSql.Append(")");

        using SqlCommand planeTypesCmd = new SqlCommand(planeTypesSql.ToString(), con);
        planeTypesCmd.CommandType = CommandType.Text;
        planeTypesCmd.CommandTimeout = 120;
        for (int i = 0; i < planeTypeIds.Count; i++)
        {
            planeTypesCmd.Parameters.AddWithValue($"@PlaneTypeID{i}", planeTypeIds[i]);
        }

        using SqlDataReader reader = planeTypesCmd.ExecuteReader();
        while (reader.Read())
        {
            int planeTypeId = Convert.ToInt32(reader["PlaneTypeID"]);
            string planeTypeName = reader["PlaneTypeName"]?.ToString() ?? planeTypeId.ToString();
            planeTypeNames[planeTypeId] = planeTypeName;
        }

        return planeTypeNames;
    }

    public List<BomRow> GetBomRowsForPlanes(List<int> planeTypeIds, string bodyPlane)
    {
        List<BomRow> bomRows = new List<BomRow>();

        if (planeTypeIds == null || planeTypeIds.Count == 0)
        {
            return bomRows;
        }

        using SqlConnection con = connect("myProjDB");

        string idsCsv = string.Join(",", planeTypeIds.Select((_, i) => $"@PlaneTypeID{i}"));

        StringBuilder bomSql = new StringBuilder();
        bomSql.Append("SELECT PlaneTypeID, RowOrder, InventoryItemID, ItemName, Quantity, MeasureUnit, BomLevel, BuyMethod, BodyPlane ");
        bomSql.Append("FROM BOM WHERE PlaneTypeID IN (");
        bomSql.Append(idsCsv);
        bomSql.Append(") AND BodyPlane = @BodyPlane ORDER BY PlaneTypeID, RowOrder");

        using SqlCommand bomCmd = new SqlCommand(bomSql.ToString(), con);
        bomCmd.CommandType = CommandType.Text;
        bomCmd.CommandTimeout = 120;
        for (int i = 0; i < planeTypeIds.Count; i++)
        {
            bomCmd.Parameters.AddWithValue($"@PlaneTypeID{i}", planeTypeIds[i]);
        }
        bomCmd.Parameters.AddWithValue("@BodyPlane", bodyPlane);

        using SqlDataReader reader = bomCmd.ExecuteReader();
        while (reader.Read())
        {
            bomRows.Add(new BomRow
            {
                PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                RowOrder = Convert.ToInt32(reader["RowOrder"]),
                InventoryItemID = reader["InventoryItemID"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                Quantity = reader["Quantity"] == DBNull.Value ? null : Convert.ToDecimal(reader["Quantity"]),
                MeasureUnit = reader["MeasureUnit"] == DBNull.Value ? null : reader["MeasureUnit"].ToString(),
                BomLevel = reader["BomLevel"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BomLevel"]),
                BuyMethod = reader["BuyMethod"] == DBNull.Value ? null : reader["BuyMethod"].ToString(),
                BodyPlane = reader["BodyPlane"] == DBNull.Value ? null : reader["BodyPlane"].ToString()
            });
        }

        return bomRows;
    }

    public Dictionary<string, InventoryCheck.InventorySnapshot> GetInventorySnapshotsForItems(List<string> itemIds)
    {
        Dictionary<string, InventoryCheck.InventorySnapshot> snapshots = new Dictionary<string, InventoryCheck.InventorySnapshot>(StringComparer.OrdinalIgnoreCase);

        if (itemIds.Count == 0)
        {
            return snapshots;
        }

        using SqlConnection con = connect("myProjDB");

        StringBuilder sql = new StringBuilder();
        sql.Append("SELECT i.InventoryItemID, i.ItemName, i.Price, s.SupplierName, ");
        sql.Append("(ISNULL(i.Whse01_QTY,0) + ISNULL(i.Whse03_QTY,0) + ISNULL(i.Whse90_QTY,0)) AS TotalStock ");
        sql.Append("FROM InventoryItems i LEFT JOIN Suppliers s ON s.SupplierID = i.SupplierID WHERE i.InventoryItemID IN (");
        sql.Append(string.Join(",", itemIds.Select((_, idx) => $"@ItemID{idx}")));
        sql.Append(")");

        using SqlCommand cmd = new SqlCommand(sql.ToString(), con);
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 120;

        for (int i = 0; i < itemIds.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@ItemID{i}", itemIds[i]);
        }

        using SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string itemId = reader["InventoryItemID"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            snapshots[itemId] = new InventoryCheck.InventorySnapshot
            {
                ItemName = reader["ItemName"] == DBNull.Value ? string.Empty : reader["ItemName"].ToString() ?? string.Empty,
                TotalStock = reader["TotalStock"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TotalStock"]),
                SupplierName = reader["SupplierName"] == DBNull.Value ? string.Empty : reader["SupplierName"].ToString() ?? string.Empty,
                Price = reader["Price"] == DBNull.Value ? null : Convert.ToDouble(reader["Price"])
            };
        }

        return snapshots;
    }

    public InventoryFilterOptions GetInventoryFilterOptions()
    {
        InventoryFilterOptions options = new InventoryFilterOptions();

        using SqlConnection con = connect("myProjDB");
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInventoryItems_GetFilterOptions", con, null);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        using SqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            options.Platforms.Add(new InventoryPlatformOption
            {
                PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                PlaneTypeName = reader["PlaneTypeName"]?.ToString() ?? string.Empty
            });
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                options.Groups.Add(new InventoryGroupOption
                {
                    ItemGrpID = Convert.ToInt32(reader["ItemGrpID"]),
                    ItemGrpName = reader["ItemGrpName"]?.ToString() ?? string.Empty
                });
            }
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                options.BuyMethods.Add(reader["BuyMethod"]?.ToString() ?? string.Empty);
            }
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                options.Suppliers.Add(new InventorySupplierOption
                {
                    SupplierID = Convert.ToInt32(reader["SupplierID"]),
                    SupplierName = reader["SupplierName"]?.ToString() ?? string.Empty
                });
            }
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                options.BodyPlanes.Add(reader["BodyPlaneValue"]?.ToString() ?? string.Empty);
            }
        }

        return options;
    }

    public InventoryImportResult ImportInventoryItemsFromExcel(string? filePath)
    {
        Console.WriteLine("Import started");
        string finalPath = ResolveExcelPath(filePath);
        Console.WriteLine("Using Excel file: " + finalPath);
        Console.WriteLine("Excel last modified: " + File.GetLastWriteTime(finalPath).ToString("yyyy-MM-dd HH:mm:ss"));

        // Always open a fresh stream from disk for each import run.
        using FileStream excelStream = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XLWorkbook workbook = new XLWorkbook(excelStream);
        Console.WriteLine("Loaded workbook");

        Console.WriteLine("Reading sheet: פריטים ומלאים");
        IXLWorksheet detailsSheet = workbook.Worksheet("פריטים ומלאים");
        Debug.WriteLine("Reading sheet: ספק אחרון לפריט");
        IXLWorksheet supplierSheet = workbook.Worksheet("ספק אחרון לפריט");
        Debug.WriteLine("Reading BOM sheet: עץ מוצר WB");
        IXLWorksheet wbBomSheet = workbook.Worksheet("עץ מוצר WB");
        Debug.WriteLine("Reading BOM sheet: עץ מוצר TBV");
        IXLWorksheet tbvBomSheet = workbook.Worksheet("עץ מוצר TBV");
        Dictionary<string, string> itemToGroupMap = BuildItemToGroupMap(detailsSheet);
        Dictionary<string, string> itemToBuyMethod = BuildItemToBuyMethodMap(detailsSheet);
        Dictionary<string, string> itemToSupplierMap = BuildItemToSupplierMap(supplierSheet);
        Dictionary<string, DateTime> itemToLastPODateMap = BuildItemToLastPODateMap(supplierSheet);
        List<BomRow> wbBomRows = BuildBomRowsForSheet(wbBomSheet, "WB");
        List<BomRow> tbvBomRows = BuildBomRowsForSheet(tbvBomSheet, "TBV");

        Debug.WriteLine("Calculating BodyPlane for BOM rows");
        CalculateBodyPlaneForBomRows(wbBomRows);
        CalculateBodyPlaneForBomRows(tbvBomRows);

        foreach (BomRow row in wbBomRows.Take(10))
        {
            Debug.WriteLine($"WB | RowOrder={row.RowOrder} | BomLevel={row.BomLevel} | BodyPlane={row.BodyPlane}");
        }

        foreach (BomRow row in tbvBomRows.Take(10))
        {
            Debug.WriteLine($"TBV | RowOrder={row.RowOrder} | BomLevel={row.BomLevel} | BodyPlane={row.BodyPlane}");
        }

        Console.WriteLine("Built item-to-group map with " + itemToGroupMap.Count + " entries");
        Console.WriteLine("Built item-to-buyMethod map with " + itemToBuyMethod.Count + " entries");
        Debug.WriteLine("Built item-to-supplier map with " + itemToSupplierMap.Count + " entries");
        Debug.WriteLine("Built item-to-lastPODate map with " + itemToLastPODateMap.Count + " entries");
        Debug.WriteLine("Built " + wbBomRows.Count + " BOM rows for WB");
        Debug.WriteLine("Built " + tbvBomRows.Count + " BOM rows for TBV");

        foreach (var mapping in itemToSupplierMap.Take(5))
        {
            Debug.WriteLine(mapping.Key + " -> " + mapping.Value);
        }

        foreach (BomRow row in wbBomRows.Take(5))
        {
            Debug.WriteLine($"WB | {row.RowOrder} | {row.InventoryItemID} | {row.ItemName} | {row.Quantity} | {row.MeasureUnit} | {row.Warehouse} | {row.BomLevel} | {row.HasChildRaw} | {row.BuyMethod} | {row.BodyPlane}");
        }

        foreach (BomRow row in tbvBomRows.Take(5))
        {
            Debug.WriteLine($"TBV | {row.RowOrder} | {row.InventoryItemID} | {row.ItemName} | {row.Quantity} | {row.MeasureUnit} | {row.Warehouse} | {row.BomLevel} | {row.HasChildRaw} | {row.BuyMethod} | {row.BodyPlane}");
        }

        List<string> uniqueSuppliers = itemToSupplierMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Debug.WriteLine("Found " + uniqueSuppliers.Count + " unique suppliers");

        List<string> uniqueGroupNames = itemToGroupMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine("Found " + uniqueGroupNames.Count + " unique group names");

        int insertedGroups = 0;
        int insertedSuppliers = 0;
        int updatedRows = 0;
        int updatedSupplierRows = 0;
        Dictionary<string, int> groupNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> supplierNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int deletedProductionItems = 0;
        int insertedProductionItems = 0;
        int updatedProductionItems = 0;
        int finalProductionItemsCount = 0;

        using (SqlConnection con = connect("myProjDB"))
        {
            Dictionary<string, int> planeTypeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand selectPlaneTypesCmd = new SqlCommand("SELECT PlaneTypeID, PlaneTypeName FROM PlaneTypes", con))
            {
                selectPlaneTypesCmd.CommandTimeout = 120;
                using SqlDataReader planeTypesReader = selectPlaneTypesCmd.ExecuteReader();
                while (planeTypesReader.Read())
                {
                    int planeTypeId = Convert.ToInt32(planeTypesReader["PlaneTypeID"]);
                    string planeTypeName = planeTypesReader["PlaneTypeName"]?.ToString()?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(planeTypeName))
                    {
                        continue;
                    }

                    planeTypeNameToId[planeTypeName] = planeTypeId;
                }
            }
            Debug.WriteLine("Loaded PlaneTypes dictionary with " + planeTypeNameToId.Count + " entries");

            DataTable bomTable = new DataTable();
            bomTable.Columns.Add("PlaneTypeID", typeof(int));
            bomTable.Columns.Add("RowOrder", typeof(int));
            bomTable.Columns.Add("InventoryItemID", typeof(string));
            bomTable.Columns.Add("ItemName", typeof(string));
            bomTable.Columns.Add("Quantity", typeof(decimal));
            bomTable.Columns.Add("MeasureUnit", typeof(string));
            bomTable.Columns.Add("Warehouse", typeof(string));
            bomTable.Columns.Add("BomLevel", typeof(int));
            bomTable.Columns.Add("HasChild", typeof(bool));
            bomTable.Columns.Add("BuyMethod", typeof(string));
            bomTable.Columns.Add("BodyPlane", typeof(string));

            foreach (BomRow bomRow in wbBomRows.Concat(tbvBomRows))
            {
                if (!planeTypeNameToId.TryGetValue((bomRow.PlaneTypeName ?? string.Empty).Trim(), out int planeTypeId))
                {
                    continue;
                }

                bomRow.PlaneTypeID = planeTypeId;

                bool hasChild = !string.IsNullOrWhiteSpace(bomRow.HasChildRaw)
                    && !string.Equals(bomRow.HasChildRaw.Trim(), "N", StringComparison.OrdinalIgnoreCase);
                bomRow.HasChild = hasChild;

                bomTable.Rows.Add(
                    bomRow.PlaneTypeID,
                    bomRow.RowOrder,
                    bomRow.InventoryItemID,
                    bomRow.ItemName ?? (object)DBNull.Value,
                    bomRow.Quantity ?? 0m,
                    bomRow.MeasureUnit ?? (object)DBNull.Value,
                    bomRow.Warehouse ?? (object)DBNull.Value,
                    bomRow.BomLevel,
                    bomRow.HasChild ?? false,
                    bomRow.BuyMethod ?? (object)DBNull.Value,
                    bomRow.BodyPlane ?? (object)DBNull.Value
                );
            }

            Debug.WriteLine("Prepared " + bomTable.Rows.Count + " BOM rows for insert");

            using (SqlCommand deleteBomCmd = new SqlCommand("TRUNCATE TABLE BOM", con))
            {
                deleteBomCmd.CommandTimeout = 120;
                deleteBomCmd.ExecuteNonQuery();
            }
            Debug.WriteLine("Truncated BOM table");

            if (bomTable.Rows.Count > 0)
            {
                using SqlBulkCopy bomBulkCopy = new SqlBulkCopy(con);
                bomBulkCopy.DestinationTableName = "BOM";
                bomBulkCopy.BulkCopyTimeout = 120;
                bomBulkCopy.BatchSize = 2000;
                bomBulkCopy.ColumnMappings.Add("PlaneTypeID", "PlaneTypeID");
                bomBulkCopy.ColumnMappings.Add("RowOrder", "RowOrder");
                bomBulkCopy.ColumnMappings.Add("InventoryItemID", "InventoryItemID");
                bomBulkCopy.ColumnMappings.Add("ItemName", "ItemName");
                bomBulkCopy.ColumnMappings.Add("Quantity", "Quantity");
                bomBulkCopy.ColumnMappings.Add("MeasureUnit", "MeasureUnit");
                bomBulkCopy.ColumnMappings.Add("Warehouse", "Warehouse");
                bomBulkCopy.ColumnMappings.Add("BomLevel", "BomLevel");
                bomBulkCopy.ColumnMappings.Add("HasChild", "HasChild");
                bomBulkCopy.ColumnMappings.Add("BuyMethod", "BuyMethod");
                bomBulkCopy.ColumnMappings.Add("BodyPlane", "BodyPlane");
                bomBulkCopy.WriteToServer(bomTable);
            }
            Debug.WriteLine("Inserted " + bomTable.Rows.Count + " BOM rows into BOM");

            // Sync ProductionItems from BOM without breaking FK references from ItemsInProduction.
            // Source set: DISTINCT InventoryItemID where BuyMethod='M' and BodyPlane='B'.
            const string createProductionItemsSourceSql = @"
CREATE TABLE #ProductionItemsSource
(
    ProductionItemID NVARCHAR(100) NOT NULL,
    ItemName NVARCHAR(255) NULL
);

INSERT INTO #ProductionItemsSource (ProductionItemID, ItemName)
SELECT
    LTRIM(RTRIM(b.InventoryItemID)) AS ProductionItemID,
    MAX(NULLIF(LTRIM(RTRIM(b.ItemName)), '')) AS ItemName
FROM BOM b
WHERE
    NULLIF(LTRIM(RTRIM(b.InventoryItemID)), '') IS NOT NULL
    AND LTRIM(RTRIM(b.BuyMethod)) = 'M'
    AND LTRIM(RTRIM(b.BodyPlane)) = 'B'
GROUP BY LTRIM(RTRIM(b.InventoryItemID));";

            using (SqlCommand createSourceCmd = new SqlCommand(createProductionItemsSourceSql, con))
            {
                createSourceCmd.CommandTimeout = 120;
                createSourceCmd.ExecuteNonQuery();
            }

            const string insertProductionItemsSql = @"
INSERT INTO ProductionItems (ProductionItemID, ItemName)
SELECT s.ProductionItemID, s.ItemName
FROM #ProductionItemsSource s
LEFT JOIN ProductionItems p ON p.ProductionItemID = s.ProductionItemID
WHERE p.ProductionItemID IS NULL;";

            using (SqlCommand insertProductionItemsCmd = new SqlCommand(insertProductionItemsSql, con))
            {
                insertProductionItemsCmd.CommandTimeout = 120;
                insertedProductionItems = insertProductionItemsCmd.ExecuteNonQuery();
            }

            const string updateProductionItemsSql = @"
UPDATE p
SET p.ItemName = s.ItemName
FROM ProductionItems p
INNER JOIN #ProductionItemsSource s ON s.ProductionItemID = p.ProductionItemID
WHERE ISNULL(p.ItemName, '') <> ISNULL(s.ItemName, '');";

            using (SqlCommand updateProductionItemsCmd = new SqlCommand(updateProductionItemsSql, con))
            {
                updateProductionItemsCmd.CommandTimeout = 120;
                updatedProductionItems = updateProductionItemsCmd.ExecuteNonQuery();
            }

            const string deleteProductionItemsSql = @"
DELETE p
FROM ProductionItems p
WHERE
    NOT EXISTS (SELECT 1 FROM #ProductionItemsSource s WHERE s.ProductionItemID = p.ProductionItemID)
    AND NOT EXISTS (SELECT 1 FROM ItemsInProduction iip WHERE iip.ProductionItemID = p.ProductionItemID);";

            using (SqlCommand deleteProductionItemsCmd = new SqlCommand(deleteProductionItemsSql, con))
            {
                deleteProductionItemsCmd.CommandTimeout = 120;
                deletedProductionItems = deleteProductionItemsCmd.ExecuteNonQuery();
            }

            using (SqlCommand countProductionItemsCmd = new SqlCommand("SELECT COUNT(*) FROM ProductionItems", con))
            {
                countProductionItemsCmd.CommandTimeout = 120;
                object? scalar = countProductionItemsCmd.ExecuteScalar();
                finalProductionItemsCount = scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt32(scalar);
            }

            if (insertedProductionItems < 0)
            {
                insertedProductionItems = finalProductionItemsCount;
            }

            if (updatedProductionItems < 0)
            {
                updatedProductionItems = 0;
            }

            Debug.WriteLine("Synced ProductionItems from BOM: inserted=" + insertedProductionItems + ", updated=" + updatedProductionItems + ", deleted=" + deletedProductionItems + ", finalCount=" + finalProductionItemsCount);

            const string insertSupplierIfMissingSql = @"
IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE SupplierName = @SupplierName)
BEGIN
    INSERT INTO Suppliers (SupplierName)
    VALUES (@SupplierName)
END";

            using (SqlCommand supplierCmd = new SqlCommand(insertSupplierIfMissingSql, con))
            {
                supplierCmd.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 100);
                supplierCmd.CommandTimeout = 120;

                foreach (string supplierName in uniqueSuppliers)
                {
                    supplierCmd.Parameters["@SupplierName"].Value = supplierName;
                    int affectedRows = supplierCmd.ExecuteNonQuery();
                    if (affectedRows > 0)
                    {
                        insertedSuppliers++;
                    }
                }
            }
            Debug.WriteLine("Inserted " + insertedSuppliers + " new suppliers");

            {
                using SqlCommand selectSuppliersCmd = new SqlCommand("SELECT SupplierID, SupplierName FROM Suppliers", con);
                selectSuppliersCmd.CommandTimeout = 120;
                using SqlDataReader supplierReader = selectSuppliersCmd.ExecuteReader();
                while (supplierReader.Read())
                {
                    int supplierId = Convert.ToInt32(supplierReader["SupplierID"]);
                    string supplierName = supplierReader["SupplierName"]?.ToString()?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(supplierName))
                    {
                        continue;
                    }

                    supplierNameToId[supplierName] = supplierId;
                }
            }

            Debug.WriteLine("Loaded " + supplierNameToId.Count + " suppliers from database");
            foreach (var supplier in supplierNameToId.Take(5))
            {
                Debug.WriteLine(supplier.Key + " -> " + supplier.Value);
            }

            DataTable supplierUpdatesTable = new DataTable();
            supplierUpdatesTable.Columns.Add("ItemCode", typeof(string));
            supplierUpdatesTable.Columns.Add("SupplierID", typeof(int));

            foreach (var mapping in itemToSupplierMap)
            {
                string itemCode = mapping.Key;
                string supplierName = mapping.Value?.Trim() ?? string.Empty;

                if (!supplierNameToId.TryGetValue(supplierName, out int supplierId))
                {
                    continue;
                }

                supplierUpdatesTable.Rows.Add(itemCode, supplierId);
            }

            Debug.WriteLine("Prepared " + supplierUpdatesTable.Rows.Count + " supplier updates");

            if (supplierUpdatesTable.Rows.Count > 0)
            {
                const string createSupplierTempTableSql = @"
CREATE TABLE #SupplierUpdates
(
    ItemCode NVARCHAR(100) NOT NULL,
    SupplierID INT NOT NULL
)";

                using (SqlCommand createTempCmd = new SqlCommand(createSupplierTempTableSql, con))
                {
                    createTempCmd.CommandTimeout = 120;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#SupplierUpdates";
                    bulkCopy.BulkCopyTimeout = 120;
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("SupplierID", "SupplierID");
                    bulkCopy.WriteToServer(supplierUpdatesTable);
                }
                Debug.WriteLine("Bulk copied supplier temp table");

                const string updateSupplierSql = @"
UPDATE i
SET i.SupplierID = u.SupplierID
FROM InventoryItems i
INNER JOIN #SupplierUpdates u
    ON i.InventoryItemID = u.ItemCode";

                using SqlCommand updateSupplierCmd = new SqlCommand(updateSupplierSql, con);
                updateSupplierCmd.CommandTimeout = 120;
                updatedSupplierRows = updateSupplierCmd.ExecuteNonQuery();
            }

            Debug.WriteLine("Updated " + updatedSupplierRows + " inventory supplier rows");

            DataTable lastPoDateUpdatesTable = new DataTable();
            lastPoDateUpdatesTable.Columns.Add("ItemCode", typeof(string));
            lastPoDateUpdatesTable.Columns.Add("LastPODate", typeof(DateTime));

            foreach (var mapping in itemToLastPODateMap)
            {
                lastPoDateUpdatesTable.Rows.Add(mapping.Key, mapping.Value.Date);
            }

            Debug.WriteLine("Prepared " + lastPoDateUpdatesTable.Rows.Count + " LastPODate updates");

            int updatedLastPoDateRows = 0;
            if (lastPoDateUpdatesTable.Rows.Count > 0)
            {
                const string createLastPoDateTempTableSql = @"
CREATE TABLE #LastPODateUpdates
(
    ItemCode NVARCHAR(100) NOT NULL,
    LastPODate DATE NOT NULL
)";

                using (SqlCommand createTempCmd = new SqlCommand(createLastPoDateTempTableSql, con))
                {
                    createTempCmd.CommandTimeout = 120;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#LastPODateUpdates";
                    bulkCopy.BulkCopyTimeout = 120;
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("LastPODate", "LastPODate");
                    bulkCopy.WriteToServer(lastPoDateUpdatesTable);
                }
                Debug.WriteLine("Bulk copied LastPODate temp table");

                const string updateLastPoDateSql = @"
UPDATE i
SET i.LastPODate = u.LastPODate
FROM InventoryItems i
INNER JOIN #LastPODateUpdates u
    ON i.InventoryItemID = u.ItemCode";

                using SqlCommand updateLastPoDateCmd = new SqlCommand(updateLastPoDateSql, con);
                updateLastPoDateCmd.CommandTimeout = 120;
                updatedLastPoDateRows = updateLastPoDateCmd.ExecuteNonQuery();
            }

            Debug.WriteLine("Updated " + updatedLastPoDateRows + " inventory LastPODate rows");

            const string insertIfMissingSql = @"
IF NOT EXISTS (SELECT 1 FROM Groups WHERE ItemGrpName = @GroupName)
BEGIN
    INSERT INTO Groups (ItemGrpName)
    VALUES (@GroupName)
END";

            using SqlCommand cmd = new SqlCommand(insertIfMissingSql, con);
            cmd.Parameters.Add("@GroupName", SqlDbType.NVarChar, 255);
            cmd.CommandTimeout = 120;

            foreach (string groupName in uniqueGroupNames)
            {
                cmd.Parameters["@GroupName"].Value = groupName;
                int affectedRows = cmd.ExecuteNonQuery();
                if (affectedRows > 0)
                {
                    insertedGroups++;
                }
            }
            Console.WriteLine("Inserted " + insertedGroups + " new groups");

            {
                using SqlCommand selectGroupsCmd = new SqlCommand("SELECT ItemGrpID, ItemGrpName FROM Groups", con);
                selectGroupsCmd.CommandTimeout = 120;
                using SqlDataReader reader = selectGroupsCmd.ExecuteReader();
                while (reader.Read())
                {
                    int itemGrpId = Convert.ToInt32(reader["ItemGrpID"]);
                    string groupName = reader["ItemGrpName"]?.ToString()?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(groupName))
                    {
                        continue;
                    }

                    groupNameToId[groupName] = itemGrpId;
                }
            }
            Console.WriteLine("Loaded " + groupNameToId.Count + " groups from database");

            DataTable updatesTable = new DataTable();
            updatesTable.Columns.Add("ItemCode", typeof(string));
            updatesTable.Columns.Add("ItemGrpID", typeof(int));

            foreach (var mapping in itemToGroupMap)
            {
                string itemCode = mapping.Key;
                string groupName = mapping.Value?.Trim() ?? string.Empty;

                if (!groupNameToId.TryGetValue(groupName, out int itemGrpId))
                {
                    continue;
                }

                updatesTable.Rows.Add(itemCode, itemGrpId);
            }

            Console.WriteLine("Prepared " + updatesTable.Rows.Count + " inventory item/group matches for update");

            if (updatesTable.Rows.Count > 0)
            {
                const string createTempTableSql = @"
CREATE TABLE #ItemGroupUpdates
(
    ItemCode NVARCHAR(100) NOT NULL,
    ItemGrpID INT NOT NULL
)";

                using (SqlCommand createTempCmd = new SqlCommand(createTempTableSql, con))
                {
                    createTempCmd.CommandTimeout = 120;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#ItemGroupUpdates";
                    bulkCopy.BulkCopyTimeout = 120;
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("ItemGrpID", "ItemGrpID");
                    bulkCopy.WriteToServer(updatesTable);
                }
                Console.WriteLine("Bulk copied temp update table");

                const string updateInventorySql = @"
UPDATE i
SET i.ItemGrpID = u.ItemGrpID
FROM InventoryItems i
INNER JOIN #ItemGroupUpdates u
    ON i.InventoryItemID = u.ItemCode";

                using SqlCommand updateCmd = new SqlCommand(updateInventorySql, con);
                updateCmd.CommandTimeout = 120;
                updatedRows = updateCmd.ExecuteNonQuery();
            }

            Console.WriteLine("Updated " + updatedRows + " inventory rows");

            Console.WriteLine("Updating BuyMethod for " + itemToBuyMethod.Count + " items");

            DataTable buyMethodUpdatesTable = new DataTable();
            buyMethodUpdatesTable.Columns.Add("ItemCode", typeof(string));
            buyMethodUpdatesTable.Columns.Add("BuyMethod", typeof(string));

            foreach (var mapping in itemToBuyMethod)
            {
                buyMethodUpdatesTable.Rows.Add(mapping.Key, mapping.Value);
            }

            Console.WriteLine("Prepared " + buyMethodUpdatesTable.Rows.Count + " BuyMethod updates");

            int updatedBuyMethodRows = 0;
            if (buyMethodUpdatesTable.Rows.Count > 0)
            {
                const string createBuyMethodTempTableSql = @"
CREATE TABLE #BuyMethodUpdates
(
    ItemCode NVARCHAR(100) NOT NULL,
    BuyMethod CHAR(1) NOT NULL
)";

                using (SqlCommand createTempCmd = new SqlCommand(createBuyMethodTempTableSql, con))
                {
                    createTempCmd.CommandTimeout = 120;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#BuyMethodUpdates";
                    bulkCopy.BulkCopyTimeout = 120;
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("BuyMethod", "BuyMethod");
                    bulkCopy.WriteToServer(buyMethodUpdatesTable);
                }
                Console.WriteLine("Bulk copied BuyMethod temp table");

                const string updateBuyMethodSql = @"
UPDATE i
SET i.BuyMethod = u.BuyMethod
FROM InventoryItems i
INNER JOIN #BuyMethodUpdates u
    ON i.InventoryItemID = u.ItemCode";

                using SqlCommand updateBuyMethodCmd = new SqlCommand(updateBuyMethodSql, con);
                updateBuyMethodCmd.CommandTimeout = 120;
                updatedBuyMethodRows = updateBuyMethodCmd.ExecuteNonQuery();
            }

            Console.WriteLine("Updated " + updatedBuyMethodRows + " inventory BuyMethod rows");
        }

        Console.WriteLine("Import finished successfully");
        return new InventoryImportResult
        {
            ImportedRows = updatedRows,
            DeletedProductionItems = deletedProductionItems,
            InsertedProductionItems = insertedProductionItems,
            UpdatedProductionItems = updatedProductionItems,
            FinalProductionItemsCount = finalProductionItemsCount
        };
    }

    private static Dictionary<string, string> BuildItemToGroupMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToGroupMap = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            string itmsGrpNam = row.Cell(3).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(itmsGrpNam))
            {
                continue;
            }

            // If ItemCode appears more than once, keep the latest value.
            itemToGroupMap[itemCode] = itmsGrpNam;
        }

        return itemToGroupMap;
    }

    private static Dictionary<string, string> BuildItemToBuyMethodMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToBuyMethod = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            string buyMethod = row.Cell(4).GetValue<string>().Trim().ToUpper();
            if (buyMethod != "B" && buyMethod != "M")
            {
                continue;
            }

            itemToBuyMethod[itemCode] = buyMethod;
        }

        return itemToBuyMethod;
    }

    private static Dictionary<string, string> BuildItemToSupplierMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToSupplier = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            string lastVendor = row.Cell(2).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(lastVendor))
            {
                continue;
            }

            // If ItemCode appears more than once, keep the latest value.
            itemToSupplier[itemCode] = lastVendor;
        }

        return itemToSupplier;
    }

    private static Dictionary<string, DateTime> BuildItemToLastPODateMap(IXLWorksheet sheet)
    {
        Dictionary<string, DateTime> itemToLastPODate = new Dictionary<string, DateTime>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            IXLCell dateCell = row.Cell(3);
            if (dateCell.IsEmpty())
            {
                continue;
            }

            DateTime parsedDate;
            bool validDate = false;

            if (dateCell.TryGetValue<DateTime>(out parsedDate))
            {
                validDate = true;
            }
            else
            {
                string rawDate = dateCell.GetValue<string>().Trim();
                if (DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate) ||
                    DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate) ||
                    DateTime.TryParse(rawDate, new CultureInfo("he-IL"), DateTimeStyles.None, out parsedDate))
                {
                    validDate = true;
                }
            }

            if (!validDate)
            {
                continue;
            }

            itemToLastPODate[itemCode] = parsedDate.Date;
        }

        return itemToLastPODate;
    }

    private static List<BomRow> BuildBomRowsForSheet(IXLWorksheet sheet, string planeTypeName)
    {
        List<BomRow> rows = new List<BomRow>();
        int rowOrder = 1;

        foreach (IXLRow row in sheet.RowsUsed().Where(r => r.RowNumber() >= 4))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            string itemName = row.Cell(2).GetValue<string>().Trim();
            string measureUnit = row.Cell(3).GetValue<string>().Trim();
            decimal quantity = ToSafeDecimal(row.Cell(4));
            string warehouse = row.Cell(5).GetValue<string>().Trim();
            int bomLevel = ToSafeInt(row.Cell(6));
            string hasChildRaw = row.Cell(7).GetValue<string>().Trim();
            string buyMethod = row.Cell(8).GetValue<string>().Trim();

            rows.Add(new BomRow
            {
                PlaneTypeName = planeTypeName,
                RowOrder = rowOrder,
                InventoryItemID = itemCode,
                ItemName = NullIfEmpty(itemName),
                Quantity = quantity,
                MeasureUnit = NullIfEmpty(measureUnit),
                Warehouse = NullIfEmpty(warehouse),
                BomLevel = bomLevel,
                HasChildRaw = NullIfEmpty(hasChildRaw),
                BuyMethod = NullIfEmpty(buyMethod),
                BodyPlane = null
            });

            rowOrder++;
        }

        return rows;
    }

    private static void CalculateBodyPlaneForBomRows(List<BomRow> rows)
    {
        int level2CounterInBlock = 0;

        foreach (BomRow row in rows.OrderBy(r => r.RowOrder))
        {
            if (row.BomLevel == 1)
            {
                row.BodyPlane = null;
                level2CounterInBlock = 0;
                continue;
            }

            if (row.BomLevel == 2)
            {
                level2CounterInBlock++;
                row.BodyPlane = level2CounterInBlock == 1 ? "B" : "P";
                continue;
            }

            if (level2CounterInBlock == 1)
            {
                row.BodyPlane = "B";
            }
            else if (level2CounterInBlock >= 2)
            {
                row.BodyPlane = "P";
            }
            else
            {
                row.BodyPlane = null;
            }
        }
    }

    private static decimal ToSafeDecimal(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return 0;
        }

        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToDecimal(cell.GetDouble());
        }

        string raw = cell.GetValue<string>().Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out double currentCultureValue))
        {
            return Convert.ToDecimal(currentCultureValue);
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariantValue))
        {
            return Convert.ToDecimal(invariantValue);
        }

        return 0;
    }

    private static Dictionary<string, int> BuildSingleValueDictionary(IXLWorksheet sheet, int valueColumnIndex)
    {
        Dictionary<string, int> dictionary = new Dictionary<string, int>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            dictionary[itemCode] = ToSafeInt(row.Cell(valueColumnIndex));
        }

        return dictionary;
    }

    private static Dictionary<string, (int OpenPo, int Approved, int Unapproved)> BuildOpenPoDictionary(IXLWorksheet sheet)
    {
        Dictionary<string, (int OpenPo, int Approved, int Unapproved)> dictionary = new Dictionary<string, (int OpenPo, int Approved, int Unapproved)>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1));
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            dictionary[itemCode] = (
                ToSafeInt(row.Cell(2)),
                ToSafeInt(row.Cell(3)),
                ToSafeInt(row.Cell(4))
            );
        }

        return dictionary;
    }

    private static int ToSafeInt(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return 0;
        }

        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToInt32(Math.Round(cell.GetDouble(), MidpointRounding.AwayFromZero));
        }

        string raw = cell.GetValue<string>().Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariantNumber))
        {
            return Convert.ToInt32(Math.Round(invariantNumber, MidpointRounding.AwayFromZero));
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out double currentCultureNumber))
        {
            return Convert.ToInt32(Math.Round(currentCultureNumber, MidpointRounding.AwayFromZero));
        }

        return 0;
    }

    private string ResolveExcelPath(string? filePath)
    {
        string finalPath;

        bool missingFromRequest = string.IsNullOrWhiteSpace(filePath)
            || string.Equals(filePath.Trim(), "null", StringComparison.OrdinalIgnoreCase);

        if (!missingFromRequest)
        {
            finalPath = filePath.Trim();
        }
        else
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            finalPath = (configuration["InventoryImport:DefaultExcelPath"] ?? string.Empty).Trim();
        }

        if (string.IsNullOrWhiteSpace(finalPath))
        {
            throw new Exception("Excel file path was not provided and InventoryImport:DefaultExcelPath is empty in appsettings.json.");
        }

        if (!File.Exists(finalPath))
        {
            throw new Exception($"Excel file was not found at path: {finalPath}");
        }

        return finalPath;
    }

    private static string? NullIfEmpty(string value)
    {
        string clean = value.Trim();
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string GetExcelCellTextPreserveFormatting(IXLCell cell)
    {
        if (cell == null || cell.IsEmpty())
            return string.Empty;

        return cell.GetFormattedString().Trim();
    }




    //ייצור
    public List<ItemInProduction> GetTasksBoard()
    {
        SqlConnection con = null;
        // מילון לאיחוד השורות לפי המספר הסידורי של הפריט
        Dictionary<int, ItemInProduction> itemsMap = new Dictionary<int, ItemInProduction>();

        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetProductionBoardData", con, null);
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int sn = Convert.ToInt32(reader["SerialNumber"]);
                if (!itemsMap.ContainsKey(sn))
                {
                    itemsMap[sn] = new ItemInProduction
                    {
                        SerialNumber = sn,
                        WorkOrderID = Convert.ToInt32(reader["WorkOrderID"]),
                        ProductionItem = new ProductionItem { ProductionItemID = reader["ProductionItemID"].ToString() },
                        PlannedQty = Convert.ToInt32(reader["PlannedQty"]),
                        PlaneID = new Plane
                        {
                            Type = new PlaneType
                            {
                                PlaneTypeName = reader["PlaneTypeName"].ToString()
                            }
                        },
                        Stages = new List<ProductionItemStage>()
                    };
                }
                itemsMap[sn].Stages.Add(new ProductionItemStage
                {
                    Stage = new ProductionStage
                    {
                        ProductionStageID = Convert.ToInt32(reader["ProductionStageID"]),
                        ProductionStageName = reader["ProductionStageName"].ToString()
                    },
                    Status = new ProductionStatus
                    {
                        ProductionStatusID = Convert.ToInt32(reader["ProductionStatusID"]),
                        ProductionStatusName = reader["StatusName"].ToString()
                    },
                    Comment = reader["Comment"].ToString()
                });
            }
            return itemsMap.Values.ToList();
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public List<Project> GetProjects()
    {
        SqlConnection con = connect("myProjDB");
        List<Project> list = new List<Project>();
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetAllProjects", con, null);
        SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Project
            {
                ProjectID = (int)reader["ProjectID"],
                ProjectName = reader["ProjectName"].ToString(),
                DueDate = (DateTime)reader["DueDate"],
                PriorityLevel = (byte)reader["PriorityLevel"]
            });
        }
        con.Close();
        return list;
    }

    public int InsertProject(Project p)
    {
        Dictionary<string, object> d = new Dictionary<string, object> {
        {"@ProjectName", p.ProjectName},
        {"@DueDate", p.DueDate},
        {"@PriorityLevel", p.PriorityLevel}
    };
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInsertProject", connect("myProjDB"), d);
        return cmd.ExecuteNonQuery();
    }

    public int UpdateProject(Project p)
    {
        Dictionary<string, object> d = new Dictionary<string, object> {
        {"@ProjectID", p.ProjectID}, {"@ProjectName", p.ProjectName},
        {"@DueDate", p.DueDate}, {"@PriorityLevel", p.PriorityLevel}
    };
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUpdateProject", connect("myProjDB"), d);
        return cmd.ExecuteNonQuery();
    }

    public int DeleteProject(int id)
    {
        Dictionary<string, object> d = new Dictionary<string, object> { { "@ProjectID", id } };
        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spDeleteProject", connect("myProjDB"), d);
        return cmd.ExecuteNonQuery();
    }

   
    public List<PlaneType> GetPlaneTypes()
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB"); 
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetPlaneTypes", con, new Dictionary<string, object>());
            SqlDataReader reader = cmd.ExecuteReader();
            List<PlaneType> list = new List<PlaneType>();

            while (reader.Read())
            {
                list.Add(new PlaneType
                {
                    PlaneTypeID = (int)reader["PlaneTypeID"],
                    PlaneTypeName = reader["PlaneTypeName"].ToString()
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public int InsertPlaneType(PlaneType pt)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@PlaneTypeName", pt.PlaneTypeName }
        };
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInsertPlaneType", con, paramDic);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public int DeletePlaneType(int id)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@PlaneTypeID", id }
        };
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spDeletePlaneType", con, paramDic);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }
    public List<ProductionStage> GetProductionStages()
    {
        SqlConnection con = null;
        List<ProductionStage> stagesList = new List<ProductionStage>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetProductionStages", con, null);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ProductionStage stage = new ProductionStage();

                stage.ProductionStageID = Convert.ToInt32(reader["ProductionStageID"]);
                stage.ProductionStageName = reader["ProductionStageName"].ToString();
                if (reader["TargetDuration"] != DBNull.Value)
                {
                    stage.TargetDuration = (TimeSpan)reader["TargetDuration"];
                }
                else
                {
                    stage.TargetDuration = TimeSpan.Zero;
                }
                stage.StageOrder = Convert.ToInt32(reader["StageOrder"]);
                stagesList.Add(stage);
            }
            return stagesList;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public List<Project> GetFullProjectsStatus()
    {
        SqlConnection con = null;
        Dictionary<int, Project> projectsMap = new Dictionary<int, Project>();
        Dictionary<int, Plane> planesMap = new Dictionary<int, Plane>();
        Dictionary<string, ItemInProduction> itemsMap = new Dictionary<string, ItemInProduction>();

        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("sp_GetFullProjectsStatus", con, null);
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int pID = Convert.ToInt32(reader["ProjectID"]);
                if (!projectsMap.ContainsKey(pID))
                {
                    projectsMap[pID] = new Project
                    {
                        ProjectID = pID,
                        ProjectName = reader["ProjectName"].ToString(),
                       
                        DueDate = reader["DueDate"] != DBNull.Value ? Convert.ToDateTime(reader["DueDate"]) : DateTime.MinValue,
                        PriorityLevel = reader["ProjectPriority"] != DBNull.Value ? Convert.ToInt32(reader["ProjectPriority"]) : 0,
                        Planes = new List<Plane>()
                    };
                }


                if (reader["PlaneID"] != DBNull.Value)
                {
                    int plID = Convert.ToInt32(reader["PlaneID"]);
                    if (!planesMap.ContainsKey(plID))
                    {
                        planesMap[plID] = new Plane
                        {
                            PlaneID = plID,
                            ProjectID = pID,
                            PriorityLevel = reader["PlanePriority"] != DBNull.Value ? Convert.ToInt32(reader["PlanePriority"]) : 0,
                            Items = new List<ItemInProduction>(),
                            Type = new PlaneType
                            {
                                PlaneTypeID = reader["PlaneTypeID"] != DBNull.Value ? Convert.ToInt32(reader["PlaneTypeID"]) : 0,
                                PlaneTypeName = reader["PlaneTypeName"]?.ToString() ?? "UAV"
                            }
                        };
                        projectsMap[pID].Planes.Add(planesMap[plID]);
                    }


                    if (reader["SerialNumber"] != DBNull.Value)
                    {
                        int sn = Convert.ToInt32(reader["SerialNumber"]);
                        string itemKey = pID + "_" + plID + "_" + sn; 

                        if (!itemsMap.ContainsKey(itemKey))
                        {
                            itemsMap[itemKey] = new ItemInProduction
                            {
                                SerialNumber = sn,
                                WorkOrderID = reader["WorkOrderID"] != DBNull.Value ? Convert.ToInt32(reader["WorkOrderID"]) : 0,
                                PlannedQty = reader["PlannedQty"] != DBNull.Value ? Convert.ToInt32(reader["PlannedQty"]) : 0,
                                Comments = reader["Comments"]?.ToString() ?? "",
                                ProductionItem = new ProductionItem
                                {
                                    ProductionItemID = reader["ProductionItemID"]?.ToString() ?? "-",
                                    ItemName = reader["ItemName"]?.ToString() ?? "-"
                                },
                                Stages = new List<ProductionItemStage>()
                            };
                            planesMap[plID].Items.Add(itemsMap[itemKey]);
                        }

                        if (reader["ProductionStageID"] != DBNull.Value)
                        {
                            var stageStatus = new ProductionItemStage
                            {
                                Status = new ProductionStatus
                                {
                                    ProductionStatusID = reader["ProductionStatusID"] != DBNull.Value ? Convert.ToInt32(reader["ProductionStatusID"]) : 1,
                                    ProductionStatusName = reader["ProductionStatusName"]?.ToString() ?? "טרם בוצע"
                                },
                                Stage = new ProductionStage
                                {
                                    ProductionStageID = Convert.ToInt32(reader["ProductionStageID"]),
                                    ProductionStageName = reader["ProductionStageName"]?.ToString() ?? "-",
                                    StageOrder = reader["StageOrder"] != DBNull.Value ? Convert.ToInt32(reader["StageOrder"]) : 0
                                }
                            };
                            itemsMap[itemKey].Stages.Add(stageStatus);
                        }
                    }
                }
            }
            return projectsMap.Values.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetFullProjectsStatus: " + ex.Message);
            throw ex;
        }
        finally { if (con != null) con.Close(); }
    }

    public List<ProductionItem> GetProductionItems()
    {
        SqlConnection con = null;
        List<ProductionItem> list = new List<ProductionItem>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spProductionItems_GetFromBom", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductionItem
                {
                    ProductionItemID = reader["ProductionItemID"].ToString(),
                    ItemName = reader["ItemName"] == DBNull.Value ? string.Empty : reader["ItemName"].ToString()
                });
            }
            return list;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public List<int> GetUniqueWorkOrders()
    {
        SqlConnection con = null;
        List<int> list = new List<int>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spWorkOrders_GetDistinctFromItemsInProduction", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Convert.ToInt32(reader["WorkOrderID"]));
            }
            return list;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public List<object> GetPriorityLevels()
    {
        SqlConnection con = null;
        List<object> list = new List<object>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spPriorityLevels_GetAll", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    ID = Convert.ToInt32(reader["PriorityID"]),
                    Name = reader["PriorityName"].ToString()
                });
            }
            return list;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public List<object> GetPlanes()
    {
        SqlConnection con = null;
        List<object> list = new List<object>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spPlanes_GetBasic", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    PlaneID = reader["PlaneID"].ToString(),
                    TypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                    ProjectID = Convert.ToInt32(reader["ProjectID"])
                });
            }
            return list;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    public int InsertItemInProduction(System.Text.Json.Nodes.JsonObject item)
    {
        SqlConnection con = null;
        SqlTransaction trans = null;

        try
        {
            con = connect("myProjDB");
            if (con.State != System.Data.ConnectionState.Open) con.Open();
            trans = con.BeginTransaction();

            string projectName = item["ProjectName"]?.ToString();
            string planeID = item["PlaneID"]?.ToString();
            string productionItemID = item["ProductionItemID"]?.ToString();
            string workOrderID = item["WorkOrderID"]?.ToString();
            int serialNumber = item["SerialNumber"]?.GetValue<int>() ?? 0;
            int planeTypeID = item["PlaneTypeID"]?.GetValue<int>() ?? 0;

            HandleProjectAndPlane(con, trans, projectName, planeID, planeTypeID);
            HandleWorkOrder(con, trans, workOrderID); 

            string insertSql = @"INSERT INTO ItemsInProduction 
                            (ProductionItemID, SerialNumber, PlaneID, PriorityLevel, WorkOrderID, PlannedQty, Comments) 
                            VALUES (@itemID, @serial, @planeID, @priority, @workOrder, @qty, @comments)";

            using (SqlCommand mainCmd = new SqlCommand(insertSql, con, trans))
            {
                mainCmd.Parameters.AddWithValue("@itemID", productionItemID);
                mainCmd.Parameters.AddWithValue("@serial", serialNumber);
                mainCmd.Parameters.AddWithValue("@planeID", (object)planeID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@priority", item["PriorityID"]?.GetValue<int>() ?? 1);
                mainCmd.Parameters.AddWithValue("@workOrder", (object)workOrderID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@qty", item["Quantity"]?.GetValue<int>() ?? 1);
                mainCmd.Parameters.AddWithValue("@comments", (object)item["Comments"]?.ToString() ?? DBNull.Value);
                mainCmd.ExecuteNonQuery();
            }

            InsertStagesForProduct(con, trans, serialNumber, productionItemID);

            trans.Commit();
            return 1;
        }
        catch (Exception ex)
        {
            if (trans != null) trans.Rollback();
            throw ex;
        }
        finally { if (con != null) con.Close(); }
    }

    private void HandleWorkOrder(SqlConnection con, SqlTransaction trans, string workOrderID)
    {
        if (string.IsNullOrEmpty(workOrderID)) return;

        string sql = "IF NOT EXISTS (SELECT 1 FROM WorkOrders WHERE WorkOrderID = @woID) INSERT INTO WorkOrders (WorkOrderID) VALUES (@woID)";
        using (SqlCommand cmd = new SqlCommand(sql, con, trans))
        {
            cmd.Parameters.AddWithValue("@woID", workOrderID);
            cmd.ExecuteNonQuery();
        }
    }

    private void HandleProjectAndPlane(SqlConnection con, SqlTransaction trans, string projectName, string planeID, int planeTypeID)
    {
        if (!string.IsNullOrEmpty(projectName))
        {
            string sqlProj = "IF NOT EXISTS (SELECT 1 FROM Projects WHERE ProjectName = @pName) INSERT INTO Projects (ProjectName) VALUES (@pName)";
            using (SqlCommand cmd = new SqlCommand(sqlProj, con, trans))
            {
                cmd.Parameters.AddWithValue("@pName", projectName);
                cmd.ExecuteNonQuery();
            }
        }

        if (!string.IsNullOrEmpty(planeID))
        {
            string sqlPlane = @"IF NOT EXISTS (SELECT 1 FROM Planes WHERE PlaneID = @planeID) 
                            INSERT INTO Planes (PlaneID, PlaneTypeID, ProjectID) 
                            SELECT @planeID, @typeID, ProjectID FROM Projects WHERE ProjectName = @pName";
            using (SqlCommand cmd = new SqlCommand(sqlPlane, con, trans))
            {
                cmd.Parameters.AddWithValue("@planeID", planeID);
                cmd.Parameters.AddWithValue("@typeID", planeTypeID);
                cmd.Parameters.AddWithValue("@pName", (object)projectName ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
    }
    private void InsertStagesForProduct(SqlConnection con, SqlTransaction trans, int serialNumber, string productionItemID)
    {
        string query = @"INSERT INTO ProductionItemStage (SerialNumber, ProductionItemID, ProductionStageID, ProductionStatusID)
                     SELECT @serial, @itemID, ProductionStageID, 1 
                     FROM ProductionStages";

        using (SqlCommand cmd = new SqlCommand(query, con, trans))
        {
            cmd.Parameters.AddWithValue("@serial", serialNumber);
            cmd.Parameters.AddWithValue("@itemID", productionItemID);
            cmd.ExecuteNonQuery();
        }
    }
    public int UpdateStageStatus(int serial, string itemID, int stageID, int newStatusID, string comment, DateTime? userTime, bool resetFuture)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");

            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@Serial", serial },
            { "@ItemID", itemID },
            { "@StageID", stageID },
            { "@NewStatusID", newStatusID },
            { "@Comment", (object)comment ?? DBNull.Value },
            { "@UserTime", (object)userTime ?? DBNull.Value },
            { "@ResetFuture", resetFuture } // הוספת הפרמטר שחסר ל-SQL
        };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spItemsInProduction_UpdateStageStatus", con, paramDic);
            // שים לב: CreateCommandWithStoredProcedureGeneral כבר מגדירה CommandType ו-Parameters
            cmd.ExecuteNonQuery();

            return 1;
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally { if (con != null) con.Close(); }
    }
}
