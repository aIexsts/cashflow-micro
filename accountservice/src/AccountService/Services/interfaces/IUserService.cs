using System.Threading.Tasks;
using AccountService.Dtos;
using AccountService.Models;

namespace AccountService.Services.interfaces
{
    public interface IUserService
    {
        Task<User> SignUp(UserSignUpDto userSignUpDto);
        Task<User> SignIn(UserSignInDto userSignInDto);
        Task<User> GetUserByUsernameAndRefreshToken(string userName, string refreshToken);
        Task UpdateRefreshToken(User user);
    }
}
