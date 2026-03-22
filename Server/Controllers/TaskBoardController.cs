using Microsoft.AspNetCore.Mvc;
using Server.Models; // ודאי שזה ה-Namespace שבו נמצא ה-TaskBoardRow
using Server.DAL;    // ודאי שזה ה-Namespace שבו נמצא ה-DBservices

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskBoardController : ControllerBase
    {
        // GET: api/TaskBoard
        // פונקציה שמחזירה את כל השורות המוכנות ללוח המשימות
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                DBservices dbs = new DBservices();
                // קריאה לפונקציה שבנינו בשלב 2 ב-DBservices
                List<TaskBoardRow> boardData = dbs.GetTaskBoardData();

                if (boardData == null || boardData.Count == 0)
                {
                    return NotFound("לא נמצאו נתונים להצגה בלוח המשימות");
                }

                return Ok(boardData); // מחזיר קוד 200 עם הנתונים בפורמט JSON
            }
            catch (Exception ex)
            {
                // במקרה של תקלה ב-SQL או בשרת, נחזיר שגיאה מפורטת
                return StatusCode(500, $"שגיאה פנימית בשרת: {ex.Message}");
            }
        }

        [HttpGet("stages")]
        public IEnumerable<ProductionStage> GetStages()
        {
            DBservices dbs = new DBservices();
            return dbs.GetProductionStages();
        }
    }
}