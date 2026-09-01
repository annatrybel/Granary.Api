using Granary.Api.Models.Dto.Auth;
using Granary.Api.Models.Dto.Users;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult> RegisterAsync(RegisterDto registerDto, bool isAdmin = false);
        Task<ServiceResult<LoginResponse>> Login(LoginDto loginDto);
        Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task<ServiceResult<LoginResponse>> HandleExternalLoginAsync();
        Task<ServiceResult<UserDto>> GetMe();
        Task<ServiceResult> UpdateProfileAsync(string userId, UpdateProfileDto dto);
        Task<ServiceResult> LogoutAsync();
    }
}