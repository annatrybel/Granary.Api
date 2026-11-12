using Granary.Api.Models.Dto.Auth;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult> RegisterAsync(RegisterDto registerDto);
        Task<ServiceResult> ConfirmEmailAsync(ConfirmEmailDto confirmEmailDto);
        Task<ServiceResult<LoginResponse>> Login(LoginDto loginDto);
        Task<ServiceResult<LoginResponse>> AdminLogin(LoginDto loginDto);
        Task<ServiceResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequestDto dto);
        Task<ServiceResult> RevokeTokenAsync(RevokeTokenRequestDto dto);
        Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task<ServiceResult<LoginResponse>> HandleExternalLoginAsync();
        Task<ServiceResult> LogoutAsync();
    }
}