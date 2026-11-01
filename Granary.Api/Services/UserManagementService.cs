using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Users;
using Granary.Api.Services.Errors;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Granary.Api.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<UserManagementService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        private const string ObjectName = "User";

        public UserManagementService(
            GranaryDbContext context,
            ILogger<UserManagementService> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<ServiceResult<List<IdentityRole<Guid>>>> GetAllRolesAsync()
        {
            try
            {
                var roles = await _roleManager.Roles.AsNoTracking().ToListAsync();
                return ServiceResult<List<IdentityRole<Guid>>>.Success(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił błąd podczas pobierania ról.");
                return ServiceResult<List<IdentityRole<Guid>>>.Failure(CommonErrors.FetchFailed("Roles"));
            }
        }

        public async Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync(PagedQueryDto query)
        {
            try
            {
                var page = query.Page < 1 ? 1 : query.Page;
                var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

                var now = DateTimeOffset.UtcNow;

                var users =
                    from u in _context.Users.AsNoTracking()
                    join ur in _context.UserRoles on u.Id equals ur.UserId into userRoles
                    from ur in userRoles.DefaultIfEmpty()
                    join r in _context.Roles on ur.RoleId equals r.Id into roles
                    from r in roles.DefaultIfEmpty()
                    select new UserDto
                    {
                        UserId = u.Id.ToString(),
                        UserName = u.UserName ?? string.Empty,
                        Email = u.Email ?? string.Empty,
                        AvatarUrl = u.AvatarUrl,
                        RoleId = r == null ? string.Empty : r.Id.ToString(),
                        RoleName = r == null ? string.Empty : r.Name ?? string.Empty,
                        IsLocked = u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > now
                    };

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var pattern = $"%{EscapeLikePattern(query.Search.Trim())}%";
                    users = users.Where(u =>
                        EF.Functions.ILike(u.UserName, pattern, "\\") ||
                        EF.Functions.ILike(u.Email, pattern, "\\"));
                }

                var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
                var ordered = (query.SortBy?.Trim().ToLowerInvariant()) switch
                {
                    "email" => descending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
                    "rolename" or "role" => descending ? users.OrderByDescending(u => u.RoleName) : users.OrderBy(u => u.RoleName),
                    "islocked" => descending ? users.OrderByDescending(u => u.IsLocked) : users.OrderBy(u => u.IsLocked),
                    _ => descending ? users.OrderByDescending(u => u.UserName) : users.OrderBy(u => u.UserName)
                };

                var totalCount = await users.CountAsync();

                var pagedItems = await ordered
                    .ThenBy(u => u.UserId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return ServiceResult<PagedResult<UserDto>>.Success(new PagedResult<UserDto>
                {
                    Items = pagedItems,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,                 "Error fetching users with roles");
                return ServiceResult<PagedResult<UserDto>>.Failure(CommonErrors.DataProcessingError("Błąd pobierania listy użytkowników."));
            }
        }

                        private static string EscapeLikePattern(string value) =>
                            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

                        private static string GenerateRandomPassword()
                        {
                            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
                            const string lower = "abcdefghijkmnopqrstuvwxyz";
                            const string digits = "23456789";
                            const string symbols = "!@#$%^&*-_=+";
                            const string all = upper + lower + digits + symbols;

                            var chars = new List<char>
                            {
                                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
                            };

                            while (chars.Count < 32)
                            {
                                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
                            }

                            RandomNumberGenerator.Shuffle(CollectionsMarshal.AsSpan(chars));
                            return new string(chars.ToArray());
                        }

        public async Task<ServiceResult> LockUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ServiceResult.Failure(CommonErrors.NotFound(ObjectName, userId));

                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

                _logger.LogInformation("User {UserId} locked permanently", userId);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently locking user {UserId}", userId);
                return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, userId, "Błąd blokowania konta."));
            }
        }

        public async Task<ServiceResult> UnlockUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ServiceResult.Failure(CommonErrors.NotFound(ObjectName, userId));

                await _userManager.SetLockoutEndDateAsync(user, null);

                _logger.LogInformation("User {UserId} unlocked", userId);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", userId);
                return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, userId, "Błąd odblokowania konta."));
            }
        }

        public async Task<ServiceResult<UserDto>> CreateUser(UserDto user, ClaimsPrincipal currentUser)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return ServiceResult<UserDto>.Failure(AuthErrors.EmailRequired());
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(user.Email);
                if (existingUserByEmail != null)
                    return ServiceResult<UserDto>.Failure(AuthErrors.UserAlreadyExists());

                var role = await _context.Roles.FindAsync(Guid.Parse(user.RoleId));
                if (role == null || string.IsNullOrEmpty(role.Name))
                    return ServiceResult<UserDto>.Failure(UserManagementErrors.RoleNotFound(user.RoleId));

                if (role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentUser == null || !currentUser.IsInRole("Administrator"))
                    {
                        _logger.LogWarning("Użytkownik bez uprawnień próbował utworzyć admina.");
                        return ServiceResult<UserDto>.Failure(UserManagementErrors.CannotCreateAdminWithoutPrivileges());
                    }
                }

                var newUser = new ApplicationUser
                {
                    Email = user.Email,
                    UserName = user.UserName,
                    EmailConfirmed = true
                };

                // Konto dostaje losowe hasło, którego nikt nie zna; użytkownik ustawia własne przez link z e-maila.
                var createResult = await _userManager.CreateAsync(newUser, GenerateRandomPassword());
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("CreateAsync failed: {Errors}", errors);
                    return ServiceResult<UserDto>.Failure(UserManagementErrors.CreateFailed(errors));
                }

                var roleResult = await _userManager.AddToRoleAsync(newUser, role.Name);
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    return ServiceResult<UserDto>.Failure(UserManagementErrors.UpdateFailed("Nie udało się przypisać roli: " + errors));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(newUser);
                var setupUrl = PasswordLinkBuilder.BuildResetUrl(_configuration, token, newUser.Email!);
                var emailResult = await _emailService.SendAccountSetupAsync(newUser.Email!, newUser.UserName ?? newUser.Email!, setupUrl);
                if (!emailResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Nie wysłano linku do ustawienia hasła dla {Email}; konto nie zostało utworzone.", user.Email);
                    return ServiceResult<UserDto>.Failure(UserManagementErrors.CreateFailed(
                        "Nie udało się wysłać e-maila z linkiem do ustawienia hasła. Konto nie zostało utworzone."));
                }

                await transaction.CommitAsync();

                user.UserId = newUser.Id.ToString();
                return ServiceResult<UserDto>.Success(user);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating user {Email}", user.Email);
                return ServiceResult<UserDto>.Failure(CommonErrors.InternalServerError("Błąd zapisu nowego użytkownika."));
            }
        }

        public async Task<ServiceResult> UpdateUser(UserDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                return ServiceResult.Failure(AuthErrors.EmailRequired());
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                    return ServiceResult.Failure(CommonErrors.NotFound(ObjectName, model.UserId));

                var role = await _context.Roles.FindAsync(Guid.Parse(model.RoleId));
                if (role == null)
                    return ServiceResult.Failure(UserManagementErrors.RoleNotFound(model.RoleId));

                if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var existingUserWithNewEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUserWithNewEmail != null && existingUserWithNewEmail.Id != user.Id)
                    {
                        return ServiceResult.Failure(AuthErrors.UserAlreadyExists());
                    }
                    user.Email = model.Email;
                }

                user.UserName = model.UserName;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    return ServiceResult.Failure(UserManagementErrors.UpdateFailed(errors));
                }

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                await _userManager.AddToRoleAsync(user, role.Name!);

                await transaction.CommitAsync();
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating roles/user {UserId}", model.UserId);
                return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, model.UserId));
            }
        }

        public async Task<ServiceResult<UserDto>> GetUserById(string userId)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                var userData = await _context.Users
                     .Where(u => u.Id == userGuid)
                     .Join(_context.UserRoles,
                           u => u.Id,
                           ur => ur.UserId,
                           (u, ur) => new { User = u, UserRole = ur })
                     .Join(_context.Roles,
                           ur => ur.UserRole.RoleId,
                           r => r.Id,
                           (ur, r) => new
                           {
                               User = ur.User,
                               Role = r
                           })
                     .FirstOrDefaultAsync();

                if (userData == null)
                    return ServiceResult<UserDto>.Failure(CommonErrors.NotFound(ObjectName, userId));

                var userDto = new UserDto
                {
                    UserId = userData.User.Id.ToString(),
                    UserName = userData.User.UserName ?? string.Empty,
                    Email = userData.User.Email ?? string.Empty,
                    RoleId = userData.Role.Id.ToString(),
                    RoleName = userData.Role.Name ?? string.Empty
                };

                return ServiceResult<UserDto>.Success(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserById error {UserId}", userId);
                return ServiceResult<UserDto>.Failure(CommonErrors.FetchFailed(ObjectName));
            }
        }
    }
}