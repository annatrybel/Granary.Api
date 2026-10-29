using Granary.Api.Services.Responses;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Granary.Api.Controllers
{
    public abstract class GranaryBaseController : ControllerBase
    {
        protected GranaryBaseController() : base() { }

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

                return customStatusCode.HasValue
                    ? StatusCode(customStatusCode.Value, serviceResult.Data)
                    : Ok(serviceResult.Data);
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
