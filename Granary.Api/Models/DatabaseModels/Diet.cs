namespace Granary.Api.Models.DatabaseModels
{
    public class Diet
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public ICollection<DietTranslation> Translations { get; set; } = new List<DietTranslation>();
        public ICollection<RecipeDiet> RecipeDiets { get; set; } = new List<RecipeDiet>();
    }
}