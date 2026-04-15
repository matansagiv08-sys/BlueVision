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

        SqlConnection con = null;
        SqlCommand cmd;
        SqlDataReader reader;
        try
        {
            con = connect("myProjDB");
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

    public List<BomPlaneOption> GetBomPlaneOptions()
    {
        List<BomPlaneOption> options = new List<BomPlaneOption>();

        SqlConnection con = null;
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spBom_GetPlaneOptions", con, null);
            cmd.CommandType = CommandType.StoredProcedure;

            SqlDataReader reader = cmd.ExecuteReader();
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

    public BomFilterOptions GetBomFilterOptions(int? planeTypeId = null)
    {
        BomFilterOptions options = new BomFilterOptions();

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
                    Price = null
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

    public InventoryFilterOptions GetInventoryFilterOptions()
    {
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

                supplierUpdatesTable.Rows.Add(itemCode, supplierId);
            }

            if (supplierUpdatesTable.Rows.Count > 0)
            {
                using (SqlCommand createTempCmd = new SqlCommand("dbo.SP_CreateSupplierUpdatesTempTable", con))
                {
                    createTempCmd.CommandType = CommandType.StoredProcedure;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#SupplierUpdates";
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
                lastPoDateUpdatesTable.Rows.Add(mapping.Key, mapping.Value.Date);
            }

            if (lastPoDateUpdatesTable.Rows.Count > 0)
            {
                using (SqlCommand createTempCmd = new SqlCommand("dbo.SP_CreateLastPoDateUpdatesTempTable", con))
                {
                    createTempCmd.CommandType = CommandType.StoredProcedure;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#LastPODateUpdates";
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

                updatesTable.Rows.Add(itemCode, itemGrpId);
            }

            if (updatesTable.Rows.Count > 0)
            {
                using (SqlCommand createTempCmd = new SqlCommand("dbo.SP_CreateItemGroupUpdatesTempTable", con))
                {
                    createTempCmd.CommandType = CommandType.StoredProcedure;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#ItemGroupUpdates";
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
                buyMethodUpdatesTable.Rows.Add(mapping.Key, mapping.Value);
            }

            if (buyMethodUpdatesTable.Rows.Count > 0)
            {
                using (SqlCommand createTempCmd = new SqlCommand("dbo.SP_CreateBuyMethodUpdatesTempTable", con))
                {
                    createTempCmd.CommandType = CommandType.StoredProcedure;
                    createTempCmd.ExecuteNonQuery();
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    bulkCopy.DestinationTableName = "#BuyMethodUpdates";
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
            ImportedRows = updatedRows,
            DeletedProductionItems = deletedProductionItems,
            InsertedProductionItems = insertedProductionItems,
            UpdatedProductionItems = updatedProductionItems,
            FinalProductionItemsCount = finalProductionItemsCount
        };
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
                        ProductionItem = new ProductionItem
                        {
                            ProductionItemID = reader["ProductionItemID"].ToString(),
                            ItemName = reader["ItemName"].ToString()
                        },
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public List<Project> GetProjects()
    {
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
                    DueDate = (DateTime)reader["DueDate"],
                    PriorityLevel = (byte)reader["PriorityLevel"]
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

    public int InsertProject(Project p)
    {
        SqlConnection con = null;
        Dictionary<string, object> d = new Dictionary<string, object> {
        {"@ProjectName", p.ProjectName},
        {"@DueDate", p.DueDate},
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
        catch (Exception)
        {
            throw;
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
        catch (Exception)
        {
            throw;
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
        catch (Exception)
        {
            throw;
        }
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
            throw;
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
        catch (Exception)
        {
            throw;
        }
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }

    public List<PriorityOption> GetPriorityLevels()
    {
        SqlConnection con = null;
        List<PriorityOption> list = new List<PriorityOption>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spPriorityLevels_GetAll", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PriorityOption
                {
                    ID = Convert.ToInt32(reader["PriorityID"]),
                    Name = reader["PriorityName"]?.ToString() ?? string.Empty
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

    public List<PlaneOption> GetPlanes()
    {
        SqlConnection con = null;
        List<PlaneOption> list = new List<PlaneOption>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spPlanes_GetBasic", con, null);
            cmd.CommandType = CommandType.StoredProcedure;
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PlaneOption
                {
                    PlaneID = reader["PlaneID"]?.ToString() ?? string.Empty,
                    TypeID = Convert.ToInt32(reader["PlaneTypeID"]),
                    ProjectID = Convert.ToInt32(reader["ProjectID"])
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

    public int InsertItemInProduction(InsertItemInProductionRequest item)
    {
        SqlConnection con = null;
        SqlTransaction trans = null;

        try
        {
            con = connect("myProjDB");
            if (con.State != System.Data.ConnectionState.Open) con.Open();
            trans = con.BeginTransaction();

            string projectName = item.ProjectName;
            string planeID = item.PlaneID;
            string productionItemID = item.ProductionItemID;
            string workOrderID = item.WorkOrderID;
            int serialNumber = item.SerialNumber ?? 0;
            int planeTypeID = item.PlaneTypeID ?? 0;

            HandleProjectAndPlane(con, trans, projectName, planeID, planeTypeID);
            HandleWorkOrder(con, trans, workOrderID); 

            using (SqlCommand mainCmd = new SqlCommand("dbo.SP_InsertItemInProduction", con, trans))
            {
                mainCmd.CommandType = CommandType.StoredProcedure;
                mainCmd.Parameters.AddWithValue("@itemID", productionItemID);
                mainCmd.Parameters.AddWithValue("@serial", serialNumber);
                mainCmd.Parameters.AddWithValue("@planeID", (object)planeID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@priority", item.PriorityID ?? 1);
                mainCmd.Parameters.AddWithValue("@workOrder", (object)workOrderID ?? DBNull.Value);
                mainCmd.Parameters.AddWithValue("@qty", item.Quantity ?? 1);
                mainCmd.Parameters.AddWithValue("@comments", (object)item.Comments ?? DBNull.Value);
                mainCmd.ExecuteNonQuery();
            }

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

    private void HandleProjectAndPlane(SqlConnection con, SqlTransaction trans, string projectName, string planeID, int planeTypeID)
    {
        using (SqlCommand cmd = new SqlCommand("dbo.SP_HandleProjectAndPlane", con, trans))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@pName", (object)projectName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@planeID", (object)planeID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@typeID", planeTypeID);
            cmd.ExecuteNonQuery();
        }
    }
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
        catch (Exception)
        {
            throw;
        }
        finally { if (con != null) con.Close(); }
    }
}
