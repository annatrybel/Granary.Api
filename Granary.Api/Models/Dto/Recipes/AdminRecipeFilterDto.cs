using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeFilterDto
    {
        public string? Search { get; set; }
        [EnumDataType(typeof(DietaryPreference))]
        public DietaryPreference? Diet { get; set; }
        public string? Status { get; set; } 
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
