using Granary.Api.Models.Dto;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IHistoryLogService
    {
        Task<ServiceResult<DataTableResponse<HistoryLogDto>>> GetHistoryLogs(DataTableRequest request);
    }
}
