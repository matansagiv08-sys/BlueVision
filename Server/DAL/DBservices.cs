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

        Console.WriteLine("Reading sheet: פריטים ומלאים");
        IXLWorksheet detailsSheet = workbook.Worksheet("פריטים ומלאים");
        Dictionary<string, string> itemToGroupMap = BuildItemToGroupMap(detailsSheet);

        Console.WriteLine("Built item-to-group map with " + itemToGroupMap.Count + " entries");

        List<string> uniqueGroupNames = itemToGroupMap.Values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine("Found " + uniqueGroupNames.Count + " unique group names");

        int insertedGroups = 0;
        int updatedRows = 0;
        Dictionary<string, int> groupNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        using (SqlConnection con = connect("myProjDB"))
        {
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
        }

        Console.WriteLine("Import finished successfully");
        return updatedRows;
    }

    private static Dictionary<string, string> BuildItemToGroupMap(IXLWorksheet sheet)
    {
        Dictionary<string, string> itemToGroupMap = new Dictionary<string, string>();

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string itemCode = row.Cell(1).GetValue<string>().Trim();
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
