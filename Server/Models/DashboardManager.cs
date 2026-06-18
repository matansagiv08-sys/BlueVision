using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using Server.DAL;

namespace Server.Models
{
    public class DashboardManager
    {
        private readonly DBservices _dbs = new DBservices();

        private static readonly HashSet<string> AllowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "InventoryItems",
            "Suppliers",
            "Groups",
            "ItemPlatforms",
            "PlaneTypes",
            "BOM",
            "ProductionItems",
            "ItemsInProduction",
            "ProductionItemStage",
            "ProductionStages",
            "ProductionStatuses",
            "Projects",
            "Planes",
            "WorkOrders",
            "PriorityLevels"
        };

        private static readonly HashSet<string> BlockedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Users", "UsersTable", "users_LC", "Baseball_2026_Users_MS", "UsersCards_LC",
            "SystemSettings", "ExcelImportMetadata", "UserDashboards", "sysdiagrams",
            "apartmentsTbl", "Baseball_2026_Cards_MS", "Baseball_2026_UsersCards_MS",
            "cards_LC", "Courses", "Flights_2026", "IngredientTable", "MealsTable",
            "Orders_LC", "Players_LC", "Players_MS", "Players_XX", "Restaurants_LC",
            "StudentInCourse", "Students", "Students_2026", "Students_2026_matan",
            "UsersMealsTable", "Platforms"
        };

        private static readonly string[] BlockedKeywordPatterns =
        {
            @"\bINSERT\b", @"\bUPDATE\b", @"\bDELETE\b", @"\bMERGE\b", @"\bTRUNCATE\b",
            @"\bDROP\b", @"\bALTER\b", @"\bCREATE\b", @"\bRENAME\b", @"\bEXEC\b",
            @"\bEXECUTE\b", @"\bSP_EXECUTESQL\b", @"\bGRANT\b", @"\bREVOKE\b", @"\bDENY\b",
            @"\bBACKUP\b", @"\bRESTORE\b", @"\bDBCC\b", @"\bDECLARE\b", @"\bSET\b",
            @"\bBEGIN\b", @"\bCOMMIT\b", @"\bROLLBACK\b", @"\bTRY\b", @"\bCATCH\b",
            @"\bWAITFOR\b", @"\bOPENQUERY\b", @"\bOPENROWSET\b", @"\bOPENDATASOURCE\b", @"\bINTO\b"
        };

        private static readonly string[] BlockedPromptTerms =
        {
            "users", "user", "password", "passwords", "email", "emails", "login", "auth", "authentication",
            "api key", "apikey", "system settings", "settings", "metadata", "excel import",
            "students", "courses", "flights", "restaurants", "cards"
        };

        public List<object> GetChartsByDashboardType(string dashboardType)
        {
            DataTable dt = _dbs.GetChartsByDashboardType(dashboardType);
            var chartsList = new List<object>();
            foreach (DataRow row in dt.Rows)
            {
                chartsList.Add(new
                {
                    ChartID = row["ChartID"],
                    ChartTitle = row["ChartTitle"],
                    ChartType = row["ChartType"],
                    SqlLogic = row["SqlLogic"],
                    UserID = row["UserID"],
                    LayoutSize = row.Table.Columns.Contains("LayoutSize") ? row["LayoutSize"] : "small",
                    DisplayOrder = row.Table.Columns.Contains("DisplayOrder") ? row["DisplayOrder"] : DBNull.Value,
                    GridX = row.Table.Columns.Contains("GridX") ? row["GridX"] : DBNull.Value,
                    GridY = row.Table.Columns.Contains("GridY") ? row["GridY"] : DBNull.Value
                });
            }
            return chartsList;
        }

        public int UpdateDashboardLayout(string dashboardType, List<DashboardLayoutItem> items)
        {
            return _dbs.UpdateDashboardLayout(dashboardType, items ?? new List<DashboardLayoutItem>());
        }

        public int SaveChart(string chartTitle, string dashboardType, int userId, string chartType, string sqlLogic)
        {
            return _dbs.SaveUserDashboardChart(chartTitle, dashboardType, userId, chartType, sqlLogic);
        }

        public int DeleteChart(int chartId)
        {
            return _dbs.DeleteUserDashboardChart(chartId);
        }

        public int RenameChart(int chartId, string dashboardType, string chartTitle)
        {
            return _dbs.RenameUserDashboardChart(chartId, dashboardType, chartTitle);
        }

        public async Task<DashboardGenerateResult> GenerateChartAsync(string prompt, string? visualizationType = null, string? resultType = null, string? dashboardType = null)
        {
            string safePrompt = (prompt ?? string.Empty).Trim();
            if (safePrompt.Length == 0)
            {
                return DashboardGenerateResult.Fail("EMPTY_PROMPT", "לא התקבלה בקשה תקינה ליצירת שאילתה.");
            }

            if (IsBlockedPromptTopic(safePrompt))
            {
                return DashboardGenerateResult.Fail("BLOCKED_TOPIC", "הבקשה נחסמה כי היא מתייחסת למידע שאינו מורשה להצגה בדשבורד.");
            }

            AiChartResponse aiResponse;
            if (LooksLikeSql(safePrompt))
            {
                // TODO: Direct SELECT input bypasses AI semantic context; restrict this path or make it admin-only later.
                aiResponse = new AiChartResponse
                {
                    Sql = safePrompt,
                    VisualizationType = string.IsNullOrWhiteSpace(visualizationType) ? "bar" : visualizationType,
                    ResultType = string.IsNullOrWhiteSpace(resultType) ? "single_series" : resultType,
                    Explanation = "Provided SQL query",
                    Assumptions = new List<string>()
                };
            }
            else
            {
                try
                {
                    aiResponse = await GenerateChartFromPrompt(safePrompt, dashboardType);
                }
                catch (AiProviderException ex)
                {
                    return DashboardGenerateResult.Fail(ex.ErrorCode, ex.UserMessage);
                }

                if (aiResponse.IsAllowed == false)
                {
                    return DashboardGenerateResult.Fail(
                        string.IsNullOrWhiteSpace(aiResponse.ErrorCode) ? "BLOCKED_TOPIC" : aiResponse.ErrorCode,
                        string.IsNullOrWhiteSpace(aiResponse.Message)
                            ? "הבקשה נחסמה כי היא מתייחסת למידע שאינו מורשה להצגה בדשבורד."
                            : aiResponse.Message
                    );
                }
            }

            SqlValidationResult validation = ValidateSql(aiResponse.Sql, aiResponse.VisualizationType, aiResponse.ResultType);
            if (!validation.IsValid)
            {
                return DashboardGenerateResult.Fail(validation.ErrorCode, validation.ErrorMessage);
            }

            DataTable dt;
            try
            {
                dt = _dbs.ExecuteDynamicQuery(validation.NormalizedSql);
            }
            catch (Exception ex)
            {
                if (!LooksLikeSqlIdentifierBindingError(ex.Message) || LooksLikeSql(safePrompt))
                {
                    throw;
                }

                AiChartResponse repairedResponse;
                try
                {
                    repairedResponse = await GenerateChartFromPrompt(
                        BuildSqlRepairPrompt(safePrompt, validation.NormalizedSql, ex.Message),
                        dashboardType
                    );
                }
                catch (AiProviderException providerEx)
                {
                    return DashboardGenerateResult.Fail(providerEx.ErrorCode, providerEx.UserMessage);
                }

                if (repairedResponse.IsAllowed == false)
                {
                    return DashboardGenerateResult.Fail(
                        string.IsNullOrWhiteSpace(repairedResponse.ErrorCode) ? "BLOCKED_TOPIC" : repairedResponse.ErrorCode,
                        string.IsNullOrWhiteSpace(repairedResponse.Message)
                            ? "הבקשה נחסמה כי היא מתייחסת למידע שאינו מורשה להצגה בדשבורד."
                            : repairedResponse.Message
                    );
                }

                SqlValidationResult repairedValidation = ValidateSql(repairedResponse.Sql, repairedResponse.VisualizationType, repairedResponse.ResultType);
                if (!repairedValidation.IsValid)
                {
                    return DashboardGenerateResult.Fail(repairedValidation.ErrorCode, repairedValidation.ErrorMessage);
                }

                dt = _dbs.ExecuteDynamicQuery(repairedValidation.NormalizedSql);
                aiResponse = repairedResponse;
                validation = repairedValidation;
            }

            SqlValidationResult shapeValidation = ValidateResultShape(dt, aiResponse.VisualizationType, aiResponse.ResultType);
            if (!shapeValidation.IsValid)
            {
                return DashboardGenerateResult.Fail(shapeValidation.ErrorCode, shapeValidation.ErrorMessage);
            }

            var labels = new List<string>();
            var values = new List<double>();
            var rows = new List<Dictionary<string, object?>>();

            foreach (DataRow row in dt.Rows)
            {
                var rowMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn col in dt.Columns)
                {
                    rowMap[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                rows.Add(rowMap);

                if (IsSingleSeries(aiResponse.ResultType, aiResponse.VisualizationType))
                {
                    labels.Add(Convert.ToString(row["Label"], CultureInfo.InvariantCulture) ?? string.Empty);
                    values.Add(Convert.ToDouble(row["Value"], CultureInfo.InvariantCulture));
                }
            }

            return DashboardGenerateResult.Ok(new DashboardQueryExecutionResult
            {
                Labels = labels,
                Values = values,
                Rows = rows,
                SqlQuery = validation.NormalizedSql,
                VisualizationType = NormalizeVisualizationType(aiResponse.VisualizationType),
                ResultType = NormalizeResultType(aiResponse.ResultType),
                Explanation = aiResponse.Explanation ?? string.Empty,
                Assumptions = aiResponse.Assumptions ?? new List<string>()
            });
        }

        public string GetDatabaseSchema()
        {
            try
            {
                string allowedTableList = string.Join(",", AllowedTables.Select(t => $"'{t}'"));
                string schemaQuery = $@"
                    SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = 'dbo' 
                    AND TABLE_NAME IN ({allowedTableList})
                    ORDER BY TABLE_NAME, ORDINAL_POSITION";

                DataTable dt = _dbs.ExecuteDynamicQuery(schemaQuery);
                StringBuilder schemaBuilder = new StringBuilder();
                schemaBuilder.AppendLine("Database Schema:");

                string currentTable = "";
                foreach (DataRow row in dt.Rows)
                {
                    string tableName = row["TABLE_NAME"].ToString();
                    string columnName = row["COLUMN_NAME"].ToString();
                    string dataType = row["DATA_TYPE"].ToString();

                    if (currentTable != tableName)
                    {
                        currentTable = tableName;
                        schemaBuilder.AppendLine($"\nTable: {currentTable}\nColumns:");
                    }
                    schemaBuilder.AppendLine($"- {columnName} ({dataType})");
                }

                return schemaBuilder.ToString();
            }
            catch (Exception ex)
            {
                return $"Error loading schema: {ex.Message}";
            }
        }

        private static SemanticContextSelection BuildSemanticContext(string userPrompt)
        {
            var selected = new List<(string Name, string Text)>
            {
                ("GLOBAL_SQL_SAFETY_RULES", GlobalSqlSafetyRulesContext()),
                ("BLOCKED_TABLES_CONTEXT", BlockedTablesContext())
            };

            string prompt = (userPrompt ?? string.Empty).ToLowerInvariant();
            bool inventory = ContainsAny(prompt, "inventory", "stock", "מלאי", "מחסן", "warehouse", "supplier", "ספק", "group", "קבוצה", "value", "שווי");
            bool procurement = ContainsAny(prompt, "procurement", "רכש", "purchase", "approved", "unapproved", "הזמנות", "בקשות", "purchase order", "purchase request");
            bool bom = ContainsAny(prompt, "bom", "עץ מוצר", "דרישות", "requirements", "רכיבים", "requirement");
            bool platform = ContainsAny(prompt, "platform", "פלטפורמה", "plane type", "tbv", "wb");
            bool production = ContainsAny(prompt, "production", "ייצור", "פקע", "פק\"ע", "פק״ע", "work order", "סדר עבודה", "priority", "עדיפות");
            bool stageStatus = ContainsAny(prompt, "station", "status", "stage", "תחנה", "סטטוס", "תקוע", "בוצע", "בתהליך", "completed", "stuck");
            bool projectPlane = ContainsAny(prompt, "project", "פרויקט", "plane", "מטוס", "tail", "זנב", "due date", "תאריך יעד");

            if (procurement) inventory = true;
            if (platform) inventory = true;
            if (stageStatus) production = true;
            if (projectPlane) production = true;

            if (!inventory && !procurement && !bom && !platform && !production && !stageStatus && !projectPlane)
            {
                production = true;
                stageStatus = true;
                projectPlane = true;
                inventory = true;
            }

            if (production) selected.Add(("PRODUCTION_CORE_CONTEXT", ProductionCoreContext()));
            if (stageStatus) selected.Add(("PRODUCTION_STAGE_STATUS_CONTEXT", ProductionStageStatusContext()));
            if (projectPlane || platform) selected.Add(("PROJECT_PLANE_CONTEXT", ProjectPlaneContext()));
            if (inventory) selected.Add(("INVENTORY_CONTEXT", InventoryContext()));
            if (procurement) selected.Add(("PROCUREMENT_CONTEXT", ProcurementContext()));
            if (bom) selected.Add(("BOM_CONTEXT", BomContext()));
            if (platform) selected.Add(("ITEM_PLATFORM_CONTEXT", ItemPlatformContext()));

            bool includeInventoryExamples = inventory && !procurement;
            string examples = ExampleSqlPatterns(production, stageStatus, projectPlane, includeInventoryExamples, procurement, bom);
            if (!string.IsNullOrWhiteSpace(examples))
            {
                selected.Add(("EXAMPLE_SQL_PATTERNS", examples));
            }

            return new SemanticContextSelection
            {
                SectionNames = selected.Select(s => s.Name).ToList(),
                ContextText = string.Join("\n\n", selected.Select(s => $"[{s.Name}]\n{s.Text}"))
            };
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            return terms.Any(term => text.Contains(term.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
        }

        private static string GlobalSqlSafetyRulesContext()
        {
            return @"Use SQL Server syntax only. Generate exactly one SELECT query. Never use SELECT *.
Do not use SQL comments or semicolons. Do not use destructive, admin, DDL, DML, or procedural keywords.
Always include TOP (N) in the outer SELECT and N must be <= 200.
Query only approved BlueVision analytics tables from the provided schema/context.
Never query blocked tables or invent table/column names.
For bar, line, or pie charts, return exactly two columns named Label and Value, where Value is numeric.
For table output, return explicit named columns and keep the result practical.
Return only the required JSON object with isAllowed, visualizationType, resultType, sql, explanation, and assumptions.
If the request asks for blocked, sensitive, auth, admin, system, sample, or unrelated data, return isAllowed=false with errorCode BLOCKED_TOPIC.";
        }

        private static string BlockedTablesContext()
        {
            return @"Never query these tables: Users, UsersTable, users_LC, UserDashboards, SystemSettings, ExcelImportMetadata, sysdiagrams.
Never query old/sample/unrelated tables: apartmentsTbl, Baseball_2026_Cards_MS, Baseball_2026_Users_MS, Baseball_2026_UsersCards_MS, cards_LC, Courses, Flights_2026, IngredientTable, MealsTable, Orders_LC, Players_LC, Players_MS, Players_XX, Restaurants_LC, StudentInCourse, Students, Students_2026, Students_2026_matan, UsersCards_LC, UsersMealsTable.
Platforms is unknown/unreviewed and must not be used.
Block requests about users, passwords, authentication, system settings, saved dashboards, sample data, courses, students, meals, restaurants, baseball/cards, players, flights, apartments, or unrelated domains.";
        }

        private static string ProductionCoreContext()
        {
            return @"Approved tables: ItemsInProduction, ProductionItems, WorkOrders, PriorityLevels.
ItemsInProduction stores current production rows/items. Primary key: SerialNumber + ProductionItemID. Important columns: PlaneID, PriorityLevel, WorkOrderID, PlannedQty, Comments, DueDate, PlaneTypeID, ProjectName.
ProductionItems is the production item catalog. Primary key: ProductionItemID. Important columns: ProductionItemID, ItemName.
WorkOrders stores work order IDs. Primary key: WorkOrderID.
PriorityLevels stores priority labels. Primary key: PriorityID. Important columns: PriorityID, PriorityName.
Joins: ItemsInProduction.ProductionItemID = ProductionItems.ProductionItemID; ItemsInProduction.WorkOrderID = WorkOrders.WorkOrderID; ItemsInProduction.PriorityLevel = PriorityLevels.PriorityID.
A current production item is one ItemsInProduction row, uniquely identified by SerialNumber + ProductionItemID. For item counts, count ItemsInProduction rows or COUNT DISTINCT SerialNumber + ProductionItemID.
Do not use SerialNumber alone as the unique production item key. PlannedQty is planned quantity, not row count. ItemsInProduction.DueDate is item due date, not project due date.";
        }

        private static string ProductionStageStatusContext()
        {
            return @"Approved tables: ProductionItemStage, ProductionStages, ProductionStatuses, ItemsInProduction.
ProductionItemStage stores workflow stage/status rows. Primary key: SerialNumber + ProductionItemID + ProductionStageID. Important columns: ProductionStatusID, StartTimeStamp, FinishTimeStamp, Comment, ManualPriority.
ProductionStages stores station/stage lookup. Primary key: ProductionStageID. Important columns: ProductionStageName, TargetDuration, StageOrder.
ProductionStatuses stores status labels. Primary key: ProductionStatusID. Important column: ProductionStatusName.
Mandatory parent join: ProductionItemStage.SerialNumber = ItemsInProduction.SerialNumber AND ProductionItemStage.ProductionItemID = ItemsInProduction.ProductionItemID.
Never join ProductionItemStage to ItemsInProduction on ProductionItemID alone or SerialNumber alone.
Stage/status joins: ProductionItemStage.ProductionStageID = ProductionStages.ProductionStageID; ProductionItemStage.ProductionStatusID = ProductionStatuses.ProductionStatusID.
One production item has multiple ProductionItemStage rows. Do not count stage rows as production item counts unless intentionally analyzing stages.
Completed status is ProductionStatusID = 4. Use ProductionStages.StageOrder for process order; do not assume ProductionStageID is process order.
Current station is usually the first non-completed stage ordered by StageOrder. For stuck items, only use a clear user-specified delay rule or state the assumption.";
        }

        private static string ProjectPlaneContext()
        {
            return @"Approved tables: Projects, Planes, PlaneTypes, ItemsInProduction.
Projects stores project metadata. Primary key: ProjectID. Important columns: ProjectName, DueDate, PriorityLevel.
Planes stores aircraft/tail/plane records. Primary key: PlaneID. Important columns: PlaneTypeID, ProjectID, PriorityLevel.
PlaneTypes stores plane type lookup. Primary key: PlaneTypeID. Important column: PlaneTypeName.
Joins: ItemsInProduction.PlaneID = Planes.PlaneID; Planes.ProjectID = Projects.ProjectID; Planes.PlaneTypeID = PlaneTypes.PlaneTypeID.
ItemsInProduction.PlaneTypeID is a fallback when PlaneID is missing. ItemsInProduction.ProjectName is a fallback when linked plane/project is missing.
Use COALESCE(Projects.ProjectName, ItemsInProduction.ProjectName) for production project name.
Use COALESCE(Planes.PlaneTypeID, ItemsInProduction.PlaneTypeID) for production plane type ID.
When starting from ItemsInProduction, prefer LEFT JOIN to Planes, Projects, and PlaneTypes so production rows without linked plane/project are not dropped.
Projects.DueDate is project due date. ItemsInProduction.DueDate is item due date. Projects.PriorityLevel and Planes.PriorityLevel are not FK-enforced to PriorityLevels.";
        }

        private static string InventoryContext()
        {
            return @"Approved tables: InventoryItems, Groups, Suppliers.
InventoryItems stores inventory master and stock. Primary key: InventoryItemID. Important columns: ItemName, ItemGrpID, BuyMethod, Price, SupplierID, Whse01_QTY, Whse03_QTY, Whse90_QTY, OpenPurchaseRequestQty, OpenPurchaseOrderQty, ApprovedOrderQty, UnapprovedOrderQty, BodyPlane, LastPODate, IsActive.
Groups stores inventory item groups. Primary key: ItemGrpID. Important column: ItemGrpName.
Suppliers stores suppliers. Primary key: SupplierID. Important column: SupplierName.
Joins: InventoryItems.ItemGrpID = Groups.ItemGrpID; InventoryItems.SupplierID = Suppliers.SupplierID.
Stock quantity formula: ISNULL(Whse01_QTY,0) + ISNULL(Whse03_QTY,0) + ISNULL(Whse90_QTY,0).
Inventory value formula: ISNULL(Price,0) * (ISNULL(Whse01_QTY,0) + ISNULL(Whse03_QTY,0) + ISNULL(Whse90_QTY,0)).
Warehouse quantities are stock quantities. Procurement columns are not stock. Use IsActive = 1 for current/active inventory. Use LEFT JOIN to keep items with missing group/supplier.";
        }

        private static string ProcurementContext()
        {
            return @"Approved table: InventoryItems.
OpenPurchaseRequestQty is quantity in open purchase requests.
OpenPurchaseOrderQty is quantity in open purchase orders.
ApprovedOrderQty is quantity in approved orders.
UnapprovedOrderQty is quantity in unapproved orders.
Procurement quantities are not warehouse stock. Do not add procurement quantities to stock unless the user explicitly asks for expected/combined availability.
For current stock, use warehouse quantity columns. For procurement analytics, aggregate procurement columns separately or with clear labels. Use IsActive = 1 for current/active items.";
        }

        private static string BomContext()
        {
            return @"Approved tables: BOM, PlaneTypes, InventoryItems.
BOM stores bill of materials by plane type. Primary key: BomSerialID. Important columns: PlaneTypeID, RowOrder, InventoryItemID, ItemName, Quantity, MeasureUnit, Warehouse, BomLevel, HasChild, BuyMethod, BodyPlane.
PlaneTypes stores plane type lookup. Primary key: PlaneTypeID. Important column: PlaneTypeName.
Join: BOM.PlaneTypeID = PlaneTypes.PlaneTypeID.
Possible logical join, not FK-enforced: BOM.InventoryItemID may match InventoryItems.InventoryItemID.
BOM.Quantity means required quantity in the BOM, not inventory stock. BomLevel indicates hierarchy level. HasChild indicates child components. RowOrder is BOM row order. BuyMethod indicates buy/make behavior. BodyPlane indicates body/plane classification.
Avoid overcounting repeated BOM rows. When comparing BOM requirements to stock, aggregate BOM requirements by item first if needed, then join to InventoryItems.";
        }

        private static string ItemPlatformContext()
        {
            return @"Approved tables: ItemPlatforms, InventoryItems, PlaneTypes.
ItemPlatforms is a many-to-many bridge between inventory items and plane types/platforms. Primary key: InventoryItemID + PlaneTypeID.
Joins: ItemPlatforms.InventoryItemID = InventoryItems.InventoryItemID; ItemPlatforms.PlaneTypeID = PlaneTypes.PlaneTypeID.
One inventory item can belong to multiple plane types and one plane type can have many inventory items. Joining InventoryItems through ItemPlatforms can multiply inventory rows.
When calculating stock/value totals, avoid double-counting the same inventory item across multiple plane types unless the user asks for per-plane-type allocation.";
        }

        private static string ExampleSqlPatterns(bool production, bool stageStatus, bool projectPlane, bool inventory, bool procurement, bool bom)
        {
            var examples = new List<string>();

            if (production && projectPlane)
            {
                examples.Add(@"Production items by project:
SELECT TOP (50) COALESCE(p.ProjectName, iip.ProjectName, N'ללא פרויקט') AS Label, COUNT(*) AS Value
FROM ItemsInProduction iip
LEFT JOIN Planes pl ON pl.PlaneID = iip.PlaneID
LEFT JOIN Projects p ON p.ProjectID = pl.ProjectID
GROUP BY COALESCE(p.ProjectName, iip.ProjectName, N'ללא פרויקט')
ORDER BY Value DESC");
            }

            if (stageStatus)
            {
                examples.Add(@"Non-completed items by station:
SELECT TOP (50) ps.ProductionStageName AS Label, COUNT(DISTINCT CAST(pis.SerialNumber AS varchar(20)) + '|' + pis.ProductionItemID) AS Value
FROM ProductionItemStage pis
INNER JOIN ProductionStages ps ON ps.ProductionStageID = pis.ProductionStageID
WHERE ISNULL(pis.ProductionStatusID, 1) <> 4
GROUP BY ps.ProductionStageName, ps.StageOrder
ORDER BY ps.StageOrder");

                examples.Add(@"Table-only current station by production item. Use visualizationType='table' and resultType='table' because CurrentStatus is text, not numeric chart Value:
SELECT TOP (100) CAST(iip.SerialNumber AS varchar(20)) + ' - ' + iip.ProductionItemID AS ProductionItem, ps.ProductionStageName + ' / ' + ISNULL(pst.ProductionStatusName, N'לא ידוע') AS CurrentStatus
FROM ItemsInProduction iip
OUTER APPLY (
    SELECT TOP (1) pis.ProductionStageID, pis.ProductionStatusID
    FROM ProductionItemStage pis
    INNER JOIN ProductionStages ps2 ON ps2.ProductionStageID = pis.ProductionStageID
    WHERE pis.SerialNumber = iip.SerialNumber AND pis.ProductionItemID = iip.ProductionItemID
    ORDER BY CASE WHEN ISNULL(pis.ProductionStatusID, 1) <> 4 THEN 0 ELSE 1 END, ps2.StageOrder
) cur
LEFT JOIN ProductionStages ps ON ps.ProductionStageID = cur.ProductionStageID
LEFT JOIN ProductionStatuses pst ON pst.ProductionStatusID = cur.ProductionStatusID
ORDER BY iip.SerialNumber, iip.ProductionItemID");
            }

            if (inventory)
            {
                examples.Add(@"Inventory value by supplier:
SELECT TOP (50) ISNULL(s.SupplierName, N'ללא ספק') AS Label, SUM(ISNULL(ii.Price, 0) * (ISNULL(ii.Whse01_QTY, 0) + ISNULL(ii.Whse03_QTY, 0) + ISNULL(ii.Whse90_QTY, 0))) AS Value
FROM InventoryItems ii
LEFT JOIN Suppliers s ON s.SupplierID = ii.SupplierID
WHERE ii.IsActive = 1
GROUP BY ISNULL(s.SupplierName, N'ללא ספק')
ORDER BY Value DESC");
            }

            if (procurement)
            {
                examples.Add(@"Procurement quantity by group:
SELECT TOP (50) ISNULL(g.ItemGrpName, N'ללא קבוצה') AS Label, SUM(ISNULL(ii.OpenPurchaseRequestQty, 0) + ISNULL(ii.OpenPurchaseOrderQty, 0) + ISNULL(ii.ApprovedOrderQty, 0) + ISNULL(ii.UnapprovedOrderQty, 0)) AS Value
FROM InventoryItems ii
LEFT JOIN Groups g ON g.ItemGrpID = ii.ItemGrpID
WHERE ii.IsActive = 1
GROUP BY ISNULL(g.ItemGrpName, N'ללא קבוצה')
ORDER BY Value DESC");
            }

            if (bom)
            {
                examples.Add(@"BOM requirements by plane type:
SELECT TOP (50) pt.PlaneTypeName AS Label, SUM(ISNULL(b.Quantity, 0)) AS Value
FROM BOM b
INNER JOIN PlaneTypes pt ON pt.PlaneTypeID = b.PlaneTypeID
GROUP BY pt.PlaneTypeName
ORDER BY Value DESC");
            }

            return examples.Count == 0 ? string.Empty : string.Join("\n\n", examples);
        }

        public SqlValidationResult ValidateSqlForSave(string sqlLogic, string? chartType)
        {
            string viz = NormalizeVisualizationType(chartType);
            string resultType = viz == "table" ? "table" : "single_series";
            return ValidateSql(sqlLogic, viz, resultType);
        }

        public async Task<AiChartResponse> GenerateChartFromPrompt(string userPrompt, string? dashboardType = null)
        {
            string dbSchema = GetDatabaseSchema();
            string dashboardScopeInstruction = BuildDashboardScopeInstruction(dashboardType);
            SemanticContextSelection semanticContext = BuildSemanticContext(userPrompt);
            Debug.WriteLine($"Dashboard AI semantic sections: {string.Join(", ", semanticContext.SectionNames)}");
            Console.WriteLine($"Dashboard AI semantic sections: {string.Join(", ", semanticContext.SectionNames)}");

            string systemInstruction = $@"
    You are an expert SQL Server analyst for the 'BlueVision' drone company.
    Generate exactly one safe SQL Server SELECT query for dashboard analytics.

    {dbSchema}

    Semantic database context:
    {semanticContext.ContextText}

    RULES:
    1. Return ONLY a valid JSON object. No markdown, no code fences.
    2. Output fields must be:
       visualizationType: one of 'bar','line','pie','table'
       resultType: one of 'single_series','multi_series','table'
       sql: one SQL Server query
       explanation: short text
       assumptions: string array
    3. SQL MUST be a single SELECT statement only.
    4. Never use comments, semicolon, EXEC, INTO, DECLARE, SET, BEGIN, COMMIT, ROLLBACK.
    5. Never use SELECT *.
    6. Always include TOP (N) with N <= 200.
    7. Allowed tables only:
       InventoryItems, Suppliers, Groups, ItemPlatforms, PlaneTypes, BOM, ProductionItems, ItemsInProduction,
       ProductionItemStage, ProductionStages, ProductionStatuses, Projects, Planes, WorkOrders, PriorityLevels
    8. Blocked tables include Users, UsersTable, users_LC, Baseball_2026_Users_MS, UsersCards_LC,
       SystemSettings, ExcelImportMetadata, UserDashboards and any unrelated tables.
    9. For bar/pie/line with single_series: return exactly two columns named Label and Value (Value numeric).
    10. For table: return explicit named columns and keep width practical.
     11. If the request is blocked, sensitive, admin/auth/user data, or unrelated to production/inventory domain,
         DO NOT invent a substitute chart. Return isAllowed=false with errorCode BLOCKED_TOPIC.
     12. SQL aliases must be valid and bound. Never reference an alias or column that is not defined in the current SELECT scope.

    Dashboard focus:
    {dashboardScopeInstruction}

    Allowed format:
    {{
        ""isAllowed"": true,
        ""visualizationType"": ""bar"",
        ""resultType"": ""single_series"",
        ""sql"": ""SELECT TOP (50) ItemName AS Label, SUM(ISNULL(Whse01_QTY,0)+ISNULL(Whse03_QTY,0)+ISNULL(Whse90_QTY,0)) AS Value FROM InventoryItems GROUP BY ItemName ORDER BY Value DESC"",
        ""explanation"": ""Total stock quantity by item"",
        ""assumptions"": [""Using three warehouse quantity columns""]
    }}

    Blocked format:
    {{
        ""isAllowed"": false,
        ""errorCode"": ""BLOCKED_TOPIC"",
        ""message"": ""The request asks for blocked or unrelated data."",
        ""visualizationType"": null,
        ""resultType"": null,
        ""sql"": null,
        ""explanation"": ""Blocked request"",
        ""assumptions"": []
    }}";

            string geminiApiKey = GetGeminiApiKey();
            string geminiModel = GetGeminiModel();
            string? fallbackModel = GetGeminiFallbackModel();
            string rawAiText;

            try
            {
                rawAiText = await CallGeminiWithRetriesAsync(systemInstruction, userPrompt, geminiModel, geminiApiKey);
            }
            catch (AiProviderException ex) when (ex.IsTemporary && !string.IsNullOrWhiteSpace(fallbackModel) && !fallbackModel.Equals(geminiModel, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Gemini primary model failed temporarily; using fallback model {fallbackModel}.");
                Debug.WriteLine($"Gemini primary model failed temporarily; using fallback model {fallbackModel}.");
                rawAiText = await CallGeminiWithRetriesAsync(systemInstruction, userPrompt, fallbackModel, geminiApiKey);
            }

            // חילוץ ה-JSON למקרה שגוגל החזיר תגיות עיטוף
            if (rawAiText.Contains("{"))
            {
                rawAiText = rawAiText.Substring(rawAiText.IndexOf("{"));
                rawAiText = rawAiText.Substring(0, rawAiText.LastIndexOf("}") + 1);
            }

            AiChartResponse chartResult = JsonConvert.DeserializeObject<AiChartResponse>(rawAiText);
            chartResult.Sql = (chartResult.Sql ?? string.Empty).Trim();
            chartResult.VisualizationType = NormalizeVisualizationType(chartResult.VisualizationType);
            chartResult.ResultType = NormalizeResultType(chartResult.ResultType);
            return chartResult;
        }

        private static async Task<string> CallGeminiWithRetriesAsync(string systemInstruction, string userPrompt, string model, string apiKey)
        {
            int[] retryDelaysMs = { 0, 800, 1800 };
            AiProviderException? lastTemporaryError = null;

            for (int attempt = 1; attempt <= retryDelaysMs.Length; attempt++)
            {
                if (retryDelaysMs[attempt - 1] > 0)
                {
                    await Task.Delay(retryDelaysMs[attempt - 1]);
                }

                try
                {
                    Console.WriteLine($"Calling Gemini model {model}, attempt {attempt}.");
                    Debug.WriteLine($"Calling Gemini model {model}, attempt {attempt}.");
                    return await CallGeminiOnceAsync(systemInstruction, userPrompt, model, apiKey);
                }
                catch (AiProviderException ex) when (ex.IsTemporary)
                {
                    lastTemporaryError = ex;
                    Console.WriteLine($"Temporary Gemini failure for model {model}, attempt {attempt}, code {ex.ErrorCode}, status {ex.StatusCode?.ToString() ?? "n/a"}.");
                    Debug.WriteLine($"Temporary Gemini failure for model {model}, attempt {attempt}, code {ex.ErrorCode}, status {ex.StatusCode?.ToString() ?? "n/a"}.");

                    if (attempt == retryDelaysMs.Length)
                    {
                        throw;
                    }
                }
            }

            throw lastTemporaryError ?? new AiProviderException("AI_PROVIDER_ERROR", "שירות ה-AI אינו זמין כרגע. נסה שוב מאוחר יותר.");
        }

        private static async Task<string> CallGeminiOnceAsync(string systemInstruction, string userPrompt, string model, string apiKey)
        {
            using (var client = new HttpClient())
            {
                string url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";

                string cleanInstruction = systemInstruction.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
                string cleanPrompt = userPrompt.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");

                string jsonPayload = "{" +
                    "\"contents\": [{" +
                        "\"parts\": [{" +
                            "\"text\": \"" + cleanInstruction + "\\n\\nUser Request: " + cleanPrompt + "\\n\\nReminder: Output ONLY the raw JSON object.\"" +
                        "}]" +
                    "}]" +
                "}";

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(url, content);
                }
                catch (TaskCanceledException ex)
                {
                    LogGeminiApiFailure($"Gemini API timeout for model {model}", ex.Message);
                    throw new AiProviderException("AI_TEMPORARILY_UNAVAILABLE", "המודל עמוס זמנית. נסה שוב בעוד כמה רגעים.", true);
                }

                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw BuildGeminiApiException((int)response.StatusCode, responseString);
                }

                dynamic geminiResponse = JsonConvert.DeserializeObject(responseString);
                return geminiResponse.candidates[0].content.parts[0].text.ToString().Trim();
            }
        }

        private static AiProviderException BuildGeminiApiException(int statusCode, string responseString)
        {
            LogGeminiApiFailure($"Gemini API HTTP {statusCode}", responseString);

            if (statusCode == 429 || ContainsAny(responseString, "RESOURCE_EXHAUSTED", "rate limit", "quota"))
            {
                return new AiProviderException("AI_RATE_LIMITED", "המודל קיבל יותר מדי בקשות כרגע. נסה שוב בעוד כמה רגעים.", true, statusCode);
            }

            if (statusCode == 503 || ContainsAny(responseString, "UNAVAILABLE", "high demand", "overloaded", "temporarily unavailable"))
            {
                return new AiProviderException("AI_TEMPORARILY_UNAVAILABLE", "המודל עמוס זמנית. נסה שוב בעוד כמה רגעים.", true, statusCode);
            }

            return new AiProviderException("AI_PROVIDER_ERROR", "שירות ה-AI אינו זמין כרגע. נסה שוב מאוחר יותר.", false, statusCode);
        }

        private static void LogGeminiApiFailure(string summary, string detail)
        {
            string message = $"{summary}: {detail}";
            Debug.WriteLine(message);
            Console.WriteLine(message);
        }

        private static string GetGeminiApiKey()
        {
            string? envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                return envKey.Trim();
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string? configKey = configuration["Gemini:ApiKey"];
            if (!string.IsNullOrWhiteSpace(configKey))
            {
                return configKey.Trim();
            }

            throw new Exception("Gemini API key is missing from configuration.");
        }

        private static string GetGeminiModel()
        {
            string? envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
            if (!string.IsNullOrWhiteSpace(envModel))
            {
                return envModel.Trim();
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string? configModel = configuration["Gemini:Model"];
            if (!string.IsNullOrWhiteSpace(configModel))
            {
                return configModel.Trim();
            }

            return "gemini-1.5-flash-latest";
        }

        private static string? GetGeminiFallbackModel()
        {
            string? envModel = Environment.GetEnvironmentVariable("GEMINI_FALLBACK_MODEL");
            if (!string.IsNullOrWhiteSpace(envModel))
            {
                return envModel.Trim();
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string? configModel = configuration["Gemini:FallbackModel"];
            if (!string.IsNullOrWhiteSpace(configModel))
            {
                return configModel.Trim();
            }

            return null;
        }

        private static bool LooksLikeSql(string input)
        {
            string normalized = (input ?? string.Empty).TrimStart();
            return normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeSqlIdentifierBindingError(string message)
        {
            string msg = (message ?? string.Empty).ToLowerInvariant();
            return msg.Contains("could not be bound")
                || msg.Contains("invalid column name")
                || msg.Contains("multi-part identifier");
        }

        private static string BuildSqlRepairPrompt(string originalPrompt, string failedSql, string sqlError)
        {
            return $@"User asked: {originalPrompt}

Previous SQL failed with SQL Server error:
{sqlError}

Failed SQL:
{failedSql}

Generate a corrected SQL query for the same intent.
Fix alias binding/scope issues and invalid column references.
Return JSON only as specified.";
        }

        private static string BuildDashboardScopeInstruction(string? dashboardType)
        {
            string scope = (dashboardType ?? string.Empty).Trim();
            if (scope.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
            {
                return "Monthly dashboard: prioritize production and monthly operational insights (ItemsInProduction, ProductionItemStage, ProductionStatuses, WorkOrders, Projects, Planes).";
            }

            if (scope.Equals("Inventory", StringComparison.OrdinalIgnoreCase))
            {
                return "Inventory dashboard: prioritize inventory and procurement insights (InventoryItems, Suppliers, Groups, BOM, ItemPlatforms, PlaneTypes).";
            }

            return "General dashboard scope: production and inventory analytics only.";
        }

        public SqlValidationResult ValidateSql(string sql, string? visualizationType, string? resultType)
        {
            string normalizedSql = (sql ?? string.Empty).Trim();
            if (normalizedSql.Length == 0)
            {
                return SqlValidationResult.Fail("EMPTY_SQL", "השאילתה שהתקבלה ריקה.");
            }

            if (normalizedSql.Contains(";"))
                return SqlValidationResult.Fail("SEMICOLON_BLOCKED", "השאילתה נחסמה: תו ';' אינו מותר.");
            if (normalizedSql.Contains("--") || normalizedSql.Contains("/*") || normalizedSql.Contains("*/"))
                return SqlValidationResult.Fail("COMMENTS_BLOCKED", "השאילתה נחסמה: הערות SQL אינן מותרות.");

            string trimmed = normalizedSql.TrimStart();
            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return SqlValidationResult.Fail("NOT_SELECT", "השאילתה נחסמה: מותרות רק שאילתות SELECT.");

            foreach (string pattern in BlockedKeywordPatterns)
            {
                if (Regex.IsMatch(normalizedSql, pattern, RegexOptions.IgnoreCase))
                {
                    return SqlValidationResult.Fail("BLOCKED_KEYWORD", "השאילתה נחסמה: זוהתה מילת מפתח אסורה.");
                }
            }

            if (Regex.IsMatch(normalizedSql, @"\bSELECT\s+\*", RegexOptions.IgnoreCase)
                || Regex.IsMatch(normalizedSql, @",\s*\*", RegexOptions.IgnoreCase))
            {
                return SqlValidationResult.Fail("SELECT_STAR_BLOCKED", "השאילתה נחסמה: שימוש ב-SELECT * אינו מותר.");
            }

            SqlValidationResult rowLimitValidation = ValidateRowLimit(normalizedSql);
            if (!rowLimitValidation.IsValid)
            {
                return rowLimitValidation;
            }

            List<string> referencedTables = ExtractReferencedTables(normalizedSql);
            if (referencedTables.Count == 0)
            {
                return SqlValidationResult.Fail("NO_TABLE_REFERENCE", "השאילתה נחסמה: לא זוהו טבלאות מקור תקינות.");
            }

            foreach (string table in referencedTables)
            {
                if (BlockedTables.Contains(table))
                    return SqlValidationResult.Fail("BLOCKED_TABLE", $"השאילתה נחסמה: הטבלה {table} חסומה לגישה.");
                if (!AllowedTables.Contains(table))
                    return SqlValidationResult.Fail("TABLE_OUTSIDE_DOMAIN", $"השאילתה נחסמה: הטבלה {table} אינה מאושרת לניתוח דשבורד.");
            }

            return SqlValidationResult.Ok(normalizedSql);
        }

        private static SqlValidationResult ValidateRowLimit(string sql)
        {
            Match topWithParens = Regex.Match(sql, @"\bTOP\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (topWithParens.Success)
            {
                return ValidateRowLimitValue(topWithParens.Groups[1].Value);
            }

            Match topWithoutParens = Regex.Match(sql, @"\bTOP\s+(\d+)\b", RegexOptions.IgnoreCase);
            if (topWithoutParens.Success)
            {
                return ValidateRowLimitValue(topWithoutParens.Groups[1].Value);
            }

            Match fetchNext = Regex.Match(sql, @"\bFETCH\s+NEXT\s+(\d+)\s+ROWS?\b", RegexOptions.IgnoreCase);
            if (fetchNext.Success)
            {
                return ValidateRowLimitValue(fetchNext.Groups[1].Value);
            }

            return SqlValidationResult.Fail("MISSING_ROW_LIMIT", "השאילתה נחסמה: חובה להוסיף TOP או OFFSET/FETCH עם הגבלת שורות.");
        }

        private static SqlValidationResult ValidateRowLimitValue(string value)
        {
            if (!int.TryParse(value, out int limit) || limit <= 0)
            {
                return SqlValidationResult.Fail("INVALID_ROW_LIMIT", "השאילתה נחסמה: הגבלת השורות אינה תקינה.");
            }

            if (limit > 200)
            {
                return SqlValidationResult.Fail("ROW_LIMIT_TOO_HIGH", "השאילתה נחסמה: מותר להחזיר עד 200 שורות בלבד.");
            }

            return SqlValidationResult.Ok(string.Empty);
        }

        private static List<string> ExtractReferencedTables(string sql)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MatchCollection matches = Regex.Matches(sql, @"\b(?:FROM|JOIN)\s+([\[\]A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (!match.Success || match.Groups.Count < 2) continue;
                string raw = match.Groups[1].Value.Trim();
                string cleaned = raw.Replace("[", string.Empty).Replace("]", string.Empty);
                string[] parts = cleaned.Split('.');
                string table = parts.LastOrDefault() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(table))
                {
                    tables.Add(table);
                }
            }
            return tables.ToList();
        }

        private static bool IsSingleSeries(string? resultType, string? visualizationType)
        {
            string normalizedResult = NormalizeResultType(resultType);
            string normalizedViz = NormalizeVisualizationType(visualizationType);
            if (normalizedResult == "single_series") return true;
            return normalizedViz == "bar" || normalizedViz == "line" || normalizedViz == "pie";
        }

        public SqlValidationResult ValidateResultShape(DataTable dt, string? visualizationType, string? resultType)
        {
            string normalizedViz = NormalizeVisualizationType(visualizationType);
            string normalizedResult = NormalizeResultType(resultType);

            if (normalizedResult == "table" || normalizedViz == "table")
            {
                if (dt.Columns.Count == 0)
                    return SqlValidationResult.Fail("EMPTY_RESULT", "השאילתה לא החזירה עמודות.");
                if (dt.Columns.Count > 20)
                    return SqlValidationResult.Fail("TOO_MANY_COLUMNS", "השאילתה נחסמה: עבור תצוגת טבלה מותרות עד 20 עמודות.");
                return SqlValidationResult.Ok(string.Empty);
            }

            if (normalizedResult == "multi_series")
            {
                if (dt.Columns.Count != 3)
                    return SqlValidationResult.Fail("INVALID_RESULT_SHAPE", "השאילתה נחסמה: עבור multi_series נדרשות 3 עמודות: Label, Series, Value.");
                if (!HasColumn(dt, "Label") || !HasColumn(dt, "Series") || !HasColumn(dt, "Value"))
                    return SqlValidationResult.Fail("INVALID_RESULT_COLUMNS", "השאילתה נחסמה: נדרשות עמודות בשם Label, Series, Value.");
                if (!IsNumericColumn(dt.Columns[GetColumnOrdinal(dt, "Value")]))
                    return SqlValidationResult.Fail("INVALID_VALUE_TYPE", "השאילתה נחסמה: העמודה Value חייבת להיות מספרית.");
                return SqlValidationResult.Ok(string.Empty);
            }

            if (dt.Columns.Count != 2)
                return SqlValidationResult.Fail("INVALID_RESULT_SHAPE", "השאילתה נחסמה: עבור bar/line/pie נדרשות 2 עמודות: Label, Value.");
            if (!HasColumn(dt, "Label") || !HasColumn(dt, "Value"))
                return SqlValidationResult.Fail("INVALID_RESULT_COLUMNS", "השאילתה נחסמה: נדרשות עמודות בשם Label ו-Value.");
            if (!IsNumericColumn(dt.Columns[GetColumnOrdinal(dt, "Value")]))
                return SqlValidationResult.Fail("INVALID_VALUE_TYPE", "השאילתה נחסמה: העמודה Value חייבת להיות מספרית.");

            return SqlValidationResult.Ok(string.Empty);
        }

        private static bool HasColumn(DataTable dt, string columnName)
        {
            return dt.Columns.Cast<DataColumn>().Any(c => string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetColumnOrdinal(DataTable dt, string columnName)
        {
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (string.Equals(dt.Columns[i].ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static bool IsNumericColumn(DataColumn col)
        {
            Type type = col.DataType;
            return type == typeof(byte) || type == typeof(short) || type == typeof(int)
                || type == typeof(long) || type == typeof(float) || type == typeof(double)
                || type == typeof(decimal);
        }

        private static string NormalizeVisualizationType(string? visualizationType)
        {
            string viz = (visualizationType ?? string.Empty).Trim().ToLowerInvariant();
            if (viz == "stacked_bar") return "bar";
            return viz switch
            {
                "bar" => "bar",
                "line" => "line",
                "pie" => "pie",
                "table" => "table",
                _ => "bar"
            };
        }

        private static string NormalizeResultType(string? resultType)
        {
            string rt = (resultType ?? string.Empty).Trim().ToLowerInvariant();
            return rt switch
            {
                "single_series" => "single_series",
                "multi_series" => "multi_series",
                "table" => "table",
                _ => "single_series"
            };
        }

        private static bool IsBlockedPromptTopic(string prompt)
        {
            string p = (prompt ?? string.Empty).Trim().ToLowerInvariant();
            if (p.Length == 0)
            {
                return false;
            }

            return BlockedPromptTerms.Any(term => p.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class AiChartResponse
    {
        [JsonProperty("isAllowed")]
        public bool? IsAllowed { get; set; }

        [JsonProperty("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("sql")]
        public string Sql { get; set; } = string.Empty;

        [JsonProperty("visualizationType")]
        public string VisualizationType { get; set; } = "bar";

        [JsonProperty("resultType")]
        public string ResultType { get; set; } = "single_series";

        [JsonProperty("explanation")]
        public string? Explanation { get; set; }

        [JsonProperty("assumptions")]
        public List<string>? Assumptions { get; set; }

        [JsonProperty("SqlQuery")]
        private string LegacySqlQuery { set { if (string.IsNullOrWhiteSpace(Sql)) Sql = value ?? string.Empty; } }

        [JsonProperty("ChartType")]
        private string LegacyChartType { set { if (string.IsNullOrWhiteSpace(VisualizationType)) VisualizationType = value ?? "bar"; } }
    }

    public class AiChartDataResponse
    {
        public string ChartTitle { get; set; } = string.Empty;
        public string ChartType { get; set; } = string.Empty;
        public List<ChartDataPoint> DataPoints { get; set; } = new List<ChartDataPoint>();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class SqlValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string NormalizedSql { get; set; } = string.Empty;

        public static SqlValidationResult Ok(string normalizedSql)
        {
            return new SqlValidationResult { IsValid = true, NormalizedSql = normalizedSql };
        }

        public static SqlValidationResult Fail(string code, string message)
        {
            return new SqlValidationResult { IsValid = false, ErrorCode = code, ErrorMessage = message };
        }
    }

    public class DashboardQueryExecutionResult
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<double> Values { get; set; } = new List<double>();
        public List<Dictionary<string, object?>> Rows { get; set; } = new List<Dictionary<string, object?>>();
        public string SqlQuery { get; set; } = string.Empty;
        public string VisualizationType { get; set; } = "bar";
        public string ResultType { get; set; } = "single_series";
        public string Explanation { get; set; } = string.Empty;
        public List<string> Assumptions { get; set; } = new List<string>();
    }

    public class DashboardGenerateResult
    {
        public bool IsValid { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DashboardQueryExecutionResult? Data { get; set; }

        public static DashboardGenerateResult Ok(DashboardQueryExecutionResult data)
        {
            return new DashboardGenerateResult { IsValid = true, Data = data };
        }

        public static DashboardGenerateResult Fail(string errorCode, string errorMessage)
        {
            return new DashboardGenerateResult
            {
                IsValid = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }

    public class AiProviderException : Exception
    {
        public string ErrorCode { get; }
        public string UserMessage { get; }
        public bool IsTemporary { get; }
        public int? StatusCode { get; }

        public AiProviderException(string errorCode, string userMessage, bool isTemporary = false, int? statusCode = null) : base(userMessage)
        {
            ErrorCode = errorCode;
            UserMessage = userMessage;
            IsTemporary = isTemporary;
            StatusCode = statusCode;
        }
    }

    public class SemanticContextSelection
    {
        public List<string> SectionNames { get; set; } = new List<string>();
        public string ContextText { get; set; } = string.Empty;
    }

    public class DashboardLayoutItem
    {
        public int ChartID { get; set; }
        public int DisplayOrder { get; set; }
        public string LayoutSize { get; set; } = "small";
        public int GridX { get; set; }
        public int GridY { get; set; }
    }

}
