using Granary.Api.Services.Responses;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;

namespace Granary.Api.Controllers
{
    public abstract class GranaryBaseController : ControllerBase
    {
        protected GranaryBaseController() : base() { }

        /// <summary>
        /// Pobiera surowy identyfikator użytkownika z claimsów JWT jako string.
        /// </summary>
        protected string? CurrentUserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>
        /// Pobiera pewny GUID zalogowanego użytkownika. 
        /// Rzuca wyjątek 401 (Unauthorized), gdyby token był nieprawidłowy.
        /// </summary>
        protected Guid GetCurrentUserId()
        {
            var userIdStr = CurrentUserId;

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                throw new UnauthorizedAccessException("Brak lub nieprawidłowy identyfikator użytkownika w tokenie JWT.");
            }

            return userId;
        }

        #region Strony Błędów / Redirecty

        protected IActionResult ErrorPage(string message = "", HttpStatusCode? statusCode = null)
        {
            return Redirect($"/Error?message={message}&statusCode={statusCode}");
        }

        protected IActionResult ErrorPage<T>(ServiceResult<T> serviceResult)
        {
            return Redirect($"/Error?message={serviceResult.Error.Description}&statusCode={serviceResult.StatusCode}");
        }

        protected IActionResult ErrorPage(ServiceResult serviceResult)
        {
            return Redirect($"/Error?message={serviceResult.Error.Description}&statusCode={serviceResult.StatusCode}");
        }

        #endregion

        #region Obsługa Wyników Serwisów (ServiceResult)

        protected IActionResult HandleServiceResult<T>(ServiceResult<T> serviceResult, int? customStatusCode = null)
        {
            if (serviceResult.IsSuccess)
            {
                if (typeof(T) == typeof(FileMemoryStreamResponse))
                {
                    var fileData = (FileMemoryStreamResponse)(object)serviceResult.Data!;
                    return File(fileData.MemoryStream, fileData.ContentType, fileData.FileDownloadName);
                }
                else if (typeof(T) == typeof(FileByteArrayResponse))
                {
                    var fileData = (FileByteArrayResponse)(object)serviceResult.Data!;
                    return File(fileData.Bytes, fileData.ContentType, fileData.FileDownloadName);
                }

                return customStatusCode.HasValue ? StatusCode(customStatusCode.Value, serviceResult.Data) : Ok(serviceResult.Data);
            }

            var errors = new Dictionary<string, string[]>() { { "Model", serviceResult.Error.Errors.ToArray() } };

            var vpd = new ValidationProblemDetails(errors)
            {
                Detail = serviceResult.Error.Description,
                Instance = $"{HttpContext.Request.Method} {HttpContext.Request.Path}",
                Type = $"Error {serviceResult.Error.Code}",
                Title = "Bad request",
                Status = (int)HttpStatusCode.BadRequest
            };
            vpd.Extensions.Add("traceId", HttpContext.TraceIdentifier);

            return ValidationProblem(vpd);
        }

        protected IActionResult HandleServiceResult(ServiceResult serviceResult, int? customStatusCode = null)
        {
            if (serviceResult.IsSuccess)
                return customStatusCode.HasValue ? StatusCode(customStatusCode.Value) : Ok();

            var errors = new Dictionary<string, string[]>() { { "Model", serviceResult.Error.Errors.ToArray() } };

            var vpd = new ValidationProblemDetails(errors)
            {
                Detail = serviceResult.Error.Description,
                Instance = $"{HttpContext.Request.Method} {HttpContext.Request.Path}",
                Type = $"Error {serviceResult.Error.Code}",
                Title = "Bad request",
                Status = (int)HttpStatusCode.BadRequest
            };
            vpd.Extensions.Add("traceId", HttpContext.TraceIdentifier);

            return ValidationProblem(vpd);
        }

        #endregion
    }
}
