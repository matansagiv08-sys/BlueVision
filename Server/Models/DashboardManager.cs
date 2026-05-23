using System;
using System.Data;
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
        private readonly string _geminiApiKey = "AIzaSyA3zpzWNtlPn8H26bF_12qKtllW721Bhz4";

        public string GetDatabaseSchema()
        {
            try
            {
                string schemaQuery = @"
                    SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = 'dbo' 
                    AND TABLE_NAME IN ('InventoryItems', 'BOM', 'PlaneTypes', 'ItemsInProduction', 'Projects')";

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

        public async Task<AiChartResponse> GenerateChartFromPrompt(string userPrompt)
        {
            string dbSchema = GetDatabaseSchema();

            string systemInstruction = $@"
    You are an expert SQL Server DBA for the 'BlueVision' drone company.
    Based on this schema, generate a safe SELECT query to answer the user request.

    {dbSchema}

    RULES:
    1. Return ONLY a valid JSON object. No markdown, no ```json formatting.
    2. The query MUST return exactly two columns: 'Label' (string) and 'Value' (numeric).
    3. Choose chartType from: 'bar', 'pie', 'line'.
    4. Map requests for stock/inventory to 'InventoryItems' table, using Whse01_QTY, Whse03_QTY, or Price.

    Format:
    {{
        ""SqlQuery"": ""SELECT ItemName AS Label, Whse01_QTY AS Value FROM InventoryItems"",
        ""ChartType"": ""bar""
    }}";

            using (var client = new HttpClient())
            {
                // כתובת ה-API הרשמית והיציבה של גוגל
                string url = $"[https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key=](https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key=){_geminiApiKey}";

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
                return chartResult;
            }
        }
    }

    public class AiChartResponse
    {
        [JsonProperty("SqlQuery")]
        public string SqlQuery { get; set; } = string.Empty;

        [JsonProperty("ChartType")]
        public string ChartType { get; set; } = string.Empty;
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

}