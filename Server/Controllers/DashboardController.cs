using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Threading.Tasks;
using Server.DAL;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DBservices _dbs = new DBservices();
        private readonly DashboardManager _manager = new DashboardManager();

        // 1. שליפת כל הגרפים הפעילים של המשתמש לפי סוג דשבורד
        // 1. שליפת כל הגרפים הפעילים לפי סוג דשבורד (לכלל המשתמשים)
        [HttpGet("get-charts")]
        public IActionResult GetCharts([FromQuery] string dashboardType)
        {
            try
            {
                // שינוי קריטי: קריאה לפונקציה החדשה שמביאה את הגרפים ללא סינון משתמש
                DataTable dt = _dbs.GetChartsByDashboardType(dashboardType);

                // הפיכת ה-DataTable לרשימה דינמית שנוח להעביר ב-JSON
                var chartsList = new System.Collections.Generic.List<object>();
                foreach (DataRow row in dt.Rows)
                {
                    chartsList.Add(new
                    {
                        ChartID = row["ChartID"],
                        ChartTitle = row["ChartTitle"],
                        ChartType = row["ChartType"],
                        SqlLogic = row["SqlLogic"],
                        UserID = row["UserID"] // שומרים את המידע של מי שיצר, אך מציגים לכולם
                    });
                }

                return Ok(chartsList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 2. יצירת גרף חדש בעזרת AI והרצת השאילתה לקבלת נתוני אמת
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateChart([FromBody] GenerateChartRequest request)
        {
            try
            {
                // 1. אם ה-Client שלח שאילתת SQL קיימת (מתוך גרף שמור)
                if (request.Prompt.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    DataTable dt = _dbs.ExecuteDynamicQuery(request.Prompt);

                    var labels = new System.Collections.Generic.List<string>();
                    var values = new System.Collections.Generic.List<double>();

                    // בדיקה אילו עמודות קיימות בתוצאה כדי למנוע קריסה
                    bool hasLabelColumn = dt.Columns.Contains("Label");
                    bool hasValueColumn = dt.Columns.Contains("Value");

                    foreach (DataRow row in dt.Rows)
                    {
                        // אם קיימת עמודה בשם Label משתמשים בה, אחרת לוקחים את העמודה הראשונה (אינדקס 0)
                        string labelText = hasLabelColumn ? row["Label"].ToString() : row[0].ToString();
                        labels.Add(labelText);

                        // אם קיימת עמודה בשם Value משתמשים בה, אחרת לוקחים את העמודה השנייה (אינדקס 1)
                        double valueNum = hasValueColumn ? Convert.ToDouble(row["Value"]) : Convert.ToDouble(row[1]);
                        values.Add(valueNum);
                    }

                    return Ok(new { labels, values });
                }

                // 2. אם המשתמש שלח פרומפט חופשי בעברית - ה-AI מייצר שאילתה חדשה
                AiChartResponse aiResponse = await _manager.GenerateChartFromPrompt(request.Prompt);

                // 3. מריצים את ה-SQL שה-AI ג'נרט
                DataTable dtAi = _dbs.ExecuteDynamicQuery(aiResponse.SqlQuery);

                var aiLabels = new System.Collections.Generic.List<string>();
                var aiValues = new System.Collections.Generic.List<double>();

                bool hasAiLabel = dtAi.Columns.Contains("Label");
                bool hasAiValue = dtAi.Columns.Contains("Value");

                foreach (DataRow row in dtAi.Rows)
                {
                    string labelText = hasAiLabel ? row["Label"].ToString() : row[0].ToString();
                    aiLabels.Add(labelText);

                    double valueNum = hasAiValue ? Convert.ToDouble(row["Value"]) : Convert.ToDouble(row[1]);
                    aiValues.Add(valueNum);
                }

                return Ok(new
                {
                    labels = aiLabels,
                    values = aiValues,
                    sqlQuery = aiResponse.SqlQuery,
                    chartType = aiResponse.ChartType
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 3. שמירת גרף מאושר לטבלת המשתמש
        [HttpPost("save")]
        public IActionResult SaveChart([FromBody] SaveChartRequest request)
        {
            try
            {
                int rowsAffected = _dbs.SaveUserDashboardChart(
                    request.ChartTitle,
                    request.DashboardType,
                    request.UserID,
                    request.ChartType,
                    request.SqlLogic
                );

                if (rowsAffected > 0 || rowsAffected == -1)
                    return Ok(new { success = true, message = "הגרף נשמר בהצלחה!" });

                return BadRequest(new { error = "שמירת הגרף נכשלה." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 4. מחיקת גרף מהדשבורד
        [HttpDelete("delete/{id}")]
        public IActionResult DeleteChart(int id)
        {
            try
            {
                int rowsAffected = _dbs.DeleteUserDashboardChart(id);
                if (rowsAffected > 0 || rowsAffected == -1)
                    return Ok(new { success = true, message = "הגרף נמחק בהצלחה!" });

                return BadRequest(new { error = "הגרף לא נמצא או שכבר נמחק" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("inventory-charts")]
        public IActionResult GetInventoryCharts()
        {
            try
            {
                // 1. שליפת כל הגרפים מטבחת הדשבורד ששייכים למסך המלאי (בלי לסנן לפי UserID ברמת השליפה למסך)
                // הערה: נניח שיש לך פונקציה שמביאה את הגרפים לפי סוג דשבורד
                DataTable dtCharts = _dbs.GetChartsByDashboardType("Inventory");

                List<AiChartDataResponse> responseList = new List<AiChartDataResponse>();

                foreach (DataRow row in dtCharts.Rows)
                {
                    var chart = new AiChartDataResponse
                    {
                        ChartTitle = row["ChartTitle"].ToString(),
                        ChartType = row["ChartType"].ToString().ToLower(), // 'bar', 'pie', 'line'
                        DataPoints = new List<ChartDataPoint>()
                    };

                    string sqlToExecute = row["SqlLogic"].ToString();

                    try
                    {
                        // 2. הרצת השאילתה הדינמית של הגרף כדי להביא את הנתונים האמיתיים שלו ברגע זה!
                        DataTable dtData = _dbs.ExecuteDynamicQuery(sqlToExecute);

                        foreach (DataRow dataRow in dtData.Rows)
                        {
                            chart.DataPoints.Add(new ChartDataPoint
                            {
                                // השדות שג'מיני מייצר תמיד נקראים Label ו-Value
                                Label = dataRow["Label"].ToString(),
                                Value = Convert.ToDouble(dataRow["Value"])
                            });
                        }
                    }
                    catch (Exception sqlEx)
                    {
                        // אם שאילתה ספציפית נכשלת, נוסיף נקודת שגיאה כדי שהגרף לא יפיל את כל הדף
                        chart.DataPoints.Add(new ChartDataPoint { Label = "שגיאה בטעינת נתונים", Value = 0 });
                    }

                    responseList.Add(chart);
                }

                return Ok(responseList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    // מחלקות עזר לקבלת הנתונים מה-Client ב-Request
    public class GenerateChartRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class SaveChartRequest
    {
        public string ChartTitle { get; set; } = string.Empty;
        public string DashboardType { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string ChartType { get; set; } = string.Empty;
        public string SqlLogic { get; set; } = string.Empty;
    }
}