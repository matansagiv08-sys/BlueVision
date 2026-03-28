using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Diagnostics;
using ClosedXML.Excel;
using Server.Models;

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

    public int ImportInventoryItemsFromExcel(string? filePath)
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
        return updatedRows;
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
    public List<ItemInProduction> ReadItemsInProduction()
    {
        SqlConnection con = null;
        List<ItemInProduction> list = new List<ItemInProduction>();
        try
        {
            con = connect("myProjDB");
            SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetItemsInProduction", con, null);
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new ItemInProduction
                {
                    SerialNumber = (int)reader["SerialNumber"],
                    ProductionItem = new ProductionItem
                    {
                        ProductionItemID = reader["ProductionItemID"].ToString(),
                        ItemName = reader["ItemName"].ToString()
                    },
                    PlaneID = new Plane
                    {
                        PlaneID = (int)reader["PlaneID"],
                        Type = new PlaneType { PlaneTypeName = reader["PlaneTypeName"].ToString() }
                    },
                    PriorityLevel = Convert.ToInt32(reader["PriorityLevel"]),
                    WorkOrderID = Convert.ToInt32(reader["WorkOrderID"]),
                    plannedQty = Convert.ToInt32(reader["PlannedQty"]),
                    comments = reader["Comments"]?.ToString(),
                    Stages = new List<ProductionItemStage>()
                });
            }
            return list;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

    //public int InsertItemInProduction(ItemInProduction item)
    //{
    //    SqlConnection con = null;
    //    try
    //    {
    //        con = connect("myProjDB");

    //        Dictionary<string, object> paramDic = new Dictionary<string, object>();
    //        paramDic.Add("@SerialNumber", item.SerialNumber);
    //        paramDic.Add("@ProductionItemID", item.ProductionItemID);
    //        paramDic.Add("@PlaneID", item.PlaneID);
    //        paramDic.Add("@PriorityLevel", item.PriorityLevel);
    //        paramDic.Add("@WorkOrderID", item.WorkOrderID);
    //        paramDic.Add("@PlannedQty", item.PlannedQty);
    //        paramDic.Add("@Comments", item.Comments ?? (object)DBNull.Value);

    //        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInsertItemInProduction", con, paramDic);
    //        return cmd.ExecuteNonQuery();
    //    }
    //    catch (Exception ex) { throw ex; }
    //    finally { if (con != null) con.Close(); }
    //}

    //public int UpdateItemInProduction(ItemInProduction item) // שינוי הפרמטר לקבלת האובייקט
    //{
    //    SqlConnection con = null;
    //    try
    //    {
    //        con = connect("myProjDB");

    //        Dictionary<string, object> paramDic = new Dictionary<string, object>();
    //        paramDic.Add("@SerialNumber", item.SerialNumber);
    //        paramDic.Add("@ProductionItemID", item.ProductionItemID);
    //        paramDic.Add("@PlaneID", item.PlaneID);
    //        paramDic.Add("@PriorityLevel", item.PriorityLevel);
    //        paramDic.Add("@WorkOrderID", item.WorkOrderID);
    //        paramDic.Add("@PlannedQty", item.PlannedQty);
    //        paramDic.Add("@Comments", item.Comments ?? (object)DBNull.Value);

    //        // כאן חסר היה להעביר את ה-paramDic למתודה
    //        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUpdateItemInProduction", con, paramDic);
    //        return cmd.ExecuteNonQuery();
    //    }
    //    catch (Exception ex) { throw (ex); }
    //    finally { if (con != null) con.Close(); }
    //}

    //public int DeleteItemInProduction(int serialNumber, string ProductionItemID)
    //{
    //    SqlConnection con = null;
    //    try
    //    {
    //        con = connect("myProjDB");
    //        Dictionary<string, object> paramDic = new Dictionary<string, object>();
    //        paramDic.Add("@SerialNumber", serialNumber);
    //        paramDic.Add("@ProductionItemID", ProductionItemID); // עכשיו זה string

    //        SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spDeleteItemInProduction", con, paramDic);
    //        return cmd.ExecuteNonQuery();
    //    }
    //    catch (Exception ex) { throw ex; }
    //    finally { if (con != null) con.Close(); }
    //}

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

    //public List<Plane> GetPlanes()
    //{
    //    SqlConnection con = connect("myProjDB");
    //    List<Plane> list = new List<Plane>();
    //    // שימוש בפונקציה הכללית הקיימת אצלך
    //    SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spGetAllPlanes", con, null);
    //    SqlDataReader reader = cmd.ExecuteReader();
    //    while (reader.Read())
    //    {
    //        list.Add(new Plane
    //        {
    //            PlaneID = (int)reader["PlaneID"],
    //            PlaneTypeID = (int)reader["PlaneTypeID"],
    //            ProjectID = (int)reader["ProjectID"],
    //            PriorityLevel = (byte)reader["PriorityLevel"]
    //        });
    //    }
    //    con.Close();
    //    return list;
    //}

    //public int InsertPlane(Plane p)
    //{
    //    Dictionary<string, object> d = new Dictionary<string, object> {
    //    {"@PlaneTypeID", p.PlaneTypeID},
    //    {"@ProjectID", p.ProjectID},
    //    {"@PriorityLevel", p.PriorityLevel}
    //};
    //    SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spInsertPlane", connect("myProjDB"), d);
    //    return cmd.ExecuteNonQuery();
    //}

    //public int UpdatePlane(Plane p)
    //{
    //    Dictionary<string, object> d = new Dictionary<string, object> {
    //    {"@PlaneID", p.PlaneID},
    //    {"@PlaneTypeID", p.PlaneTypeID},
    //    {"@ProjectID", p.ProjectID},
    //    {"@PriorityLevel", p.PriorityLevel}
    //};
    //    SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spUpdatePlane", connect("myProjDB"), d);
    //    return cmd.ExecuteNonQuery();
    //}

    //public int DeletePlane(int id)
    //{
    //    Dictionary<string, object> d = new Dictionary<string, object> { { "@PlaneID", id } };
    //    SqlCommand cmd = CreateCommandWithStoredProcedureGeneral("spDeletePlane", connect("myProjDB"), d);
    //    return cmd.ExecuteNonQuery();
    //}

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
                stagesList.Add(new ProductionStage
                {
                    ProductionStageID = (int)reader["ProductionStageID"],
                    ProductionStageName = reader["ProductionStageName"].ToString()
                });
            }
            return stagesList;
        }
        catch (Exception ex) { throw ex; }
        finally { if (con != null) con.Close(); }
    }

}