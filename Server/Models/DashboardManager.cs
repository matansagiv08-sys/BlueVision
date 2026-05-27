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
            "SystemSettings", "ExcelImportMetadata", "UserDashboards"
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
                    UserID = row["UserID"]
                });
            }
            return chartsList;
        }

        public int SaveChart(string chartTitle, string dashboardType, int userId, string chartType, string sqlLogic)
        {
            return _dbs.SaveUserDashboardChart(chartTitle, dashboardType, userId, chartType, sqlLogic);
        }

        public int DeleteChart(int chartId)
        {
            return _dbs.DeleteUserDashboardChart(chartId);
        }

        public async Task<DashboardGenerateResult> GenerateChartAsync(string prompt, string? visualizationType = null, string? resultType = null)
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
                aiResponse = await GenerateChartFromPrompt(safePrompt);
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

            DataTable dt = _dbs.ExecuteDynamicQuery(validation.NormalizedSql);

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

        public SqlValidationResult ValidateSqlForSave(string sqlLogic, string? chartType)
        {
            string viz = NormalizeVisualizationType(chartType);
            string resultType = viz == "table" ? "table" : "single_series";
            return ValidateSql(sqlLogic, viz, resultType);
        }

        public async Task<AiChartResponse> GenerateChartFromPrompt(string userPrompt)
        {
            string dbSchema = GetDatabaseSchema();

            string systemInstruction = $@"
    You are an expert SQL Server analyst for the 'BlueVision' drone company.
    Generate exactly one safe SQL Server SELECT query for dashboard analytics.

    {dbSchema}

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

            using (var client = new HttpClient())
            {
                string geminiApiKey = GetGeminiApiKey();
                string geminiModel = GetGeminiModel();
                // כתובת ה-API הרשמית והיציבה של גוגל
                string url = $"https://generativelanguage.googleapis.com/v1/models/{geminiModel}:generateContent?key={geminiApiKey}";

                // ניקוי תווים מיוחדים לפילוד
                string cleanInstruction = systemInstruction.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
                string cleanPrompt = userPrompt.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");

                // תיקון קריטי: פילוד נקי ללא generationConfig הבעייתי בדפדפן
                string jsonPayload = "{" +
                    "\"contents\": [{" +
                        "\"parts\": [{" +
                            "\"text\": \"" + cleanInstruction + "\\n\\nUser Request: " + cleanPrompt + "\\n\\nReminder: Output ONLY the raw JSON object.\"" +
                        "}]" +
                    "}]" +
                "}";

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Gemini API Error: {responseString}");
                }

                dynamic geminiResponse = JsonConvert.DeserializeObject(responseString);
                string rawAiText = geminiResponse.candidates[0].content.parts[0].text.ToString().Trim();

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

        private static bool LooksLikeSql(string input)
        {
            string normalized = (input ?? string.Empty).TrimStart();
            return normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
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

            if (!HasRowLimit(normalizedSql))
            {
                return SqlValidationResult.Fail("MISSING_ROW_LIMIT", "השאילתה נחסמה: חובה להוסיף TOP או OFFSET/FETCH עם הגבלת שורות.");
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

        private static bool HasRowLimit(string sql)
        {
            return Regex.IsMatch(sql, @"\bTOP\s*\(\s*\d+\s*\)", RegexOptions.IgnoreCase)
                || Regex.IsMatch(sql, @"\bTOP\s+\d+\b", RegexOptions.IgnoreCase)
                || (Regex.IsMatch(sql, @"\bOFFSET\b", RegexOptions.IgnoreCase)
                    && Regex.IsMatch(sql, @"\bFETCH\s+NEXT\b", RegexOptions.IgnoreCase));
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

}
