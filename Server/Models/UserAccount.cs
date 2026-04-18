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

        public bool CreateUser(AppUser? user, out string message, out int userID, out string temporaryPassword)
        {
            message = string.Empty;
            userID = 0;
            temporaryPassword = string.Empty;

            if (user == null || string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName))
            {
                message = "Username and full name are required";
                return false;
            }

            DBservices dbs = new DBservices();
            string username = user.Username.Trim();
            AppUser? existing = dbs.GetUserByUsername(username);
            if (existing != null)
            {
                message = "Username already exists";
                return false;
            }

            temporaryPassword = GenerateTemporaryPassword();
            string passwordHash = HashPassword(temporaryPassword);

            AppUser userToInsert = new AppUser
            {
                Username = username,
                PasswordHash = passwordHash,
                FullName = user.FullName.Trim(),
                IsActive = user.IsActive,
                MustChangePassword = true,
                CanViewProduction = user.CanViewProduction,
                CanViewStock = user.CanViewStock,
                CanManageUsers = user.CanManageUsers,
                CreatedByUserID = user.CreatedByUserID
            };

            userID = dbs.InsertUser(userToInsert);
            bool success = userID > 0;
            message = success ? "User created successfully" : "User creation failed";

            return success;
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

        public bool UpdateUser(AppUser? user)
        {
            if (user == null || user.UserID <= 0 || string.IsNullOrWhiteSpace(user.FullName))
            {
                return false;
            }

            AppUser userToUpdate = new AppUser
            {
                UserID = user.UserID,
                FullName = user.FullName.Trim(),
                IsActive = user.IsActive,
                CanViewProduction = user.CanViewProduction,
                CanViewStock = user.CanViewStock,
                CanManageUsers = user.CanManageUsers
            };

            DBservices dbs = new DBservices();
            return dbs.UpdateUserDetails(userToUpdate) > 0;
        }

        public bool UpdateUserAccess(AppUser? user)
        {
            if (user == null || user.UserID <= 0)
            {
                return false;
            }

            AppUser userAccess = new AppUser
            {
                UserID = user.UserID,
                IsActive = user.IsActive,
                CanViewProduction = user.CanViewProduction,
                CanViewStock = user.CanViewStock,
                CanManageUsers = user.CanManageUsers
            };

            DBservices dbs = new DBservices();
            return dbs.UpdateUserAccess(userAccess) > 0;
        }

        public bool ResetPassword(int userID, out string message, out string temporaryPassword)
        {
            message = string.Empty;
            temporaryPassword = string.Empty;

            if (userID <= 0)
            {
                message = "Request is required";
                return false;
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByID(userID);
            if (user == null)
            {
                message = "User not found";
                return false;
            }

            temporaryPassword = GenerateTemporaryPassword();
            string passwordHash = HashPassword(temporaryPassword);
            int rows = dbs.UpdateUserPassword(userID, passwordHash, true);
            bool success = rows > 0;
            message = success ? "Password reset successfully" : "Password reset failed";

            return success;
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

        public bool DeleteUser(int userID, int currentUserID, out string message)
        {
            message = string.Empty;

            if (userID <= 0)
            {
                message = "User is required";
                return false;
            }

            if (currentUserID > 0 && currentUserID == userID)
            {
                message = "Cannot delete current logged-in user";
                return false;
            }

            DBservices dbs = new DBservices();
            AppUser? user = dbs.GetUserByID(userID);
            if (user == null)
            {
                message = "User not found";
                return false;
            }

            int rows = dbs.DeleteUser(userID);
            bool success = rows > 0;
            message = success ? "User deleted successfully" : "User delete failed";

            return success;
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

}
