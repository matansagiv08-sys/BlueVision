using Server.DAL;
using ClosedXML.Excel;
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
        //פתיחת הקובץ גם אם פתוח במחשב ולקריאה בלבד
        using FileStream excelStream = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XLWorkbook workbook = new XLWorkbook(excelStream);
        //גישה לגליונות האקסל
        IXLWorksheet detailsSheet = workbook.Worksheet("פריטים ומלאים");
        IXLWorksheet supplierSheet = workbook.Worksheet("ספק אחרון לפריט");
        IXLWorksheet wbBomSheet = workbook.Worksheet("עץ מוצר WB");
        IXLWorksheet tbvBomSheet = workbook.Worksheet("עץ מוצר TBV");

        Dictionary<string, string> itemToGroupMap = BuildItemToGroupMap(detailsSheet);
        Dictionary<string, string> itemToBuyMethod = BuildItemToBuyMethodMap(detailsSheet);
        Dictionary<string, string> itemToSupplierMap = BuildItemToSupplierMap(supplierSheet);
        Dictionary<string, DateTime> itemToLastPODateMap = BuildItemToLastPODateMap(supplierSheet);

        //יצירת רשימה של עץ מוצר מסוגי המטוסים
        List<BomRow> wbBomRows = BuildBomRowsForSheet(wbBomSheet, "WB");
        List<BomRow> tbvBomRows = BuildBomRowsForSheet(tbvBomSheet, "TBV");
        //  חישוב האם החלק מיועד לגוף או למטוס
        CalculateBodyPlaneForBomRows(wbBomRows);
        CalculateBodyPlaneForBomRows(tbvBomRows);
        //יצירת רשימה של ספקים
        List<string> uniqueSuppliers = itemToSupplierMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        //יצירת רשימה של קבוצות
        List<string> uniqueGroupNames = itemToGroupMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        //ריכוז כל מה שנשלף מהאקסל לאובייקט אחד
        InventoryImportData importData = new InventoryImportData
        {
            ItemToGroupMap = itemToGroupMap,
            ItemToBuyMethod = itemToBuyMethod,
            ItemToSupplierMap = itemToSupplierMap,
            ItemToLastPODateMap = itemToLastPODateMap,
            WbBomRows = wbBomRows,
            TbvBomRows = tbvBomRows,
            UniqueSuppliers = uniqueSuppliers,
            UniqueGroupNames = uniqueGroupNames
        };
        // הכנסת הנתונים מהאקסל לDB
        DBservices dbs = new DBservices();
        return dbs.ImportInventoryDataToDatabase(importData);
    }

    //שליפת נתוני המלאי
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

    //שליפת אפשרויות הפילטור
    public InventoryFilterOptions GetInventoryFilterOptions()
    {
        DBservices dbs = new DBservices();
        return dbs.GetInventoryFilterOptions();
    }

    private static string ResolveExcelPath(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return filePath;
        }

        string fallback = Path.Combine(AppContext.BaseDirectory, "Output", "final_inventory_data.xlsx");
        if (File.Exists(fallback))
        {
            return fallback;
        }

        throw new FileNotFoundException("Inventory source file not found", filePath ?? fallback);
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

    //
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
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number) return Convert.ToDecimal(cell.GetDouble());

        string raw = cell.GetValue<string>().Trim();
        if (string.IsNullOrWhiteSpace(raw)) return 0;

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

    private static int ToSafeInt(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToInt32(Math.Round(cell.GetDouble(), MidpointRounding.AwayFromZero));
        }

        string raw = cell.GetValue<string>().Trim();
        if (string.IsNullOrWhiteSpace(raw)) return 0;

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

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetExcelCellTextPreserveFormatting(IXLCell cell)
    {
        if (cell == null || cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.Number)
        {
            decimal numeric = Convert.ToDecimal(cell.Value);
            return decimal.Truncate(numeric) == numeric
                ? ((long)numeric).ToString(CultureInfo.InvariantCulture)
                : numeric.ToString(CultureInfo.InvariantCulture);
        }

        return cell.GetValue<string>().Trim();
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
    public Dictionary<string, string> ItemToGroupMap { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> ItemToBuyMethod { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> ItemToSupplierMap { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, DateTime> ItemToLastPODateMap { get; set; } = new Dictionary<string, DateTime>();
    public List<BomRow> WbBomRows { get; set; } = new List<BomRow>();
    public List<BomRow> TbvBomRows { get; set; } = new List<BomRow>();
    public List<string> UniqueSuppliers { get; set; } = new List<string>();
    public List<string> UniqueGroupNames { get; set; } = new List<string>();
}
