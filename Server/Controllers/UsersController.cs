using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            try
            {
                UserAccount model = new UserAccount();
                List<AppUser> users = model.GetAllUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetUserByID(int id)
        {
            try
            {
                UserAccount model = new UserAccount();
                AppUser? user = model.GetUserByID(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateUser([FromBody] CreateUserRequest? request)
        {
            try
            {
                UserAccount model = new UserAccount();
                CreateUserResult result = model.CreateUser(request);
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("access")]
        public IActionResult UpdateUserAccess([FromBody] UpdateUserAccessRequest? request)
        {
            try
            {
                UserAccount model = new UserAccount();
                bool updated = model.UpdateUserAccess(request);
                if (updated)
                {
                    return Ok(new { message = "User access updated successfully" });
                }

                return BadRequest(new { message = "User access update failed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] UpdateUserRequest? request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Invalid request" });
                }

                request.UserID = id;
                UserAccount model = new UserAccount();
                bool updated = model.UpdateUser(request);
                if (updated)
                {
                    return Ok(new { message = "User updated successfully" });
                }

                return BadRequest(new { message = "User update failed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest? request)
        {
            try
            {
                UserAccount model = new UserAccount();
                ResetPasswordResult result = model.ResetPassword(request);
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id, [FromQuery] int currentUserID = 0)
        {
            try
            {
                UserAccount model = new UserAccount();
                ActionResultData result = model.DeleteUser(new DeleteUserRequest
                {
                    UserID = id,
                    CurrentUserID = currentUserID
                });

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
