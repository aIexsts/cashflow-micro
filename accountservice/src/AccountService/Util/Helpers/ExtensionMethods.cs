using AccountService.Dtos;
using AccountService.Models;
using Microsoft.AspNetCore.Http;

namespace AccountService.Util.Helpers
{
    public static class ExtensionMethods
    {
        public static User WithoutPassword(this User user) 
        {
            if (user == null) return null;

            user.Password = null;
            return user;
        }
        
        public static void AppendAuthCookies(this HttpResponse response, User user, string token) 
        {
            if (response == null) return;
            response.Cookies.Append("X-Access-Token", token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });
            response.Cookies.Append("X-Username", user.UserName, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });
            response.Cookies.Append("X-Refresh-Token", user.RefreshToken, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });
        }
        
        public static UserReadDto ToPublicDto(this User user) 
        {
            if (user == null) return null;
            
            return new UserReadDto
            {
                Email = user.Email,
                Username = user.UserName,
                Firstname = user.Firstname,
                Lastname = user.Lastname,
                Gender = (int) user.Gender,
                CreatedAt = user.CreatedAt,
                Id = user.PublicId
            };
        }
    }
}
