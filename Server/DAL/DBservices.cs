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
        string environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString(conString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Connection string '{conString}' was not found in configuration");
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

        cmd.CommandType = System.Data.CommandType.StoredProcedure; // the type of the command, can also be text

        if (paramDic != null)
            foreach (KeyValuePair<string, object> param in paramDic)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value);

            }
        return cmd;
    }

    private void ExecuteStoredProcedure(SqlConnection con, string procedureName)
    {
        try
        {
            using SqlCommand cmd = new SqlCommand(procedureName, con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.ExecuteNonQuery();
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            throw new Exception($"Required stored procedure is missing: {procedureName}", ex);
        }
    }

    private void EnsureTempTableExists(SqlConnection con, string tempTableName)
    {
        using SqlCommand cmd = new SqlCommand("SELECT OBJECT_ID(@TempTableName)", con);
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("@TempTableName", $"tempdb..{tempTableName}");

        object? result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
        {
            throw new Exception($"{tempTableName} was not created on active connection");
        }
    }

    private void LogInventoryImportPreview(SqlConnection con)
    {
        try
        {
            using SqlCommand cmd = new SqlCommand("dbo.SP_GetInventoryImportPreviewFromTemp", con);
            cmd.CommandType = CommandType.StoredProcedure;

            using SqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Console.WriteLine($"Inventory import DB preview: existingMatched={reader["ExistingMatched"]}, newItemsToInsert={reader["NewItemsToInsert"]}, existingItemsToUpdate={reader["ExistingItemsToUpdate"]}");
                Debug.WriteLine($"Inventory import DB preview: existingMatched={reader["ExistingMatched"]}, newItemsToInsert={reader["NewItemsToInsert"]}, existingItemsToUpdate={reader["ExistingItemsToUpdate"]}");
            }

            if (reader.NextResult())
            {
                while (reader.Read())
                {
                    Console.WriteLine($"Inventory import changed row: ItemCode={reader["InventoryItemID"]}, {reader["ChangeSummary"]}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Inventory import preview failed, continuing to upsert: {ex.Message}");
            Debug.WriteLine($"Inventory import preview failed, continuing to upsert: {ex.Message}");
        }
    }

    private void ExecuteInventoryItemsUpsert(SqlConnection con)
    {
        Console.WriteLine("Calling SP_UpsertInventoryItemsFromTemp");
        Debug.WriteLine("Calling SP_UpsertInventoryItemsFromTemp");

        using SqlCommand cmd = new SqlCommand("dbo.SP_UpsertInventoryItemsFromTemp", con);
        cmd.CommandType = CommandType.StoredProcedure;

        using SqlDataReader reader = cmd.ExecuteReader();
        if (reader.Read() && ReaderHasColumn(reader, "UpdatedRows"))
        {
            Console.WriteLine($"SP_UpsertInventoryItemsFromTemp summary: UpdatedRows={reader["UpdatedRows"]}, InsertedRows={reader["InsertedRows"]}, DeactivatedRows={reader["DeactivatedRows"]}");
            Debug.WriteLine($"SP_UpsertInventoryItemsFromTemp summary: UpdatedRows={reader["UpdatedRows"]}, InsertedRows={reader["InsertedRows"]}, DeactivatedRows={reader["DeactivatedRows"]}");
        }

        while (reader.NextResult())
        {
            while (reader.Read())
            {
            }
        }

        Console.WriteLine("SP_UpsertInventoryItemsFromTemp completed");
        Debug.WriteLine("SP_UpsertInventoryItemsFromTemp completed");
    }

    private string ValidateTempItemCode(string itemCode, string tempTableName)
    {
        string normalized = (itemCode ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            throw new Exception($"ItemCode is empty for {tempTableName}");
        }

        if (normalized.Length > 100)
        {
            throw new Exception($"ItemCode '{normalized}' exceeds NVARCHAR(100) for {tempTableName}");
        }

        return normalized;
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
        DateTime? lastPODate = null,
        DateTime? lastPODateFrom = null,
        DateTime? lastPODateTo = null)
    {
        List<InventoryItem> items = new List<InventoryItem>();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;

        SqlConnection con = null;
        SqlCommand cmd;
        SqlDataReader reader;
        try
        {
            con = connect("myProjDB");
            bool supportsLastPODateRange = StoredProcedureHasParameter(con, "spInventoryItems_GetPaged", "@LastPODateFrom")
                && StoredProcedureHasParameter(con, "spInventoryItems_GetPaged", "@LastPODateTo");
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
                { "@LastPODate", lastPODate.HasValue ? lastPODate.Value.Date : DBNull.Value },
                { "@OnlyActive", true }
            };

            if (supportsLastPODateRange)
            {
                paramDic.Add("@LastPODateFrom", lastPODateFrom.HasValue ? lastPODateFrom.Value.Date : DBNull.Value);
                paramDic.Add("@LastPODateTo", lastPODateTo.HasValue ? lastPODateTo.Value.Date : DBNull.Value);
            }

            cmd = CreateCommandWithStoredProcedureGeneral("spInventoryItems_GetPaged", con, paramDic);
            cmd.CommandType = CommandType.StoredProcedure;
            reader = cmd.ExecuteReader();

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
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    private static bool StoredProcedureHasParameter(SqlConnection con, string procedureName, string parameterName)
    {
        using SqlCommand cmd = new SqlCommand(@"
SELECT 1
FROM sys.parameters p
INNER JOIN sys.objects o ON o.object_id = p.object_id
WHERE o.type = 'P'
  AND o.name = @ProcedureName
  AND p.name = @ParameterName;", con);
        cmd.Parameters.AddWithValue("@ProcedureName", procedureName);
        cmd.Parameters.AddWithValue("@ParameterName", parameterName);
        return cmd.ExecuteScalar() != null;
    }

    public List<object> GetBomPlaneOptions()
    {
        List<object> options = new List<object>();

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetPlaneOptions", con, null);
            cmd.CommandType = CommandType.StoredProcedure;

            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                options.Add(new
                {
                    PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                    PlaneTypeName = reader["PlaneTypeName"]?.ToString() ?? string.Empty
                });
            }

            return options;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public List<BomRow> GetBomRows(
        int page = 1,
        int pageSize = 100,
        bool treeMode = false,
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
        if (treeMode)
        {
            // Tree mode needs the full filtered sequence so parent/child branches are not split by pagination.
            // We reuse the same SP and request a very high page size while keeping RowOrder from the backend.
            page = 1;
            pageSize = 50000;
        }

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
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
            SqlDataReader reader = cmd.ExecuteReader();
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
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public (List<string> MeasureUnits, List<string> Warehouses, List<int> BomLevels, List<bool> HasChildOptions, List<string> BuyMethods, List<string> BodyPlanes) GetBomFilterOptions(int? planeTypeId = null)
    {
        List<string> measureUnits = new List<string>();
        List<string> warehouses = new List<string>();
        List<int> bomLevels = new List<int>();
        List<bool> hasChildOptions = new List<bool>();
        List<string> buyMethods = new List<string>();
        List<string> bodyPlanes = new List<string>();

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@PlaneTypeID", planeTypeId.HasValue ? planeTypeId.Value : DBNull.Value }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetFilterOptions", con, paramDic);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read()) measureUnits.Add(reader["Value"]?.ToString() ?? string.Empty);

            if (reader.NextResult())
            {
                while (reader.Read()) warehouses.Add(reader["Value"]?.ToString() ?? string.Empty);
            }

            if (reader.NextResult())
            {
                while (reader.Read()) bomLevels.Add(Convert.ToInt32(reader["BomLevel"]));
            }

            if (reader.NextResult())
            {
                while (reader.Read()) hasChildOptions.Add(Convert.ToBoolean(reader["HasChild"]));
            }

            if (reader.NextResult())
            {
                while (reader.Read()) buyMethods.Add(reader["Value"]?.ToString() ?? string.Empty);
            }

            if (reader.NextResult())
            {
                while (reader.Read()) bodyPlanes.Add(reader["Value"]?.ToString() ?? string.Empty);
            }

            return (measureUnits, warehouses, bomLevels, hasChildOptions, buyMethods, bodyPlanes);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public Dictionary<int, string> GetPlaneTypeNames(List<int> planeTypeIds)
    {
        Dictionary<int, string> planeTypeNames = new Dictionary<int, string>();

        if (planeTypeIds == null || planeTypeIds.Count == 0)
        {
            return planeTypeNames;
        }

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");

            string idsCsv = string.Join(",", planeTypeIds);

            SqlCommand planeTypesCmd = new SqlCommand("SP_GetPlaneTypeNamesByIds", con);
            planeTypesCmd.CommandType = CommandType.StoredProcedure;
            planeTypesCmd.Parameters.AddWithValue("@PlaneTypeIds", idsCsv);

            SqlDataReader reader = planeTypesCmd.ExecuteReader();
            while (reader.Read())
            {
                int planeTypeId = Convert.ToInt32(reader["PlaneTypeID"]);
                string planeTypeName = reader["PlaneTypeName"]?.ToString() ?? planeTypeId.ToString();
                planeTypeNames[planeTypeId] = planeTypeName;
            }

            return planeTypeNames;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public List<BomRow> GetBomRowsForPlanes(List<int> planeTypeIds, string bodyPlane)
    {
        List<BomRow> bomRows = new List<BomRow>();

        if (planeTypeIds == null || planeTypeIds.Count == 0)
        {
            return bomRows;
        }

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");

            string idsCsv = string.Join(",", planeTypeIds);

            SqlCommand bomCmd = new SqlCommand("dbo.SP_GetBomRowsForPlanes", con);
            bomCmd.CommandType = CommandType.StoredProcedure;
            bomCmd.Parameters.AddWithValue("@PlaneTypeIds", idsCsv);

            SqlDataReader reader = bomCmd.ExecuteReader();
            while (reader.Read())
            {
                string rowBodyPlane = reader["BodyPlane"] == DBNull.Value ? string.Empty : reader["BodyPlane"].ToString() ?? string.Empty;
                if (!string.Equals(rowBodyPlane.Trim(), bodyPlane?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bomRows.Add(new BomRow
                {
                    PlaneTypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                    RowOrder = Convert.ToInt32(reader["RowOrder"]),
                    InventoryItemID = reader["InventoryItemID"]?.ToString() ?? string.Empty,
                    ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                    Quantity = reader["Quantity"] == DBNull.Value ? null : Convert.ToDecimal(reader["Quantity"]),
                    MeasureUnit = reader["MeasureUnit"] == DBNull.Value ? null : reader["MeasureUnit"].ToString(),
                    Warehouse = reader["Warehouse"] == DBNull.Value ? null : reader["Warehouse"].ToString(),
                    BomLevel = reader["BomLevel"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BomLevel"]),
                    HasChild = reader["HasChild"] == DBNull.Value ? null : Convert.ToBoolean(reader["HasChild"]),
                    BuyMethod = reader["BuyMethod"] == DBNull.Value ? null : reader["BuyMethod"].ToString(),
                    BodyPlane = reader["BodyPlane"] == DBNull.Value ? null : reader["BodyPlane"].ToString()
                });
            }

            return bomRows;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public Dictionary<string, InventoryCheck.InventorySnapshot> GetInventorySnapshotsForItems(List<string> itemIds)
    {
        Dictionary<string, InventoryCheck.InventorySnapshot> snapshots = new Dictionary<string, InventoryCheck.InventorySnapshot>(StringComparer.OrdinalIgnoreCase);

        if (itemIds == null || itemIds.Count == 0)
        {
            return snapshots;
        }

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");

            string idsCsv = string.Join(",", itemIds);

            SqlCommand cmd = new SqlCommand("dbo.SP_GetInventorySnapshotsForItems", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ItemIds", idsCsv);

            SqlDataReader reader = cmd.ExecuteReader();
            bool hasOpenPurchaseRequestQty = ReaderHasColumn(reader, "OpenPurchaseRequestQty");
            bool hasOpenPurchaseOrderQty = ReaderHasColumn(reader, "OpenPurchaseOrderQty");
            bool hasApprovedOrderQty = ReaderHasColumn(reader, "ApprovedOrderQty");
            bool hasUnapprovedOrderQty = ReaderHasColumn(reader, "UnapprovedOrderQty");

            while (reader.Read())
            {
                string itemId = reader["InventoryItemID"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                snapshots[itemId] = new InventoryCheck.InventorySnapshot
                {
                    ItemName = string.Empty,
                    TotalStock = (reader["Whse01_QTY"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Whse01_QTY"]))
                               + (reader["Whse03_QTY"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Whse03_QTY"]))
                               + (reader["Whse90_QTY"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Whse90_QTY"])),
                    SupplierName = string.Empty,
                    Price = null,
                    OpenPurchaseRequestQty = hasOpenPurchaseRequestQty && reader["OpenPurchaseRequestQty"] != DBNull.Value ? Convert.ToInt32(reader["OpenPurchaseRequestQty"]) : 0,
                    OpenPurchaseOrderQty = hasOpenPurchaseOrderQty && reader["OpenPurchaseOrderQty"] != DBNull.Value ? Convert.ToInt32(reader["OpenPurchaseOrderQty"]) : 0,
                    ApprovedOrderQty = hasApprovedOrderQty && reader["ApprovedOrderQty"] != DBNull.Value ? Convert.ToInt32(reader["ApprovedOrderQty"]) : 0,
                    UnapprovedOrderQty = hasUnapprovedOrderQty && reader["UnapprovedOrderQty"] != DBNull.Value ? Convert.ToInt32(reader["UnapprovedOrderQty"]) : 0
                };
            }

            return snapshots;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    private static bool ReaderHasColumn(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    //db connection to get all the options for inventory filters (platforms, groups, buy methods, suppliers, body planes)
    public InventoryFilterOptions GetInventoryFilterOptions()
    {
        //options is a
        InventoryFilterOptions options = new InventoryFilterOptions();

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInventoryItems_GetFilterOptions", con, null);
            cmd.CommandType = CommandType.StoredProcedure;

            SqlDataReader reader = cmd.ExecuteReader();

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
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public InventoryImportResult ImportInventoryDataToDatabase(InventoryImportData importData)
    {
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

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            using (SqlCommand appLockCmd = new SqlCommand("sp_getapplock", con))
            {
                appLockCmd.CommandType = CommandType.StoredProcedure;
                appLockCmd.Parameters.AddWithValue("@Resource", "InventoryImportTempTables");
                appLockCmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                appLockCmd.Parameters.AddWithValue("@LockOwner", "Session");
                appLockCmd.Parameters.AddWithValue("@LockTimeout", 60000);
                SqlParameter returnValue = appLockCmd.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
                returnValue.Direction = ParameterDirection.ReturnValue;

                appLockCmd.ExecuteNonQuery();
                int lockResult = returnValue.Value == DBNull.Value ? -999 : Convert.ToInt32(returnValue.Value);
                if (lockResult < 0)
                {
                    throw new Exception($"Failed to acquire import lock. sp_getapplock result: {lockResult}");
                }
            }

            DataTable inventoryItemsImportTable = new DataTable();
            inventoryItemsImportTable.Columns.Add("InventoryItemID", typeof(string));
            inventoryItemsImportTable.Columns.Add("ItemName", typeof(string));
            inventoryItemsImportTable.Columns.Add("BuyMethod", typeof(string));
            inventoryItemsImportTable.Columns.Add("Price", typeof(double));
            inventoryItemsImportTable.Columns.Add("Whse01_QTY", typeof(int));
            inventoryItemsImportTable.Columns.Add("Whse03_QTY", typeof(int));
            inventoryItemsImportTable.Columns.Add("Whse90_QTY", typeof(int));
            inventoryItemsImportTable.Columns.Add("OpenPurchaseRequestQty", typeof(int));
            inventoryItemsImportTable.Columns.Add("OpenPurchaseOrderQty", typeof(int));
            inventoryItemsImportTable.Columns.Add("ApprovedOrderQty", typeof(int));
            inventoryItemsImportTable.Columns.Add("UnapprovedOrderQty", typeof(int));
            inventoryItemsImportTable.Columns.Add("ExcelRowNumber", typeof(int));

            foreach (InventoryBaseRow row in importData.InventoryBaseRows)
            {
                inventoryItemsImportTable.Rows.Add(
                    ValidateTempItemCode(row.InventoryItemID, "#InventoryItemsImport"),
                    string.IsNullOrWhiteSpace(row.ItemName) ? DBNull.Value : row.ItemName.Trim(),
                    string.IsNullOrWhiteSpace(row.BuyMethod) ? DBNull.Value : row.BuyMethod.Trim(),
                    row.Price.HasValue ? (object)row.Price.Value : DBNull.Value,
                    row.Whse01_QTY.HasValue ? (object)row.Whse01_QTY.Value : DBNull.Value,
                    row.Whse03_QTY.HasValue ? (object)row.Whse03_QTY.Value : DBNull.Value,
                    row.Whse90_QTY.HasValue ? (object)row.Whse90_QTY.Value : DBNull.Value,
                    row.OpenPurchaseRequestQty.HasValue ? (object)row.OpenPurchaseRequestQty.Value : DBNull.Value,
                    row.OpenPurchaseOrderQty.HasValue ? (object)row.OpenPurchaseOrderQty.Value : DBNull.Value,
                    row.ApprovedOrderQty.HasValue ? (object)row.ApprovedOrderQty.Value : DBNull.Value,
                    row.UnapprovedOrderQty.HasValue ? (object)row.UnapprovedOrderQty.Value : DBNull.Value,
                    row.ExcelRowNumber > 0 ? row.ExcelRowNumber : 0);
            }

            if (inventoryItemsImportTable.Rows.Count > 0)
            {
                ExecuteStoredProcedure(con, "dbo.SP_CreateInventoryItemsImportTempTable");
                EnsureTempTableExists(con, "##InventoryItemsImport");

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "##InventoryItemsImport";
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("InventoryItemID", "InventoryItemID");
                    bulkCopy.ColumnMappings.Add("ItemName", "ItemName");
                    bulkCopy.ColumnMappings.Add("BuyMethod", "BuyMethod");
                    bulkCopy.ColumnMappings.Add("Price", "Price");
                    bulkCopy.ColumnMappings.Add("Whse01_QTY", "Whse01_QTY");
                    bulkCopy.ColumnMappings.Add("Whse03_QTY", "Whse03_QTY");
                    bulkCopy.ColumnMappings.Add("Whse90_QTY", "Whse90_QTY");
                    bulkCopy.ColumnMappings.Add("OpenPurchaseRequestQty", "OpenPurchaseRequestQty");
                    bulkCopy.ColumnMappings.Add("OpenPurchaseOrderQty", "OpenPurchaseOrderQty");
                    bulkCopy.ColumnMappings.Add("ApprovedOrderQty", "ApprovedOrderQty");
                    bulkCopy.ColumnMappings.Add("UnapprovedOrderQty", "UnapprovedOrderQty");
                    bulkCopy.ColumnMappings.Add("ExcelRowNumber", "ExcelRowNumber");
                    bulkCopy.WriteToServer(inventoryItemsImportTable);
                }

                LogInventoryImportPreview(con);
                ExecuteInventoryItemsUpsert(con);
            }

            Dictionary<string, int> planeTypeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand selectPlaneTypesCmd = new SqlCommand("dbo.SP_GetPlaneTypesForImport", con))
            {
                selectPlaneTypesCmd.CommandType = CommandType.StoredProcedure;
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

            foreach (BomRow bomRow in importData.WbBomRows.Concat(importData.TbvBomRows))
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

            using (SqlCommand deleteBomCmd = new SqlCommand("dbo.SP_TruncateBom", con))
            {
                deleteBomCmd.CommandType = CommandType.StoredProcedure;
                deleteBomCmd.ExecuteNonQuery();
            }

            if (bomTable.Rows.Count > 0)
            {
                using SqlBulkCopy bomBulkCopy = new SqlBulkCopy(con);
                bomBulkCopy.DestinationTableName = "BOM";
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

            using (SqlCommand syncProductionItemsCmd = new SqlCommand("dbo.SP_SyncProductionItemsFromBom", con))
            {
                syncProductionItemsCmd.CommandType = CommandType.StoredProcedure;

                using SqlDataReader syncReader = syncProductionItemsCmd.ExecuteReader();
                if (syncReader.Read())
                {
                    insertedProductionItems = syncReader["InsertedProductionItems"] == DBNull.Value ? 0 : Convert.ToInt32(syncReader["InsertedProductionItems"]);
                    updatedProductionItems = syncReader["UpdatedProductionItems"] == DBNull.Value ? 0 : Convert.ToInt32(syncReader["UpdatedProductionItems"]);
                    deletedProductionItems = syncReader["DeletedProductionItems"] == DBNull.Value ? 0 : Convert.ToInt32(syncReader["DeletedProductionItems"]);
                    finalProductionItemsCount = syncReader["FinalProductionItemsCount"] == DBNull.Value ? 0 : Convert.ToInt32(syncReader["FinalProductionItemsCount"]);
                }
            }

            using (SqlCommand supplierCmd = new SqlCommand("dbo.SP_InsertSupplierIfMissing", con))
            {
                supplierCmd.CommandType = CommandType.StoredProcedure;
                supplierCmd.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 100);

                foreach (string supplierName in importData.UniqueSuppliers)
                {
                    supplierCmd.Parameters["@SupplierName"].Value = supplierName;
                    int affectedRows = Convert.ToInt32(supplierCmd.ExecuteScalar());
                    if (affectedRows > 0)
                    {
                        insertedSuppliers++;
                    }
                }
            }

            using (SqlCommand selectSuppliersCmd = new SqlCommand("dbo.SP_GetSuppliersForImport", con))
            {
                selectSuppliersCmd.CommandType = CommandType.StoredProcedure;
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

            DataTable supplierUpdatesTable = new DataTable();
            supplierUpdatesTable.Columns.Add("ItemCode", typeof(string));
            supplierUpdatesTable.Columns.Add("SupplierID", typeof(int));

            foreach (var mapping in importData.ItemToSupplierMap)
            {
                string itemCode = mapping.Key;
                string supplierName = mapping.Value?.Trim() ?? string.Empty;

                if (!supplierNameToId.TryGetValue(supplierName, out int supplierId))
                {
                    continue;
                }

                supplierUpdatesTable.Rows.Add(ValidateTempItemCode(itemCode, "#SupplierUpdates"), supplierId);
            }

            if (supplierUpdatesTable.Rows.Count > 0)
            {
                ExecuteStoredProcedure(con, "dbo.SP_CreateSupplierUpdatesTempTable");
                EnsureTempTableExists(con, "##SupplierUpdates");

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "##SupplierUpdates";
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("SupplierID", "SupplierID");
                    bulkCopy.WriteToServer(supplierUpdatesTable);
                }

                using SqlCommand updateSupplierCmd = new SqlCommand("dbo.SP_UpdateInventorySuppliersFromTemp", con);
                updateSupplierCmd.CommandType = CommandType.StoredProcedure;
                updatedSupplierRows = updateSupplierCmd.ExecuteNonQuery();
            }

            DataTable lastPoDateUpdatesTable = new DataTable();
            lastPoDateUpdatesTable.Columns.Add("ItemCode", typeof(string));
            lastPoDateUpdatesTable.Columns.Add("LastPODate", typeof(DateTime));

            foreach (var mapping in importData.ItemToLastPODateMap)
            {
                lastPoDateUpdatesTable.Rows.Add(ValidateTempItemCode(mapping.Key, "#LastPODateUpdates"), mapping.Value.Date);
            }

            if (lastPoDateUpdatesTable.Rows.Count > 0)
            {
                ExecuteStoredProcedure(con, "dbo.SP_CreateLastPoDateUpdatesTempTable");
                EnsureTempTableExists(con, "##LastPODateUpdates");

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "##LastPODateUpdates";
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("LastPODate", "LastPODate");
                    bulkCopy.WriteToServer(lastPoDateUpdatesTable);
                }

                using SqlCommand updateLastPoDateCmd = new SqlCommand("dbo.SP_UpdateInventoryLastPoDateFromTemp", con);
                updateLastPoDateCmd.CommandType = CommandType.StoredProcedure;
                updateLastPoDateCmd.ExecuteNonQuery();
            }

            using SqlCommand cmd = new SqlCommand("dbo.SP_InsertGroupIfMissing", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@GroupName", SqlDbType.NVarChar, 255);

            foreach (string groupName in importData.UniqueGroupNames)
            {
                cmd.Parameters["@GroupName"].Value = groupName;
                int affectedRows = Convert.ToInt32(cmd.ExecuteScalar());
                if (affectedRows > 0)
                {
                    insertedGroups++;
                }
            }

            using (SqlCommand selectGroupsCmd = new SqlCommand("dbo.SP_GetGroupsForImport", con))
            {
                selectGroupsCmd.CommandType = CommandType.StoredProcedure;
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

            DataTable updatesTable = new DataTable();
            updatesTable.Columns.Add("ItemCode", typeof(string));
            updatesTable.Columns.Add("ItemGrpID", typeof(int));

            foreach (var mapping in importData.ItemToGroupMap)
            {
                string itemCode = mapping.Key;
                string groupName = mapping.Value?.Trim() ?? string.Empty;

                if (!groupNameToId.TryGetValue(groupName, out int itemGrpId))
                {
                    continue;
                }

                updatesTable.Rows.Add(ValidateTempItemCode(itemCode, "#ItemGroupUpdates"), itemGrpId);
            }

            if (updatesTable.Rows.Count > 0)
            {
                ExecuteStoredProcedure(con, "dbo.SP_CreateItemGroupUpdatesTempTable");
                EnsureTempTableExists(con, "##ItemGroupUpdates");

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "##ItemGroupUpdates";
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("ItemGrpID", "ItemGrpID");
                    bulkCopy.WriteToServer(updatesTable);
                }

                using SqlCommand updateCmd = new SqlCommand("dbo.SP_UpdateInventoryGroupsFromTemp", con);
                updateCmd.CommandType = CommandType.StoredProcedure;
                updatedRows = updateCmd.ExecuteNonQuery();
            }

            DataTable buyMethodUpdatesTable = new DataTable();
            buyMethodUpdatesTable.Columns.Add("ItemCode", typeof(string));
            buyMethodUpdatesTable.Columns.Add("BuyMethod", typeof(string));

            foreach (var mapping in importData.ItemToBuyMethod)
            {
                buyMethodUpdatesTable.Rows.Add(ValidateTempItemCode(mapping.Key, "#BuyMethodUpdates"), mapping.Value);
            }

            if (buyMethodUpdatesTable.Rows.Count > 0)
            {
                ExecuteStoredProcedure(con, "dbo.SP_CreateBuyMethodUpdatesTempTable");
                EnsureTempTableExists(con, "##BuyMethodUpdates");

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "##BuyMethodUpdates";
                    bulkCopy.BatchSize = 2000;
                    bulkCopy.ColumnMappings.Add("ItemCode", "ItemCode");
                    bulkCopy.ColumnMappings.Add("BuyMethod", "BuyMethod");
                    bulkCopy.WriteToServer(buyMethodUpdatesTable);
                }

                using SqlCommand updateBuyMethodCmd = new SqlCommand("dbo.SP_UpdateInventoryBuyMethodFromTemp", con);
                updateBuyMethodCmd.CommandType = CommandType.StoredProcedure;
                updateBuyMethodCmd.ExecuteNonQuery();
            }
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }

        return new InventoryImportResult
        {
            ImportedRows = importData.InventoryBaseRows.Count,
            DeletedProductionItems = deletedProductionItems,
            InsertedProductionItems = insertedProductionItems,
            UpdatedProductionItems = updatedProductionItems,
            FinalProductionItemsCount = finalProductionItemsCount
        };
    }

    public void SetLastInventoryImportTimestamp(DateTime timestamp)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            using SqlCommand cmd = new SqlCommand("dbo.SP_SetLastInventoryImportTimestamp", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Timestamp", timestamp);
            cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public DateTime? GetLastInventoryImportTimestamp()
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            using SqlCommand cmd = new SqlCommand("dbo.SP_GetLastInventoryImportTimestamp", con);
            cmd.CommandType = CommandType.StoredProcedure;

            object? result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            if (result is DateTime dt)
            {
                return dt;
            }

            string raw = result.ToString() ?? string.Empty;
            if (DateTime.TryParse(raw, out DateTime parsed))
            {
                return parsed;
            }

            return null;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    //ייצור

    //שליפת כל הנתונים של פרטי הייצור עבור עמודי לוח משימות וניהול לוז
    private SqlCommand CreateProductionBoardDataCommand(SqlConnection con)
    {
        string? startColumn = GetExistingColumnName(con, "ProductionItemStage", "StartTimeStamp", "StartTimestamp", "StartTime");
        string? finishColumn = GetExistingColumnName(con, "ProductionItemStage", "FinishTimeStamp", "FinishTimestamp", "FinishTime", "EndTime");
        string startSelect = startColumn == null ? "CAST(NULL AS datetime) AS StartTimeStamp" : $"pis.{startColumn} AS StartTimeStamp";
        string finishSelect = finishColumn == null ? "CAST(NULL AS datetime) AS FinishTimeStamp" : $"pis.{finishColumn} AS FinishTimeStamp";

        SqlCommand cmd = new SqlCommand(@"
SELECT
    iip.SerialNumber,
    iip.WorkOrderID AS WorkOrderNumber,
    iip.WorkOrderID,
    iip.ProductionItemID AS InventoryItemID,
    iip.ProductionItemID,
    pi.ItemName,
    pt.PlaneTypeName,
    COALESCE(pl.PlaneTypeID, iip.PlaneTypeID) AS PlaneTypeID,
    COALESCE(p.ProjectName, iip.ProjectName) AS ProjectName,
    CAST(pl.PlaneID AS nvarchar(50)) AS PlaneNumber,
    CAST(pl.PlaneID AS nvarchar(50)) AS TailNumber,
    iip.PlannedQty,
    ISNULL(iip.Comments, N'') AS Comments,
    ISNULL(iip.PriorityLevel, 3) AS ItemPriorityLevel,
    ISNULL(p.PriorityLevel, 3) AS ProjectPriorityLevel,
    iip.DueDate AS ItemDueDate,
    p.DueDate AS ProjectDueDate,
    p.DueDate AS DueDate,
    pis.ProductionStageID,
    ps.ProductionStageName,
    ps.StageOrder,
    ISNULL(ps.TargetDuration, CAST('01:00:00' AS time)) AS TargetDuration,
    ISNULL(pis.ProductionStatusID, 1) AS ProductionStatusID,
    ISNULL(pst.ProductionStatusName, N'לא ידוע') AS StatusName,
    ISNULL(pis.Comment, N'') AS Comment,
    " + startSelect + @",
    " + finishSelect + @",
    pis.ManualPriority,
    cur.CurrentStationName
FROM dbo.ItemsInProduction iip
INNER JOIN dbo.ProductionItems pi
    ON pi.ProductionItemID = iip.ProductionItemID
LEFT JOIN dbo.Planes pl
    ON pl.PlaneID = iip.PlaneID
LEFT JOIN dbo.PlaneTypes pt
    ON pt.PlaneTypeID = COALESCE(pl.PlaneTypeID, iip.PlaneTypeID)
LEFT JOIN dbo.Projects p
    ON p.ProjectID = pl.ProjectID
INNER JOIN dbo.ProductionItemStage pis
    ON pis.SerialNumber = iip.SerialNumber
   AND pis.ProductionItemID = iip.ProductionItemID
INNER JOIN dbo.ProductionStages ps
    ON ps.ProductionStageID = pis.ProductionStageID
LEFT JOIN dbo.ProductionStatuses pst
    ON pst.ProductionStatusID = pis.ProductionStatusID
OUTER APPLY
(
    SELECT TOP (1)
        ps2.ProductionStageName AS CurrentStationName
    FROM dbo.ProductionItemStage pis2
    INNER JOIN dbo.ProductionStages ps2
        ON ps2.ProductionStageID = pis2.ProductionStageID
    WHERE pis2.SerialNumber = iip.SerialNumber
      AND pis2.ProductionItemID = iip.ProductionItemID
    ORDER BY
        CASE WHEN ISNULL(pis2.ProductionStatusID, 1) <> 4 THEN 0 ELSE 1 END,
        ps2.StageOrder
) cur
ORDER BY
    iip.SerialNumber,
    ps.StageOrder;", con);
        cmd.CommandType = CommandType.Text;
        return cmd;
    }

    public List<ItemInProduction> GetTasksBoard()
    {
        SqlConnection con = null;
        Dictionary<string, ItemInProduction> itemsMap = new Dictionary<string, ItemInProduction>();

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            con = connect("myProjDB");
            EnsureItemsInProductionPlaneTypeColumn(con);
            SqlCommand cmd = CreateProductionBoardDataCommand(con);
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int sn = Convert.ToInt32(reader["SerialNumber"]);
                string productionItemId = ReadNullableString(reader,
                    "InventoryItemID",
                    "ProductionItemID") ?? string.Empty;

                string rowKey = $"{sn}|{productionItemId}";
                if (!itemsMap.ContainsKey(rowKey))
                {
                    int itemPriorityLevel = ReadNullableInt(reader,
                        "ItemPriorityLevel",
                        "IIPPriorityLevel",
                        "ItemsInProductionPriorityLevel",
                        "ProductionItemPriorityLevel",
                        "ItemPriority") ?? 3;

                    int projectPriorityLevel = ReadNullableInt(reader,
                        "ProjectPriorityLevel",
                        "ProjectsPriorityLevel",
                        "ProjectPriority",
                        "ProjectPriorityID") ?? 3;

                    DateTime? itemDueDate = ReadNullableDate(reader,
                        "ItemDueDate",
                        "ItemsInProductionDueDate",
                        "IIPDueDate",
                        "ItemProductionDueDate");

                    DateTime? projectDueDate = ReadNullableDate(reader,
                        "ProjectDueDate",
                        "ProjectsDueDate",
                        "ProjectDue")
                        ?? ReadNullableDate(reader, "DueDate");

                    string itemName = ReadNullableString(reader,
                        "ItemName",
                        "ProductionItemDescription") ?? string.Empty;

                    string projectName = ReadNullableString(reader,
                        "ProjectName") ?? string.Empty;

                    string planeNumber = ReadNullableString(reader,
                        "PlaneNumber",
                        "TailNumber",
                        "PlaneID") ?? string.Empty;

                    string planeTypeName = ReadNullableString(reader,
                        "PlaneTypeName") ?? string.Empty;

                    int planeTypeId = ReadNullableInt(reader,
                        "PlaneTypeID",
                        "TypeID") ?? 0;

                    int workOrderId = ReadNullableInt(reader,
                        "WorkOrderNumber",
                        "WorkOrderID") ?? 0;

                    int plannedQty = ReadNullableInt(reader,
                        "PlannedQty") ?? 0;

                    itemsMap[rowKey] = new ItemInProduction
                    {
                        SerialNumber = sn,
                        PriorityLevel = itemPriorityLevel,
                        ItemDueDate = itemDueDate,
                        WorkOrderID = workOrderId,
                        ProjectName = projectName,
                        TailNumber = planeNumber,
                        Comments = ReadNullableString(reader, "Comments") ?? string.Empty,
                        ProductionItem = new ProductionItem
                        {
                            ProductionItemID = productionItemId,
                            ItemName = itemName
                        },
                        PlannedQty = plannedQty,
                        PlaneID = new Plane
                        {
                            Type = new PlaneType
                            {
                                PlaneTypeID = planeTypeId,
                                PlaneTypeName = planeTypeName
                            },
                            Project = new Project
                            {
                                ProjectName = projectName,
                                DueDate = projectDueDate,
                                PriorityLevel = projectPriorityLevel
                            }
                        },
                        Stages = new List<ProductionItemStage>()
                    };
                }

                // 2. חישוב זמן מטרה (TargetDuration) - הגנה ברמת הקוד
                TimeSpan duration;
                if (reader["TargetDuration"] == DBNull.Value)
                {
                    // הגנה: אם אין נתון, נשים שעה אחת כברירת מחדל
                    duration = TimeSpan.FromHours(1);
                }
                else
                {
                    // הדרך הנכונה לשלוף TimeSpan ישירות מה-reader בלי להשתמש ב-Convert
                    duration = (TimeSpan)reader["TargetDuration"];
                }

                // 3. הוספת השלב הספציפי לפריט (כולל התעדוף הידני שעבר לפה)
                string currentProductionItemId = ReadNullableString(reader, "InventoryItemID", "ProductionItemID") ?? string.Empty;
                string currentRowKey = $"{sn}|{currentProductionItemId}";
                itemsMap[currentRowKey].Stages.Add(new ProductionItemStage
                {
                    Stage = new ProductionStage
                    {
                        ProductionStageID = Convert.ToInt32(reader["ProductionStageID"]),
                        ProductionStageName = reader["ProductionStageName"].ToString(),
                        StageOrder = ReadNullableInt(reader, "StageOrder") ?? 0,
                        TargetDuration = duration 
                    },
                    Status = new ProductionStatus
                    {
                        ProductionStatusID = Convert.ToInt32(reader["ProductionStatusID"]),
                        ProductionStatusName = reader["StatusName"].ToString()
                    },
                    Comment = reader["Comment"].ToString(),
                    StartTimeStamp = ReadNullableDate(reader, "StartTimeStamp", "StartTimestamp", "StartTime"),
                    FinishTimeStamp = ReadNullableDate(reader, "FinishTimeStamp", "FinishTimestamp", "FinishTime", "EndTime"),
                    ManualPriority = reader["ManualPriority"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ManualPriority"])
                });
            }
            reader.Close();
            sw.Stop();
            Debug.WriteLine($"[TasksBoard] GetTasksBoard loaded {itemsMap.Count} rows in {sw.ElapsedMilliseconds}ms");
            return itemsMap.Values.ToList();
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }
    // שליפת כל הפרוייקטים והנתונים שלהם  
    public List<Project> GetProjects()
    {
        //הכנסת ערך דיפולטי לפרוייט אם לא הוכנס ערך, ניתן גם להעביר לדאטא בייס
        const int defaultProjectPriority = 2;

        SqlConnection con = null;
        List<Project> list = new List<Project>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetAllProjects", con, null);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Project
                {
                    ProjectID = (int)reader["ProjectID"],
                    ProjectName = reader["ProjectName"].ToString(),
                    DueDate = reader["DueDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["DueDate"]),
                    PriorityLevel = reader["PriorityLevel"] == DBNull.Value ? defaultProjectPriority : Convert.ToInt32(reader["PriorityLevel"])
                });
            }
            return list;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    //הכנסת פרוייקט חדש - לא בשימוש כרגע תשתית להמשך
    public int InsertProject(Project p)
    {
        SqlConnection con = null;
        Dictionary<string, object> d = new Dictionary<string, object> {
        {"@ProjectName", p.ProjectName},
        {"@DueDate", p.DueDate.HasValue ? p.DueDate.Value : DBNull.Value},
        {"@PriorityLevel", p.PriorityLevel}
    };
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInsertProject", con, d);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    private void EnsureItemsInProductionPlaneTypeColumn(SqlConnection con)
    {
        using SqlCommand cmd = new SqlCommand(@"
IF COL_LENGTH('dbo.ItemsInProduction', 'PlaneTypeID') IS NULL
BEGIN
    ALTER TABLE dbo.ItemsInProduction ADD PlaneTypeID INT NULL;
END
IF COL_LENGTH('dbo.ItemsInProduction', 'ProjectName') IS NULL
BEGIN
    ALTER TABLE dbo.ItemsInProduction ADD ProjectName NVARCHAR(255) NULL;
END", con);
        cmd.CommandType = CommandType.Text;
        cmd.ExecuteNonQuery();
    }

    public Project CreateProject(string projectName, DateTime? dueDate, int priorityLevel)
    {
        string normalizedName = projectName.Trim();
        Project? existing = GetProjects()
            .FirstOrDefault(p => string.Equals(p.ProjectName?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            throw new Exception("Project already exists.");
        }

        InsertProject(new Project
        {
            ProjectName = normalizedName,
            DueDate = dueDate,
            PriorityLevel = priorityLevel <= 0 ? 2 : priorityLevel
        });

        Project? created = GetProjects()
            .FirstOrDefault(p => string.Equals(p.ProjectName?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (created == null)
        {
            throw new Exception("Project was not created.");
        }

        return created;
    }

    //עריכת פרוייקט - לא בשימוש כרגע - תשתית להמשך
    public int UpdateProject(Project p)
    {
        SqlConnection con = null;
        Dictionary<string, object> d = new Dictionary<string, object> {
        {"@ProjectID", p.ProjectID}, {"@ProjectName", p.ProjectName},
        {"@DueDate", p.DueDate}, {"@PriorityLevel", p.PriorityLevel}
    };
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUpdateProject", con, d);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    //מחיקת פרוייקט - לא בשימוש כרגע, תשתית להמשך
    public int DeleteProject(int id)
    {
        SqlConnection con = null;
        Dictionary<string, object> d = new Dictionary<string, object> { { "@ProjectID", id } };
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spDeleteProject", con, d);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
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
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    //שליפת כל תחנות העבודה
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //שליפת רשימת כל הסטטוסים שניתן לשים לכל פריט בתחנת ייצור
    public List<ProductionStatus> GetProductionStatuses()
    {
        SqlConnection con = null;
        List<ProductionStatus> statusesList = new List<ProductionStatus>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetProductionStatuses", con, null);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ProductionStatus status = new ProductionStatus();

                status.ProductionStatusID = Convert.ToInt32(reader["ProductionStatusID"]);
                status.ProductionStatusName = reader["ProductionStatusName"].ToString();
                statusesList.Add(status);
            }
            return statusesList;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    // שליפת הנתונים עבור טופס סטטוס פרוייקטים
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
                            Project = projectsMap[pID],
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
                                ItemDueDate = ReadNullableDate(reader, "ItemDueDate", "ItemsInProductionDueDate", "IIPDueDate"),
                                PriorityLevel = ReadNullableInt(reader, "ItemPriorityLevel", "ItemsInProductionPriorityLevel", "PriorityLevel") ?? 0,
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
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //שליפת כל הפריטים מעץ המוצר שמיוצרים בחברה
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    //שליפת פק"ע לצורך הוספת פריט - בחירה בין קיימים
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    //שליפת כל הסוגים לצורך שליפה למשתמש בהוספת פריט, פרוייקט או מטוס
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
                    id = Convert.ToInt32(reader["PriorityID"]),
                    name = reader["PriorityName"]?.ToString() ?? string.Empty
                });
            }
            return list;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //שליפת המטוסים הקיימים בDB
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
                    planeID = reader["PlaneID"]?.ToString() ?? string.Empty,
                    typeID = Convert.ToInt32(reader["PlaneTypeID"]),
                    projectID = Convert.ToInt32(reader["ProjectID"])
                });
            }
            return list;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //הוספת פריט חדש לייצור
    public int InsertItemInProduction(InsertItemInProductionRequest item)
    {
        const int defaultProjectPriority = 2;

        SqlConnection con = null;
        //שימוש בטרנזקציה כדי שלא ישמרו חלק מהנתונים במידה ולא כל הנתונים תקינים או הוכנסו כראוי
        SqlTransaction trans = null;

        try
        {
            con = connect("myProjDB");
            if (con.State != System.Data.ConnectionState.Open) con.Open();
            EnsureItemsInProductionPlaneTypeColumn(con);
            trans = con.BeginTransaction();

            string projectName = item.ProjectName;
            string planeID = item.PlaneID;
            string productionItemID = item.ProductionItemID;
            string workOrderID = item.WorkOrderID;
            int serialNumber = item.SerialNumber ?? 0;
            int planeTypeID = item.PlaneTypeID ?? 0;
            DateTime projectDueDate = (item.ProjectDueDate ?? DateTime.Today).Date;
            DateTime itemDueDate = (item.DueDate ?? DateTime.Today).Date;
            int projectPriorityLevel = item.ProjectPriorityLevel ?? defaultProjectPriority;

            //קריאה לפונקציה שמוסיפה מטוס ופרוייקט אם המשתמש מזין ערך חדש
            HandleProjectAndPlane(con, trans, projectName, planeID, planeTypeID, projectDueDate, projectPriorityLevel);
            //קריאה לפונקציה שמוסיפה פק"ע חדש במידה והמשתמש הזין ערך חדש
            HandleWorkOrder(con, trans, workOrderID);
            //יצירת פריט לייצור חדש בטבלה
            using (SqlCommand mainCmd = new SqlCommand("dbo.SP_InsertItemInProduction", con, trans))
            {
                mainCmd.CommandType = CommandType.StoredProcedure;
                mainCmd.Parameters.AddWithValue("@itemID", productionItemID);
                mainCmd.Parameters.AddWithValue("@serial", serialNumber);
                mainCmd.Parameters.AddWithValue("@planeID", (object)planeID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@priority", item.PriorityID ?? 1);
                mainCmd.Parameters.AddWithValue("@workOrder", (object)workOrderID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@qty", item.Quantity ?? 1);
                mainCmd.Parameters.AddWithValue("@dueDate", itemDueDate);
                mainCmd.Parameters.AddWithValue("@comments", (object)item.Comments ?? DBNull.Value);
                mainCmd.ExecuteNonQuery();
            }

            UpdateOptionalItemsInProductionColumn(con, trans, serialNumber, productionItemID, "PlaneTypeID", planeTypeID);
            UpdateOptionalItemsInProductionColumn(con, trans, serialNumber, productionItemID, "ProjectName", projectName);

            //קריאה לפונקציה שמוסיפה תחנות עבודה לפריט החדש שהוכנס
            InsertStagesForProduct(con, trans, serialNumber, productionItemID);

            trans.Commit();
            return 1;
        }
        catch (Exception)
        {
            if (trans != null) trans.Rollback();
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //הוספת פקודת עבודה חדשה במידה והמשתמש הוסיף בתהליך הוספת פריט
    private void HandleWorkOrder(SqlConnection con, SqlTransaction trans, string workOrderID)
    {
        if (string.IsNullOrEmpty(workOrderID)) return;

        using (SqlCommand cmd = new SqlCommand("dbo.SP_HandleWorkOrder", con, trans))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@woID", workOrderID);
            cmd.ExecuteNonQuery();
        }
    }


    //הוספת פרוייקט ומטוס חדשים במידה והמשתמש הוסיף בתהליך של הוספת פריט
    private void HandleProjectAndPlane(SqlConnection con, SqlTransaction trans, string projectName, string planeID, int planeTypeID, DateTime dueDate, int projectPriorityLevel)
    {
        using (SqlCommand cmd = new SqlCommand("dbo.SP_HandleProjectAndPlane", con, trans))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@pName", (object)projectName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@planeID", (object)planeID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@typeID", planeTypeID);
            cmd.Parameters.AddWithValue("@dueDate", dueDate);
            cmd.Parameters.AddWithValue("@priorityLevel", projectPriorityLevel);
            cmd.ExecuteNonQuery();
        }
    }

    public object CreatePlaneForProject(int projectID, string planeID, int planeTypeID)
    {
        string normalizedPlaneID = planeID.Trim();
        Project? project = GetProjects().FirstOrDefault(p => p.ProjectID == projectID);
        if (project == null)
        {
            throw new Exception("Project was not found.");
        }

        object? existing = GetPlanes().FirstOrDefault(p =>
        {
            string existingPlaneID = Convert.ToString(p.GetType().GetProperty("planeID")?.GetValue(p)) ?? string.Empty;
            int existingProjectID = Convert.ToInt32(p.GetType().GetProperty("projectID")?.GetValue(p) ?? 0);
            return existingProjectID == projectID && string.Equals(existingPlaneID.Trim(), normalizedPlaneID, StringComparison.OrdinalIgnoreCase);
        });

        if (existing != null)
        {
            throw new Exception("Plane already exists for this project.");
        }

        SqlConnection con = null;
        SqlTransaction trans = null;
        try
        {
            con = connect("myProjDB");
            trans = con.BeginTransaction();
            HandleProjectAndPlane(con, trans, project.ProjectName, normalizedPlaneID, planeTypeID, project.DueDate ?? DateTime.Today, project.PriorityLevel <= 0 ? 2 : project.PriorityLevel);
            trans.Commit();
        }
        catch
        {
            trans?.Rollback();
            throw;
        }
        finally { if (con != null) con.Close(); }

        object? created = GetPlanes().FirstOrDefault(p =>
        {
            string existingPlaneID = Convert.ToString(p.GetType().GetProperty("planeID")?.GetValue(p)) ?? string.Empty;
            int existingProjectID = Convert.ToInt32(p.GetType().GetProperty("projectID")?.GetValue(p) ?? 0);
            return existingProjectID == projectID && string.Equals(existingPlaneID.Trim(), normalizedPlaneID, StringComparison.OrdinalIgnoreCase);
        });

        if (created == null)
        {
            throw new Exception("Plane was not created.");
        }

        return created;
    }

    //יצירת תחנות עבור פריט ייצור חדש שנוצר במערכת
    private void InsertStagesForProduct(SqlConnection con, SqlTransaction trans, int serialNumber, string productionItemID)
    {
        using (SqlCommand cmd = new SqlCommand("dbo.SP_InsertStagesForProduct", con, trans))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@serial", serialNumber);
            cmd.Parameters.AddWithValue("@itemID", productionItemID);
            cmd.ExecuteNonQuery();
        }
    }

    //עדכון סטטוס לתחנה ספציפית
    //הפונקציה מקבלת את הפריט והתחנה שהוא נמצא, את הסטטוס החדש והנתונים הנוספים במידה והוסיף
    public int UpdateStageStatus(int serial, string itemID, int stageID, int newStatusID, string comment, DateTime? userTime, bool resetFuture, DateTime? startTime = null, DateTime? finishTime = null)
    {
        SqlConnection con = null;
        try
        {
            if (serial <= 0 || string.IsNullOrWhiteSpace(itemID) || stageID <= 0 || newStatusID <= 0)
            {
                return 0;
            }

            con = connect("myProjDB");

            using SqlCommand cmd = new SqlCommand(@"
UPDATE ProductionItemStage
SET ProductionStatusID = @NewStatusID,
    Comment = @Comment,
    StartTimeStamp = CASE WHEN @NewStatusID IN (2,3,4,5) THEN @StartTime ELSE NULL END,
    FinishTimeStamp = CASE WHEN @NewStatusID = 4 THEN @FinishTime ELSE NULL END
WHERE SerialNumber = @Serial AND ProductionItemID = @ItemID AND ProductionStageID = @StageID;

IF @ResetFuture = 1
BEGIN
    UPDATE futureStage
    SET ProductionStatusID = 1,
        Comment = NULL,
        StartTimeStamp = NULL,
        FinishTimeStamp = NULL
    FROM ProductionItemStage futureStage
    INNER JOIN ProductionStages currentStage ON currentStage.ProductionStageID = @StageID
    INNER JOIN ProductionStages nextStage ON nextStage.ProductionStageID = futureStage.ProductionStageID
    WHERE futureStage.SerialNumber = @Serial
      AND futureStage.ProductionItemID = @ItemID
      AND nextStage.StageOrder > currentStage.StageOrder;
END", con);

            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@Serial", serial);
            cmd.Parameters.AddWithValue("@ItemID", itemID);
            cmd.Parameters.AddWithValue("@StageID", stageID);
            cmd.Parameters.AddWithValue("@NewStatusID", newStatusID);
            cmd.Parameters.AddWithValue("@Comment", (object?)comment ?? DBNull.Value);
            DateTime? effectiveStartTime = startTime ?? userTime;
            DateTime? effectiveFinishTime = finishTime ?? (newStatusID == 4 ? userTime : null);
            cmd.Parameters.AddWithValue("@StartTime", effectiveStartTime.HasValue ? effectiveStartTime.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@FinishTime", effectiveFinishTime.HasValue ? effectiveFinishTime.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ResetFuture", resetFuture);

            return cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    //עדכון סידור ידני של המשתמש בDB
    public int UpdateManualPriority(int serial, string itemID, int stageID, int priority)
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
            { "@Priority", priority }
        };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spProductionItemStage_UpdateManualPriority", con, paramDic);

            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw new Exception("Error in DAL UpdateManualPriority: " + ex.Message);
        }
        finally
        {
            if (con != null) con.Close();
        }
    }

    public int UpdateItemInProductionRow(UpdateItemInProductionRowRequest data)
    {
        SqlConnection con = null;
        SqlTransaction trans = null;
        try
        {
            con = connect("myProjDB");
            trans = con.BeginTransaction();
            HandleWorkOrder(con, trans, data.WorkOrderID.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(data.TailNumber) && data.PlaneTypeID > 0)
            {
                HandleProjectAndPlane(con, trans, data.ProjectName, data.TailNumber, data.PlaneTypeID, DateTime.Today, 2);
            }

            using (SqlCommand mainCmd = new SqlCommand(@"
UPDATE ItemsInProduction
SET SerialNumber = @SerialNumber,
    ProductionItemID = @ProductionItemID,
    WorkOrderID = @WorkOrderID,
    PlaneID = @TailNumber,
    PlannedQty = @PlannedQty,
    DueDate = @DueDate,
    PriorityLevel = @PriorityLevel,
    Comments = @Comments
WHERE SerialNumber = @OriginalSerialNumber AND ProductionItemID = @OriginalProductionItemID", con, trans))
            {
                mainCmd.CommandType = CommandType.Text;
                mainCmd.Parameters.AddWithValue("@OriginalSerialNumber", data.OriginalSerialNumber);
                mainCmd.Parameters.AddWithValue("@OriginalProductionItemID", data.OriginalProductionItemID);
                mainCmd.Parameters.AddWithValue("@SerialNumber", data.SerialNumber);
                mainCmd.Parameters.AddWithValue("@ProductionItemID", data.ProductionItemID);
                mainCmd.Parameters.AddWithValue("@WorkOrderID", data.WorkOrderID);
                mainCmd.Parameters.AddWithValue("@TailNumber", string.IsNullOrWhiteSpace(data.TailNumber) ? DBNull.Value : data.TailNumber);
                mainCmd.Parameters.AddWithValue("@PlannedQty", data.PlannedQty);
                mainCmd.Parameters.AddWithValue("@DueDate", data.DueDate.HasValue ? data.DueDate.Value.Date : DBNull.Value);
                mainCmd.Parameters.AddWithValue("@PriorityLevel", data.PriorityLevel.HasValue && data.PriorityLevel.Value > 0 ? data.PriorityLevel.Value : DBNull.Value);
                mainCmd.Parameters.AddWithValue("@Comments", string.IsNullOrWhiteSpace(data.Comments) ? DBNull.Value : data.Comments);
                int affected = mainCmd.ExecuteNonQuery();
                if (affected == 0)
                {
                    trans.Rollback();
                    return 0;
                }
            }

            UpdateOptionalItemsInProductionColumn(con, trans, data.SerialNumber, data.ProductionItemID, "ItemName", data.ItemName);
            UpdateOptionalItemsInProductionColumn(con, trans, data.SerialNumber, data.ProductionItemID, "ProjectName", data.ProjectName);
            UpdateOptionalItemsInProductionColumn(con, trans, data.SerialNumber, data.ProductionItemID, "PlaneTypeID", data.PlaneTypeID);

            if (data.SerialNumber != data.OriginalSerialNumber || !string.Equals(data.ProductionItemID, data.OriginalProductionItemID, StringComparison.OrdinalIgnoreCase))
            {
                using SqlCommand stagesCmd = new SqlCommand(@"
UPDATE ProductionItemStage
SET SerialNumber = @SerialNumber,
    ProductionItemID = @ProductionItemID
WHERE SerialNumber = @OriginalSerialNumber AND ProductionItemID = @OriginalProductionItemID", con, trans);
                stagesCmd.CommandType = CommandType.Text;
                stagesCmd.Parameters.AddWithValue("@OriginalSerialNumber", data.OriginalSerialNumber);
                stagesCmd.Parameters.AddWithValue("@OriginalProductionItemID", data.OriginalProductionItemID);
                stagesCmd.Parameters.AddWithValue("@SerialNumber", data.SerialNumber);
                stagesCmd.Parameters.AddWithValue("@ProductionItemID", data.ProductionItemID);
                stagesCmd.ExecuteNonQuery();
            }

            trans.Commit();
            return 1;
        }
        catch
        {
            trans?.Rollback();
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    private static void UpdateOptionalItemsInProductionColumn(SqlConnection con, SqlTransaction trans, int serialNumber, string productionItemID, string columnName, string? value)
    {
        using SqlCommand checkCmd = new SqlCommand("SELECT COL_LENGTH('ItemsInProduction', @ColumnName)", con, trans);
        checkCmd.Parameters.AddWithValue("@ColumnName", columnName);
        object exists = checkCmd.ExecuteScalar();
        if (exists == DBNull.Value || exists == null) return;

        using SqlCommand updateCmd = new SqlCommand($"UPDATE ItemsInProduction SET {columnName} = @Value WHERE SerialNumber = @SerialNumber AND ProductionItemID = @ProductionItemID", con, trans);
        updateCmd.Parameters.AddWithValue("@Value", string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
        updateCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
        updateCmd.Parameters.AddWithValue("@ProductionItemID", productionItemID);
        updateCmd.ExecuteNonQuery();
    }

    private static void UpdateOptionalItemsInProductionColumn(SqlConnection con, SqlTransaction trans, int serialNumber, string productionItemID, string columnName, int value)
    {
        using SqlCommand checkCmd = new SqlCommand("SELECT COL_LENGTH('ItemsInProduction', @ColumnName)", con, trans);
        checkCmd.Parameters.AddWithValue("@ColumnName", columnName);
        object exists = checkCmd.ExecuteScalar();
        if (exists == DBNull.Value || exists == null) return;

        using SqlCommand updateCmd = new SqlCommand($"UPDATE ItemsInProduction SET {columnName} = @Value WHERE SerialNumber = @SerialNumber AND ProductionItemID = @ProductionItemID", con, trans);
        updateCmd.Parameters.AddWithValue("@Value", value);
        updateCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
        updateCmd.Parameters.AddWithValue("@ProductionItemID", productionItemID);
        updateCmd.ExecuteNonQuery();
    }

    public int DeleteItemInProductionRow(int serialNumber, string productionItemID)
    {
        productionItemID = productionItemID?.Trim() ?? string.Empty;
        if (serialNumber < 0 || string.IsNullOrWhiteSpace(productionItemID)) return 0;

        SqlConnection con = null;
        SqlTransaction trans = null;
        try
        {
            con = connect("myProjDB");
            trans = con.BeginTransaction();

            using (SqlCommand stagesCmd = new SqlCommand("DELETE FROM ProductionItemStage WHERE SerialNumber = @SerialNumber AND ProductionItemID = @ProductionItemID", con, trans))
            {
                stagesCmd.CommandType = CommandType.Text;
                stagesCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                stagesCmd.Parameters.AddWithValue("@ProductionItemID", productionItemID);
                stagesCmd.ExecuteNonQuery();
            }

            int affected;
            using (SqlCommand mainCmd = new SqlCommand("DELETE FROM ItemsInProduction WHERE SerialNumber = @SerialNumber AND ProductionItemID = @ProductionItemID", con, trans))
            {
                mainCmd.CommandType = CommandType.Text;
                mainCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                mainCmd.Parameters.AddWithValue("@ProductionItemID", productionItemID);
                affected = mainCmd.ExecuteNonQuery();
            }

            trans.Commit();
            return affected;
        }
        catch
        {
            trans?.Rollback();
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    private static void ApplyStageTimestamps(SqlConnection con, Dictionary<string, ItemInProduction> itemsMap)
    {
        if (itemsMap.Count == 0) return;

        string? startColumn = GetExistingColumnName(con, "ProductionItemStage", "StartTimeStamp", "StartTimestamp", "StartTime");
        string? finishColumn = GetExistingColumnName(con, "ProductionItemStage", "FinishTimeStamp", "FinishTimestamp", "FinishTime", "EndTime");
        if (startColumn == null && finishColumn == null) return;

        string startSelect = startColumn == null ? "CAST(NULL AS datetime) AS StartValue" : $"{startColumn} AS StartValue";
        string finishSelect = finishColumn == null ? "CAST(NULL AS datetime) AS FinishValue" : $"{finishColumn} AS FinishValue";
        string qualifiedStartSelect = startColumn == null ? "CAST(NULL AS datetime) AS StartValue" : $"pis.{startColumn} AS StartValue";
        string qualifiedFinishSelect = finishColumn == null ? "CAST(NULL AS datetime) AS FinishValue" : $"pis.{finishColumn} AS FinishValue";

        List<string> rowKeys = itemsMap.Keys.ToList();
        string sourceSql = "ProductionItemStage";
        SqlCommand cmd;

        if (rowKeys.Count <= 900)
        {
            List<string> values = new List<string>();
            cmd = new SqlCommand();
            cmd.Connection = con;

            for (int i = 0; i < rowKeys.Count; i++)
            {
                string[] parts = rowKeys[i].Split('|', 2);
                values.Add($"(@Serial{i}, @Item{i})");
                cmd.Parameters.AddWithValue($"@Serial{i}", Convert.ToInt32(parts[0], CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue($"@Item{i}", parts.Length > 1 ? parts[1] : string.Empty);
            }

            sourceSql = $@"ProductionItemStage pis
INNER JOIN (VALUES {string.Join(",", values)}) wanted(SerialNumber, ProductionItemID)
    ON wanted.SerialNumber = pis.SerialNumber AND wanted.ProductionItemID = pis.ProductionItemID";
            cmd.CommandText = $"SELECT pis.SerialNumber, pis.ProductionItemID, pis.ProductionStageID, {qualifiedStartSelect}, {qualifiedFinishSelect} FROM {sourceSql}";
        }
        else
        {
            cmd = new SqlCommand($@"
SELECT SerialNumber, ProductionItemID, ProductionStageID, {startSelect}, {finishSelect}
FROM {sourceSql}", con);
        }
        cmd.CommandType = CommandType.Text;

        using SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int serial = Convert.ToInt32(reader["SerialNumber"]);
            string itemId = Convert.ToString(reader["ProductionItemID"], CultureInfo.InvariantCulture) ?? string.Empty;
            int stageId = Convert.ToInt32(reader["ProductionStageID"]);
            string key = $"{serial}|{itemId}";

            if (!itemsMap.TryGetValue(key, out ItemInProduction? row)) continue;

            ProductionItemStage? stage = row.Stages.FirstOrDefault(s => s.Stage?.ProductionStageID == stageId);
            if (stage == null) continue;

            stage.StartTimeStamp = reader["StartValue"] == DBNull.Value ? null : Convert.ToDateTime(reader["StartValue"]);
            stage.FinishTimeStamp = reader["FinishValue"] == DBNull.Value ? null : Convert.ToDateTime(reader["FinishValue"]);
        }
    }

    private static string? GetExistingColumnName(SqlConnection con, string tableName, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            using SqlCommand cmd = new SqlCommand("SELECT COL_LENGTH(@TableName, @ColumnName)", con);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", candidate);
            object result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value) return candidate;
        }

        return null;
    }

    private static int? ReadNullableInt(SqlDataReader reader, params string[] columnNames)
    {
        foreach (string columnName in columnNames)
        {
            if (!TryGetColumnOrdinal(reader, columnName, out int ordinal))
            {
                continue;
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        return null;
    }

    private static DateTime? ReadNullableDate(SqlDataReader reader, params string[] columnNames)
    {
        foreach (string columnName in columnNames)
        {
            if (!TryGetColumnOrdinal(reader, columnName, out int ordinal))
            {
                continue;
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDateTime(reader.GetValue(ordinal));
        }

        return null;
    }

    //שליפת מיפוי בין פריט ייצור לסוג כטב"ם
    public List<object> GetProductionItemPlaneTypeMappings()
    {
        SqlConnection con = null;
        List<object> list = new List<object>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spProductionItems_GetPlaneTypeMappings", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    productionItemID = reader["ProductionItemID"]?.ToString() ?? string.Empty,
                    planeTypeID = reader["PlaneTypeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PlaneTypeID"]),
                    planeTypeName = reader["PlaneTypeName"] == DBNull.Value ? string.Empty : reader["PlaneTypeName"].ToString()
                });
            }
            return list;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    private static string? ReadNullableString(SqlDataReader reader, params string[] columnNames)
    {
        foreach (string columnName in columnNames)
        {
            if (!TryGetColumnOrdinal(reader, columnName, out int ordinal))
            {
                continue;
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal)?.ToString();
        }

        return null;
    }


    private static bool TryGetColumnOrdinal(SqlDataReader reader, string columnName, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(columnName);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    //משתמשים

    public AppUser? GetUserByUsername(string username)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@Username", username }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_GetByUsername", con, paramDic);
            SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new AppUser
            {
                UserID = Convert.ToInt32(reader["UserID"]),
                Username = reader["Username"]?.ToString() ?? string.Empty,
                PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                FullName = reader["FullName"]?.ToString() ?? string.Empty,
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                MustChangePassword = Convert.ToBoolean(reader["MustChangePassword"]),
                CanViewProduction = Convert.ToBoolean(reader["CanViewProduction"]),
                CanViewStock = Convert.ToBoolean(reader["CanViewStock"]),
                CanManageUsers = Convert.ToBoolean(reader["CanManageUsers"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"]),
                CreatedByUserID = reader["CreatedByUserID"] == DBNull.Value ? null : Convert.ToInt32(reader["CreatedByUserID"])
            };
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public AppUser? GetUserByID(int userID)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@UserID", userID }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_GetByID", con, paramDic);
            SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new AppUser
            {
                UserID = Convert.ToInt32(reader["UserID"]),
                Username = reader["Username"]?.ToString() ?? string.Empty,
                PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                FullName = reader["FullName"]?.ToString() ?? string.Empty,
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                MustChangePassword = Convert.ToBoolean(reader["MustChangePassword"]),
                CanViewProduction = Convert.ToBoolean(reader["CanViewProduction"]),
                CanViewStock = Convert.ToBoolean(reader["CanViewStock"]),
                CanManageUsers = Convert.ToBoolean(reader["CanManageUsers"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"]),
                CreatedByUserID = reader["CreatedByUserID"] == DBNull.Value ? null : Convert.ToInt32(reader["CreatedByUserID"])
            };
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public List<AppUser> GetAllUsers()
    {
        SqlConnection con = null;
        List<AppUser> users = new List<AppUser>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_GetAll", con, null);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new AppUser
                {
                    UserID = Convert.ToInt32(reader["UserID"]),
                    Username = reader["Username"]?.ToString() ?? string.Empty,
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                    MustChangePassword = Convert.ToBoolean(reader["MustChangePassword"]),
                    CanViewProduction = Convert.ToBoolean(reader["CanViewProduction"]),
                    CanViewStock = Convert.ToBoolean(reader["CanViewStock"]),
                    CanManageUsers = Convert.ToBoolean(reader["CanManageUsers"]),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                    UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"]),
                    CreatedByUserID = reader["CreatedByUserID"] == DBNull.Value ? null : Convert.ToInt32(reader["CreatedByUserID"])
                });
            }

            return users;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public int InsertUser(AppUser user)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@Username", user.Username },
                { "@PasswordHash", user.PasswordHash },
                { "@FullName", user.FullName },
                { "@IsActive", user.IsActive },
                { "@MustChangePassword", user.MustChangePassword },
                { "@CanViewProduction", user.CanViewProduction },
                { "@CanViewStock", user.CanViewStock },
                { "@CanManageUsers", user.CanManageUsers },
                { "@CreatedByUserID", user.CreatedByUserID.HasValue ? user.CreatedByUserID.Value : DBNull.Value }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_Insert", con, paramDic);
            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public int UpdateUserAccess(AppUser user)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@UserID", user.UserID },
                { "@IsActive", user.IsActive },
                { "@CanViewProduction", user.CanViewProduction },
                { "@CanViewStock", user.CanViewStock },
                { "@CanManageUsers", user.CanManageUsers }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_UpdateAccess", con, paramDic);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public int UpdateUserDetails(AppUser user)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@UserID", user.UserID },
                { "@FullName", user.FullName },
                { "@IsActive", user.IsActive },
                { "@CanViewProduction", user.CanViewProduction },
                { "@CanViewStock", user.CanViewStock },
                { "@CanManageUsers", user.CanManageUsers }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_UpdateDetails", con, paramDic);
            cmd.ExecuteNonQuery();
            return 1;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public int UpdateUserPassword(int userID, string passwordHash, bool mustChangePassword)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@UserID", userID },
                { "@PasswordHash", passwordHash },
                { "@MustChangePassword", mustChangePassword }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_UpdatePassword", con, paramDic);
            cmd.ExecuteNonQuery();
            return 1;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }


    public int DeleteUser(int userID)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
            {
                { "@UserID", userID }
            };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUsers_Delete", con, paramDic);
            cmd.ExecuteNonQuery();
            return 1;
        }
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    // ==========================================
    // פונקציות עבור רכיב הדשבורד הדינמי וה-AI
    // ==========================================

    // 1. שליפת הגרפים הפעילים של המשתמש באמצעות ה-Stored Procedure
    public DataTable GetUserDashboards(int userID, string dashboardType)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@UserID", userID },
            { "@DashboardType", dashboardType }
        };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUserDashboards_GetActive", con, paramDic);
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 2. שמירת גרף חדש באמצעות ה-Stored Procedure
    public int SaveUserDashboardChart(string chartTitle, string dashboardType, int userID, string chartType, string sqlLogic)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@ChartTitle", chartTitle },
            { "@DashboardType", dashboardType },
            { "@UserID", userID },
            { "@ChartType", chartType },
            { "@SqlLogic", sqlLogic }
        };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUserDashboards_Insert", con, paramDic);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 3. מחיקת גרף קבוע באמצעות ה-Stored Procedure
    public int DeleteUserDashboardChart(int chartID)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            Dictionary<string, object> paramDic = new Dictionary<string, object>
        {
            { "@ChartID", chartID }
        };

            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUserDashboards_Delete", con, paramDic);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 4. הרצת השאילתה הדינמית שה-AI ייצר (כאן חובה טקסט חופשי מבוקר ולא SP)
    public DataTable ExecuteDynamicQuery(string sqlQuery)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");

            // הגנה בסיסית מפני פקודות מחיקה/עדכון זדוניות (SQL Injection)
            string lowerSql = sqlQuery.ToLower();
            if (lowerSql.Contains("drop") || lowerSql.Contains("delete") || lowerSql.Contains("update") || lowerSql.Contains("insert"))
            {
                throw new Exception("רק שאילתות שליפה (SELECT) מותרות בדשבורד זה.");
            }

            SqlCommand cmd = new SqlCommand(sqlQuery, con);
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // פונקציה חדשה: שליפת גרפים לפי סוג דשבורד בלבד (בלי סינון משתמש)
    public DataTable GetChartsByDashboardType(string dashboardType)
    {
        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            EnsureDashboardLayoutColumns(con);

            // שאילתה ישירה שמתעלמת מה-UserID ומביאה רק לפי סוג הדף
            string query = @"SELECT ChartID, ChartTitle, ChartType, SqlLogic, UserID,
                                   ISNULL(LayoutSize, 'small') AS LayoutSize,
                                   DisplayOrder, GridX, GridY
                            FROM UserDashboards
                            WHERE DashboardType = @DashboardType
                            ORDER BY COALESCE(DisplayOrder, ChartID), ChartID";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DashboardType", dashboardType);

            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    public int UpdateDashboardLayout(string dashboardType, List<DashboardLayoutItem> items)
    {
        SqlConnection con = null;
        SqlTransaction transaction = null;
        try
        {
            con = connect("myProjDB");
            EnsureDashboardLayoutColumns(con);
            transaction = con.BeginTransaction();

            int rowsAffected = 0;
            const string query = @"UPDATE UserDashboards
                                   SET DisplayOrder = @DisplayOrder,
                                       LayoutSize = @LayoutSize,
                                       GridX = @GridX,
                                       GridY = @GridY
                                   WHERE ChartID = @ChartID
                                     AND DashboardType = @DashboardType";

            foreach (DashboardLayoutItem item in items ?? new List<DashboardLayoutItem>())
            {
                using SqlCommand cmd = new SqlCommand(query, con, transaction);
                cmd.Parameters.AddWithValue("@ChartID", item.ChartID);
                cmd.Parameters.AddWithValue("@DashboardType", dashboardType);
                cmd.Parameters.AddWithValue("@DisplayOrder", item.DisplayOrder);
                cmd.Parameters.AddWithValue("@LayoutSize", NormalizeDashboardLayoutSize(item.LayoutSize));
                cmd.Parameters.AddWithValue("@GridX", item.GridX);
                cmd.Parameters.AddWithValue("@GridY", item.GridY);
                rowsAffected += cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return rowsAffected;
        }
        catch (Exception)
        {
            transaction?.Rollback();
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    private static string NormalizeDashboardLayoutSize(string layoutSize)
    {
        return layoutSize switch
        {
            "wide" => "wide",
            "fullWidth" => "fullWidth",
            "large" => "large",
            "extraLarge" => "extraLarge",
            _ => "small"
        };
    }

    private static void EnsureDashboardLayoutColumns(SqlConnection con)
    {
        const string query = @"
            IF COL_LENGTH('dbo.UserDashboards', 'DisplayOrder') IS NULL
                ALTER TABLE dbo.UserDashboards ADD DisplayOrder INT NULL;
            IF COL_LENGTH('dbo.UserDashboards', 'LayoutSize') IS NULL
                ALTER TABLE dbo.UserDashboards ADD LayoutSize NVARCHAR(20) NOT NULL CONSTRAINT DF_UserDashboards_LayoutSize DEFAULT ('small');
            IF COL_LENGTH('dbo.UserDashboards', 'GridX') IS NULL
                ALTER TABLE dbo.UserDashboards ADD GridX INT NULL;
            IF COL_LENGTH('dbo.UserDashboards', 'GridY') IS NULL
                ALTER TABLE dbo.UserDashboards ADD GridY INT NULL;";

        using SqlCommand cmd = new SqlCommand(query, con);
        cmd.ExecuteNonQuery();
    }
}
