using Granary.Api.Models;
using Granary.Api.Models.Dto.Languages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LanguagesController : GranaryBaseController
    {
        private readonly GranaryDbContext _context;

        public LanguagesController(GranaryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LanguageDto>>> GetLanguages()
        {
            var langs = await _context.Languages
                .AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.DisplayOrder)
                .Select(l => new LanguageDto(l.Id, l.Code, l.Name, l.FlagEmoji, l.IsDefault))
                .ToListAsync();

            return Ok(langs);
        }
    }
}