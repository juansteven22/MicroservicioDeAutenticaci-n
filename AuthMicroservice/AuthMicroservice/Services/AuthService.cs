using AuthMicroservice.Data;
using AuthMicroservice.DTOs;
using AuthMicroservice.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AuthMicroservice.Services
{
    public class AuthService : IAuthService
    {
        private readonly AuthDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthService(AuthDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto model)
        {
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                throw new Exception("Username is already taken");

            var user = new User
            {
                Username = model.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)

            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _tokenService.CreateToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                AccessToken = token,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                throw new Exception("Invalid username or password");

            var token = _tokenService.CreateToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                AccessToken = token,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens.Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (token == null || token.IsRevoked || token.ExpiryDate < DateTime.UtcNow)
                throw new Exception("Invalid refresh token");

            var newToken = _tokenService.CreateToken(token.User);
            var newRefreshToken = _tokenService.GenerateRefreshToken();
            token.User.RefreshTokens.Add(newRefreshToken);
            token.IsRevoked = true;
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                AccessToken = newToken,
                RefreshToken = newRefreshToken.Token
            };
        }

        public async Task RevokeTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (token == null)
                throw new Exception("Token not found");

            token.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }
}