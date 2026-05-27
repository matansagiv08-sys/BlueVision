using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardManager _manager = new DashboardManager();

        // 1. שליפת כל הגרפים הפעילים של המשתמש לפי סוג דשבורד
        // 1. שליפת כל הגרפים הפעילים לפי סוג דשבורד (לכלל המשתמשים)
        [HttpGet("get-charts")]
        public IActionResult GetCharts([FromQuery] string dashboardType)
        {
            try
            {
                // שינוי קריטי: קריאה לפונקציה החדשה שמביאה את הגרפים ללא סינון משתמש
                var chartsList = _manager.GetChartsByDashboardType(dashboardType);
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
                DashboardGenerateResult result = await _manager.GenerateChartAsync(
                    request.Prompt,
                    request.VisualizationType,
                    request.ResultType
                );

                if (!result.IsValid)
                {
                    return BadRequest(new
                    {
                        error = result.ErrorMessage,
                        errorCode = result.ErrorCode
                    });
                }

                return Ok(new
                {
                    labels = result.Data?.Labels,
                    values = result.Data?.Values,
                    rows = result.Data?.Rows,
                    sqlQuery = result.Data?.SqlQuery,
                    chartType = result.Data?.VisualizationType,
                    visualizationType = result.Data?.VisualizationType,
                    resultType = result.Data?.ResultType,
                    explanation = result.Data?.Explanation,
                    assumptions = result.Data?.Assumptions
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
                SqlValidationResult validation = _manager.ValidateSqlForSave(request.SqlLogic, request.ChartType);
                if (!validation.IsValid)
                {
                    return BadRequest(new
                    {
                        error = validation.ErrorMessage,
                        errorCode = validation.ErrorCode
                    });
                }

                int rowsAffected = _manager.SaveChart(
                    request.ChartTitle,
                    request.DashboardType,
                    request.UserID,
                    request.ChartType,
                    validation.NormalizedSql
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
                int rowsAffected = _manager.DeleteChart(id);
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
                var charts = _manager.GetChartsByDashboardType("Inventory");
                return Ok(charts);
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
        public string? VisualizationType { get; set; }
        public string? ResultType { get; set; }
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
