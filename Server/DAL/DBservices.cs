using System.Data;
using System.Data.SqlClient;
using System.Globalization;
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

        IXLWorksheet baseSheet = workbook.Worksheet("פריטים ומלאים");
        IXLWorksheet openReqSheet = workbook.Worksheet("כמות פתוחה בדרישות");
        IXLWorksheet openPoSheet = workbook.Worksheet("כמות פתוחה בהזמנות רכש");

        List<IXLRow> baseRows = baseSheet.RowsUsed().Skip(1).ToList();
        Console.WriteLine("Read base sheet rows: " + baseRows.Count);

        // Build side-sheet dictionaries and merge them by ItemCode with the base sheet.
        // Missing ItemCode in side sheets defaults to 0 later during row creation.
        // Side sheet dictionary: ItemCode (col A) -> OpenPurchaseRequestQty (col B)
        Dictionary<string, int> openReqByItemCode = BuildSingleValueDictionary(openReqSheet, 2);
        Console.WriteLine("Built open request dictionary: " + openReqByItemCode.Count);

        // Side sheet dictionary: ItemCode (col A) -> (OpenPurchaseOrderQty, ApprovedOrderQty, UnapprovedOrderQty)
        Dictionary<string, (int OpenPo, int Approved, int Unapproved)> openPoByItemCode = BuildOpenPoDictionary(openPoSheet);
        Console.WriteLine("Built open PO dictionary: " + openPoByItemCode.Count);

        List<InventoryItem> itemsToInsert = new List<InventoryItem>();

        foreach (IXLRow row in baseRows)
        {
            string itemCode = row.Cell(1).GetValue<string>().Trim();

            // Merge key is ItemCode from column A.
            // Skip base rows where ItemCode is empty.
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            (int OpenPo, int Approved, int Unapproved) poData = openPoByItemCode.ContainsKey(itemCode)
                ? openPoByItemCode[itemCode]
                : (0, 0, 0);

            InventoryItem item = new InventoryItem
            {
                InventoryItemID = itemCode,
                ItemName = NullIfEmpty(row.Cell(2).GetValue<string>()),
                ItemGrpID = null,
                BuyMethod = null,
                Price = null,
                PlatformID = null,
                SupplierID = null,
                Whse01_QTY = ToSafeInt(row.Cell(5)),
                Whse03_QTY = ToSafeInt(row.Cell(6)),
                Whse90_QTY = ToSafeInt(row.Cell(7)),
                OpenPurchaseRequestQty = openReqByItemCode.ContainsKey(itemCode) ? openReqByItemCode[itemCode] : 0,
                OpenPurchaseOrderQty = poData.OpenPo,
                ApprovedOrderQty = poData.Approved,
                UnapprovedOrderQty = poData.Unapproved,
                PlaneOrBody = null,
                LastPODate = null
            };

            itemsToInsert.Add(item);
        }

        Console.WriteLine("Prepared items to insert: " + itemsToInsert.Count);

        DataTable inventoryTable = BuildInventoryItemsDataTable(itemsToInsert);

        using SqlConnection con = connect("myProjDB");
        Console.WriteLine("Opened SQL connection");
        using SqlTransaction transaction = con.BeginTransaction();
        Console.WriteLine("Began SQL transaction");

        try
        {
            using (SqlCommand deleteCmd = new SqlCommand("DELETE FROM InventoryItems", con, transaction))
            {
                deleteCmd.CommandTimeout = 120;
                deleteCmd.ExecuteNonQuery();
            }
            Console.WriteLine("Deleted existing InventoryItems rows");

            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con, SqlBulkCopyOptions.TableLock, transaction))
            {
                bulkCopy.DestinationTableName = "InventoryItems";
                bulkCopy.BulkCopyTimeout = 300;
                bulkCopy.BatchSize = 1000;

                bulkCopy.ColumnMappings.Add("InventoryItemID", "InventoryItemID");
                bulkCopy.ColumnMappings.Add("ItemName", "ItemName");
                bulkCopy.ColumnMappings.Add("ItemGrpID", "ItemGrpID");
                bulkCopy.ColumnMappings.Add("BuyMethod", "BuyMethod");
                bulkCopy.ColumnMappings.Add("Price", "Price");
                bulkCopy.ColumnMappings.Add("PlatformID", "PlatformID");
                bulkCopy.ColumnMappings.Add("SupplierID", "SupplierID");
                bulkCopy.ColumnMappings.Add("Whse01_QTY", "Whse01_QTY");
                bulkCopy.ColumnMappings.Add("Whse03_QTY", "Whse03_QTY");
                bulkCopy.ColumnMappings.Add("Whse90_QTY", "Whse90_QTY");
                bulkCopy.ColumnMappings.Add("OpenPurchaseRequestQty", "OpenPurchaseRequestQty");
                bulkCopy.ColumnMappings.Add("OpenPurchaseOrderQty", "OpenPurchaseOrderQty");
                bulkCopy.ColumnMappings.Add("ApprovedOrderQty", "ApprovedOrderQty");
                bulkCopy.ColumnMappings.Add("UnapprovedOrderQty", "UnapprovedOrderQty");
                bulkCopy.ColumnMappings.Add("PlaneOrBody", "PlaneOrBody");
                bulkCopy.ColumnMappings.Add("LastPODate", "LastPODate");

                bulkCopy.WriteToServer(inventoryTable);
            }
            Console.WriteLine("Inserted " + itemsToInsert.Count + " rows");

            transaction.Commit();
            Console.WriteLine("Transaction committed");
            Console.WriteLine("Import finished successfully");
            return itemsToInsert.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Import failed with exception:");
            Console.WriteLine(ex.ToString());

            try
            {
                transaction.Rollback();
                Console.WriteLine("Rollback completed");
            }
            catch (Exception rollbackEx)
            {
                Console.WriteLine("Rollback failed:");
                Console.WriteLine(rollbackEx.ToString());
            }

            throw;
        }
    }

    private static DataTable BuildInventoryItemsDataTable(List<InventoryItem> items)
    {
        DataTable table = new DataTable("InventoryItems");

        table.Columns.Add("InventoryItemID", typeof(string));
        table.Columns.Add("ItemName", typeof(string));
        table.Columns.Add("ItemGrpID", typeof(int));
        table.Columns.Add("BuyMethod", typeof(int));
        table.Columns.Add("Price", typeof(double));
        table.Columns.Add("PlatformID", typeof(int));
        table.Columns.Add("SupplierID", typeof(int));
        table.Columns.Add("Whse01_QTY", typeof(int));
        table.Columns.Add("Whse03_QTY", typeof(int));
        table.Columns.Add("Whse90_QTY", typeof(int));
        table.Columns.Add("OpenPurchaseRequestQty", typeof(int));
        table.Columns.Add("OpenPurchaseOrderQty", typeof(int));
        table.Columns.Add("ApprovedOrderQty", typeof(int));
        table.Columns.Add("UnapprovedOrderQty", typeof(int));
        table.Columns.Add("PlaneOrBody", typeof(bool));
        table.Columns.Add("LastPODate", typeof(DateTime));

        foreach (InventoryItem item in items)
        {
            DataRow row = table.NewRow();

            row["InventoryItemID"] = item.InventoryItemID;
            row["ItemName"] = item.ItemName ?? (object)DBNull.Value;
            row["ItemGrpID"] = DBNull.Value;
            row["BuyMethod"] = DBNull.Value;
            row["Price"] = DBNull.Value;
            row["PlatformID"] = DBNull.Value;
            row["SupplierID"] = DBNull.Value;
            row["Whse01_QTY"] = item.Whse01_QTY ?? 0;
            row["Whse03_QTY"] = item.Whse03_QTY ?? 0;
            row["Whse90_QTY"] = item.Whse90_QTY ?? 0;
            row["OpenPurchaseRequestQty"] = item.OpenPurchaseRequestQty ?? 0;
            row["OpenPurchaseOrderQty"] = item.OpenPurchaseOrderQty ?? 0;
            row["ApprovedOrderQty"] = item.ApprovedOrderQty ?? 0;
            row["UnapprovedOrderQty"] = item.UnapprovedOrderQty ?? 0;
            row["PlaneOrBody"] = DBNull.Value;
            row["LastPODate"] = DBNull.Value;

            table.Rows.Add(row);
        }

        return table;
    }

    private static Dictionary<string, int> BuildSingleValueDictionary(IXLWorksheet sheet, int valueColumnIndex)
    {
        Dictionary<string, int> dictionary = new Dictionary<string, int>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = row.Cell(1).GetValue<string>().Trim();
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
            string itemCode = row.Cell(1).GetValue<string>().Trim();
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
}
