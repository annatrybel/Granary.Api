using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Users;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Granary.Api.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<ServiceResult<List<IdentityRole<Guid>>>> GetAllRolesAsync();
        Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync(PagedQueryDto query);
        Task<ServiceResult<UserDto>> GetUserById(string userId);
        Task<ServiceResult<UserDto>> CreateUser(UserDto user, ClaimsPrincipal currentUser);
        Task<ServiceResult> UpdateUser(UserDto model);
        Task<ServiceResult> LockUser(string userId);
        Task<ServiceResult> UnlockUser(string userId);
        Task<ServiceResult> ResendSetupLinkAsync(string userId);
    }
}