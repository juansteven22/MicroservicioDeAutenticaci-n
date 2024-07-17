using AuthMicroservice.Models;

namespace AuthMicroservice.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
        RefreshToken GenerateRefreshToken();
    }
}