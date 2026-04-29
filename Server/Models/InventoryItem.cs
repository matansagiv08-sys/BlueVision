using Server.DAL;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using System.Globalization;

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
        Console.WriteLine("Import started");
        string finalPath = ResolveExcelPath(filePath);
        Console.WriteLine("Using Excel file: " + finalPath);
        Console.WriteLine("Excel last modified: " + File.GetLastWriteTime(finalPath).ToString("yyyy-MM-dd HH:mm:ss"));

        using FileStream excelStream = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XLWorkbook workbook = new XLWorkbook(excelStream);

        IXLWorksheet detailsSheet = workbook.Worksheet("פריטים ומלאים");
        IXLWorksheet supplierSheet = workbook.Worksheet("ספק אחרון לפריט");
        IXLWorksheet wbBomSheet = workbook.Worksheet("עץ מוצר WB");
        IXLWorksheet tbvBomSheet = workbook.Worksheet("עץ מוצר TBV");

        Dictionary<string, string> itemToGroupMap = BuildItemToGroupMap(detailsSheet);
        Dictionary<string, string> itemToBuyMethod = BuildItemToBuyMethodMap(detailsSheet);
        List<InventoryBaseRow> inventoryBaseRows = BuildInventoryBaseRows(detailsSheet);
        Dictionary<string, string> itemToSupplierMap = BuildItemToSupplierMap(supplierSheet);
        Dictionary<string, DateTime> itemToLastPODateMap = BuildItemToLastPODateMap(supplierSheet);
        List<BomRow> wbBomRows = BuildBomRowsForSheet(wbBomSheet, "WB");
        List<BomRow> tbvBomRows = BuildBomRowsForSheet(tbvBomSheet, "TBV");

        CalculateBodyPlaneForBomRows(wbBomRows);
        CalculateBodyPlaneForBomRows(tbvBomRows);

        List<string> uniqueSuppliers = itemToSupplierMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> uniqueGroupNames = itemToGroupMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        InventoryImportData importData = new InventoryImportData
        {
            ItemToGroupMap = itemToGroupMap,
            ItemToBuyMethod = itemToBuyMethod,
            InventoryBaseRows = inventoryBaseRows,
            ItemToSupplierMap = itemToSupplierMap,
            ItemToLastPODateMap = itemToLastPODateMap,
            WbBomRows = wbBomRows,
            TbvBomRows = tbvBomRows,
            UniqueSuppliers = uniqueSuppliers,
            UniqueGroupNames = uniqueGroupNames
        };

        DBservices dbs = new DBservices();
        InventoryImportResult result = dbs.ImportInventoryDataToDatabase(importData);
        dbs.SetLastInventoryImportTimestamp(DateTime.Now);
        return result;
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
        DBservices dbs = new DBservices();
        return dbs.GetInventoryItems(page, pageSize, search, stockStatus, planeTypeId, itemGrpID, buyMethod, supplierID, bodyPlane, lastPODate);
    }

    public InventoryFilterOptions GetInventoryFilterOptions()
    {
        DBservices dbs = new DBservices();
        return dbs.GetInventoryFilterOptions();
    }

    public ExcelLastModifiedInfo GetExcelLastModifiedInfo(string? filePath = null)
    {
        ExcelPathResolution resolution = ResolveExcelPathResolution(filePath);
        if (!resolution.FileExists)
        {
            return new ExcelLastModifiedInfo
            {
                FileExists = false,
                ExcelLastModifiedAt = null,
                ResolvedPath = resolution.ResolvedPath,
                Message = "Excel file not found"
            };
        }

        return new ExcelLastModifiedInfo
        {
            FileExists = true,
            ExcelLastModifiedAt = File.GetLastWriteTime(resolution.ResolvedPath),
            ResolvedPath = resolution.ResolvedPath,
            Message = "Excel file found"
        };
    }

    public LastInventoryImportTimestampInfo GetLastInventoryImportTimestampInfo()
    {
        DBservices dbs = new DBservices();
        return new LastInventoryImportTimestampInfo
        {
            LastImportTimestamp = dbs.GetLastInventoryImportTimestamp()
        };
    }

    private static string ResolveExcelPath(string? filePath)
    {
        ExcelPathResolution resolution = ResolveExcelPathResolution(filePath);
        if (resolution.FileExists)
        {
            return resolution.ResolvedPath;
        }

        throw new FileNotFoundException($"Inventory source file not found. Attempted paths: {string.Join(" | ", resolution.AttemptedPaths)}");
    }

    private static ExcelPathResolution ResolveExcelPathResolution(string? filePath)
    {
        List<string> attemptedPaths = new List<string>();

        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return new ExcelPathResolution
            {
                FileExists = true,
                ResolvedPath = filePath,
                AttemptedPaths = attemptedPaths
            };
        }
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            attemptedPaths.Add(filePath);
        }

        string currentDirectory = Directory.GetCurrentDirectory();

        string appSettingsPath = Path.Combine(currentDirectory, "appsettings.json");
        string? configuredPath = null;
        if (File.Exists(appSettingsPath))
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            configuredPath = configuration["InventoryImport:DefaultExcelPath"];
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string candidateFromConfig = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(currentDirectory, configuredPath));

            if (File.Exists(candidateFromConfig))
            {
                return new ExcelPathResolution
                {
                    FileExists = true,
                    ResolvedPath = candidateFromConfig,
                    AttemptedPaths = attemptedPaths
                };
            }

            attemptedPaths.Add(candidateFromConfig);
        }

        string projectDatafilesPath = Path.GetFullPath(Path.Combine(currentDirectory, "Datafiles", "BlueBird_Data.xlsx"));
        if (File.Exists(projectDatafilesPath))
        {
            return new ExcelPathResolution
            {
                FileExists = true,
                ResolvedPath = projectDatafilesPath,
                AttemptedPaths = attemptedPaths
            };
        }
        attemptedPaths.Add(projectDatafilesPath);

        string fallback = Path.Combine(AppContext.BaseDirectory, "Output", "final_inventory_data.xlsx");
        if (File.Exists(fallback))
        {
            return new ExcelPathResolution
            {
                FileExists = true,
                ResolvedPath = fallback,
                AttemptedPaths = attemptedPaths
            };
        }
        attemptedPaths.Add(fallback);

        return new ExcelPathResolution
        {
            FileExists = false,
            ResolvedPath = attemptedPaths.FirstOrDefault() ?? string.Empty,
            AttemptedPaths = attemptedPaths
        };
    }

    private class ExcelPathResolution
    {
        public bool FileExists { get; set; }
        public string ResolvedPath { get; set; } = string.Empty;
        public List<string> AttemptedPaths { get; set; } = new List<string>();
    }

    private static Dictionary<string, string> BuildItemToGroupMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToGroupMap = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
            string itmsGrpNam = GetCellText(row.Cell(3), sheet.Name, row.RowNumber(), "C", "ItemGroupName");

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(itmsGrpNam))
            {
                continue;
            }

            itemToGroupMap[itemCode] = itmsGrpNam;
        }

        return itemToGroupMap;
    }

    private static Dictionary<string, string> BuildItemToBuyMethodMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToBuyMethod = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            string buyMethod = GetCellText(row.Cell(4), sheet.Name, row.RowNumber(), "D", "BuyMethod").ToUpper();
            if (buyMethod != "B" && buyMethod != "M")
            {
                continue;
            }

            itemToBuyMethod[itemCode] = buyMethod;
        }

        return itemToBuyMethod;
    }

    private static List<InventoryBaseRow> BuildInventoryBaseRows(IXLWorksheet sheet)
    {
        Dictionary<string, InventoryBaseRow> rowsByItemCode = new Dictionary<string, InventoryBaseRow>(StringComparer.OrdinalIgnoreCase);

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            string itemName = GetCellText(row.Cell(2), sheet.Name, row.RowNumber(), "B", "ItemName");
            string buyMethodRaw = GetCellText(row.Cell(4), sheet.Name, row.RowNumber(), "D", "BuyMethod").ToUpperInvariant();
            string? buyMethod = (buyMethodRaw == "B" || buyMethodRaw == "M") ? buyMethodRaw : null;

            rowsByItemCode[itemCode] = new InventoryBaseRow
            {
                InventoryItemID = itemCode,
                ItemName = NullIfEmpty(itemName),
                BuyMethod = buyMethod,
                ExcelRowNumber = row.RowNumber()
            };
        }

        return rowsByItemCode.Values.ToList();
    }

    private static Dictionary<string, string> BuildItemToSupplierMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToSupplier = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
            string lastVendor = GetCellText(row.Cell(2), sheet.Name, row.RowNumber(), "B", "LastVendor");

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(lastVendor))
            {
                continue;
            }

            itemToSupplier[itemCode] = lastVendor;
        }

        return itemToSupplier;
    }

    private static Dictionary<string, DateTime> BuildItemToLastPODateMap(IXLWorksheet sheet)
    {
        Dictionary<string, DateTime> itemToLastPODate = new Dictionary<string, DateTime>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
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
                string rawDate = GetCellText(dateCell, sheet.Name, row.RowNumber(), "C", "LastPODate");
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
            string itemCode = GetExcelCellTextPreserveFormatting(row.Cell(1), sheet.Name, row.RowNumber(), "A", "ItemCode");
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                continue;
            }

            string itemName = GetCellText(row.Cell(2), sheet.Name, row.RowNumber(), "B", "ItemName");
            string measureUnit = GetCellText(row.Cell(3), sheet.Name, row.RowNumber(), "C", "MeasureUnit");
            decimal quantity = ToSafeDecimal(row.Cell(4), sheet.Name, row.RowNumber(), "D", "Quantity");
            string warehouse = GetCellText(row.Cell(5), sheet.Name, row.RowNumber(), "E", "Warehouse");
            int bomLevel = ToSafeInt(row.Cell(6), sheet.Name, row.RowNumber(), "F", "BomLevel");
            string hasChildRaw = GetCellText(row.Cell(7), sheet.Name, row.RowNumber(), "G", "HasChild");
            string buyMethod = GetCellText(row.Cell(8), sheet.Name, row.RowNumber(), "H", "BuyMethod");

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

    private static decimal ToSafeDecimal(IXLCell cell, string sheetName, int rowNumber, string columnLetter, string fieldName)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number) return Convert.ToDecimal(cell.GetDouble());

        string raw = GetCellText(cell, sheetName, rowNumber, columnLetter, fieldName);
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out double currentCultureValue))
        {
            return Convert.ToDecimal(currentCultureValue);
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariantValue))
        {
            return Convert.ToDecimal(invariantValue);
        }

        throw new InvalidDataException($"Sheet: {sheetName}, Row: {rowNumber}, Column: {columnLetter}, Field: {fieldName}, Value: '{raw}' - invalid decimal value");
    }

    private static int ToSafeInt(IXLCell cell, string sheetName, int rowNumber, string columnLetter, string fieldName)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToInt32(Math.Round(cell.GetDouble(), MidpointRounding.AwayFromZero));
        }

        string raw = GetCellText(cell, sheetName, rowNumber, columnLetter, fieldName);
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariantNumber))
        {
            return Convert.ToInt32(Math.Round(invariantNumber, MidpointRounding.AwayFromZero));
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out double currentCultureNumber))
        {
            return Convert.ToInt32(Math.Round(currentCultureNumber, MidpointRounding.AwayFromZero));
        }

        throw new InvalidDataException($"Sheet: {sheetName}, Row: {rowNumber}, Column: {columnLetter}, Field: {fieldName}, Value: '{raw}' - invalid integer value");
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetExcelCellTextPreserveFormatting(IXLCell cell, string sheetName, int rowNumber, string columnLetter, string fieldName)
    {
        if (cell == null || cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.Number)
        {
            if (!cell.TryGetValue<decimal>(out decimal numeric))
            {
                if (cell.TryGetValue<double>(out double numericDouble))
                {
                    numeric = Convert.ToDecimal(numericDouble);
                }
                else
                {
                    string rawNumeric = cell.GetString().Trim();
                    throw new InvalidDataException($"Sheet: {sheetName}, Row: {rowNumber}, Column: {columnLetter}, Field: {fieldName}, Value: '{rawNumeric}' - invalid numeric code value");
                }
            }

            return decimal.Truncate(numeric) == numeric
                ? ((long)numeric).ToString(CultureInfo.InvariantCulture)
                : numeric.ToString(CultureInfo.InvariantCulture);
        }

        return GetCellText(cell, sheetName, rowNumber, columnLetter, fieldName);
    }

    private static string GetCellText(IXLCell cell, string sheetName, int rowNumber, string columnLetter, string fieldName)
    {
        try
        {
            return cell.GetString().Trim();
        }
        catch (Exception ex)
        {
            string rawValue = cell.Value.ToString();
            throw new InvalidDataException($"Sheet: {sheetName}, Row: {rowNumber}, Column: {columnLetter}, Field: {fieldName}, Value: '{rawValue}' - text conversion failed: {ex.Message}", ex);
        }
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

public class InventoryImportData
{
    public List<InventoryBaseRow> InventoryBaseRows { get; set; } = new List<InventoryBaseRow>();
    public Dictionary<string, string> ItemToGroupMap { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> ItemToBuyMethod { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> ItemToSupplierMap { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, DateTime> ItemToLastPODateMap { get; set; } = new Dictionary<string, DateTime>();
    public List<BomRow> WbBomRows { get; set; } = new List<BomRow>();
    public List<BomRow> TbvBomRows { get; set; } = new List<BomRow>();
    public List<string> UniqueSuppliers { get; set; } = new List<string>();
    public List<string> UniqueGroupNames { get; set; } = new List<string>();
}

public class InventoryBaseRow
{
    public string InventoryItemID { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public string? BuyMethod { get; set; }
    public int ExcelRowNumber { get; set; }
}

public class ExcelLastModifiedInfo
{
    public bool FileExists { get; set; }
    public DateTime? ExcelLastModifiedAt { get; set; }
    public string ResolvedPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LastInventoryImportTimestampInfo
{
    public DateTime? LastImportTimestamp { get; set; }
}
