using Server.DAL;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Server.Models
{
    public class UserAccount
    {
        public LoginResponse Login(LoginRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Username and password are required"
                };
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByUsername(request.Username.Trim());

            if (user == null)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }

            if (!user.IsActive)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "User is not active"
                };
            }

            string incomingHash = HashPassword(request.Password);
            if (!string.Equals(incomingHash, user.PasswordHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }

            return new LoginResponse
            {
                Success = true,
                Message = "Login successful",
                UserID = user.UserID,
                Username = user.Username,
                FullName = user.FullName,
                MustChangePassword = user.MustChangePassword,
                CanViewProduction = user.CanViewProduction,
                CanViewStock = user.CanViewStock,
                CanManageUsers = user.CanManageUsers
            };
        }

        public CreateUserResult CreateUser(CreateUserRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.FullName))
            {
                return new CreateUserResult
                {
                    Success = false,
                    Message = "Username and full name are required"
                };
            }

            DBservices dbs = new DBservices();
            AppUser? existing = dbs.GetUserByUsername(request.Username.Trim());
            if (existing != null)
            {
                return new CreateUserResult
                {
                    Success = false,
                    Message = "Username already exists"
                };
            }

            string temporaryPassword = GenerateTemporaryPassword();
            string passwordHash = HashPassword(temporaryPassword);

            AppUser user = new AppUser
            {
                Username = request.Username.Trim(),
                PasswordHash = passwordHash,
                FullName = request.FullName.Trim(),
                IsActive = request.IsActive,
                MustChangePassword = true,
                CanViewProduction = request.CanViewProduction,
                CanViewStock = request.CanViewStock,
                CanManageUsers = request.CanManageUsers,
                CreatedByUserID = request.CreatedByUserID
            };

            int newUserID = dbs.InsertUser(user);

            return new CreateUserResult
            {
                Success = newUserID > 0,
                Message = newUserID > 0 ? "User created successfully" : "User creation failed",
                UserID = newUserID,
                TemporaryPassword = temporaryPassword
            };
        }

        public List<AppUser> GetAllUsers()
        {
            DBservices dbs = new DBservices();
            return dbs.GetAllUsers();
        }

        public AppUser? GetUserByID(int userID)
        {
            DBservices dbs = new DBservices();
            return dbs.GetUserByID(userID);
        }

        public bool UpdateUser(UpdateUserRequest? request)
        {
            if (request == null || request.UserID <= 0 || string.IsNullOrWhiteSpace(request.FullName))
            {
                return false;
            }

            AppUser user = new AppUser
            {
                UserID = request.UserID,
                FullName = request.FullName.Trim(),
                IsActive = request.IsActive,
                CanViewProduction = request.CanViewProduction,
                CanViewStock = request.CanViewStock,
                CanManageUsers = request.CanManageUsers
            };

            DBservices dbs = new DBservices();
            return dbs.UpdateUserDetails(user) > 0;
        }

        public bool UpdateUserAccess(UpdateUserAccessRequest? request)
        {
            if (request == null)
            {
                return false;
            }

            AppUser user = new AppUser
            {
                UserID = request.UserID,
                IsActive = request.IsActive,
                CanViewProduction = request.CanViewProduction,
                CanViewStock = request.CanViewStock,
                CanManageUsers = request.CanManageUsers
            };

            DBservices dbs = new DBservices();
            return dbs.UpdateUserAccess(user) > 0;
        }

        public ResetPasswordResult ResetPassword(ResetPasswordRequest? request)
        {
            if (request == null)
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "Request is required"
                };
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByID(request.UserID);
            if (user == null)
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            string temporaryPassword = GenerateTemporaryPassword();
            string passwordHash = HashPassword(temporaryPassword);
            int rows = dbs.UpdateUserPassword(request.UserID, passwordHash, true);

            return new ResetPasswordResult
            {
                Success = rows > 0,
                Message = rows > 0 ? "Password reset successfully" : "Password reset failed",
                TemporaryPassword = temporaryPassword
            };
        }

        public ChangePasswordResult ChangePassword(ChangePasswordRequest? request)
        {
            if (request == null || request.UserID <= 0 || string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return new ChangePasswordResult
                {
                    Success = false,
                    Message = "User, current password and new password are required"
                };
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByID(request.UserID);
            if (user == null)
            {
                return new ChangePasswordResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            string currentHash = HashPassword(request.CurrentPassword);
            if (!string.Equals(currentHash, user.PasswordHash, StringComparison.OrdinalIgnoreCase))
            {
                return new ChangePasswordResult
                {
                    Success = false,
                    Message = "Current password is incorrect"
                };
            }

            string newHash = HashPassword(request.NewPassword);
            int rows = dbs.UpdateUserPassword(request.UserID, newHash, false);

            return new ChangePasswordResult
            {
                Success = rows > 0,
                Message = rows > 0 ? "Password changed successfully" : "Password change failed"
            };
        }

        public ActionResultData DeleteUser(DeleteUserRequest? request)
        {
            if (request == null || request.UserID <= 0)
            {
                return new ActionResultData
                {
                    Success = false,
                    Message = "User is required"
                };
            }

            if (request.CurrentUserID > 0 && request.CurrentUserID == request.UserID)
            {
                return new ActionResultData
                {
                    Success = false,
                    Message = "Cannot delete current logged-in user"
                };
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByID(request.UserID);
            if (user == null)
            {
                return new ActionResultData
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            int rows = dbs.DeleteUser(request.UserID);
            return new ActionResultData
            {
                Success = rows > 0,
                Message = rows > 0 ? "User deleted successfully" : "User delete failed"
            };
        }

        private static string HashPassword(string password)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes);
        }

        private static string GenerateTemporaryPassword(int length = 10)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            byte[] randomBytes = new byte[length];
            RandomNumberGenerator.Fill(randomBytes);

            char[] passwordChars = new char[length];
            for (int i = 0; i < length; i++)
            {
                passwordChars[i] = alphabet[randomBytes[i] % alphabet.Length];
            }

            return new string(passwordChars);
        }
    }

    public class AppUser
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public bool CanViewProduction { get; set; }
        public bool CanViewStock { get; set; }
        public bool CanManageUsers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? CreatedByUserID { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public bool CanViewProduction { get; set; }
        public bool CanViewStock { get; set; }
        public bool CanManageUsers { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool CanViewProduction { get; set; }
        public bool CanViewStock { get; set; }
        public bool CanManageUsers { get; set; }
        public int? CreatedByUserID { get; set; }
    }

    public class UpdateUserRequest
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanViewProduction { get; set; }
        public bool CanViewStock { get; set; }
        public bool CanManageUsers { get; set; }
    }

    public class CreateUserResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class UpdateUserAccessRequest
    {
        public int UserID { get; set; }
        public bool IsActive { get; set; }
        public bool CanViewProduction { get; set; }
        public bool CanViewStock { get; set; }
        public bool CanManageUsers { get; set; }
    }

    public class ResetPasswordRequest
    {
        public int UserID { get; set; }
    }

    public class ResetPasswordResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public int UserID { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DeleteUserRequest
    {
        public int UserID { get; set; }
        public int CurrentUserID { get; set; }
    }

    public class ActionResultData
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
