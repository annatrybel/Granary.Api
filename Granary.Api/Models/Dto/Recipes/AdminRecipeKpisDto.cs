namespace Granary.Api.Models.Dto.Recipes
{
    /// <summary>
    /// Zbiorcze wskaźniki efektywności (KPI) modułu dań w panelu administracyjnym.
    /// </summary>
    public class AdminRecipeKpisDto
    {
        public int OfficialRecipesCount { get; set; }

        public int CommunityRecipesCount { get; set; }

        public int TotalLikes { get; set; }

        public int TotalDislikes { get; set; }

        public double? ApprovalPercentage { get; set; }

        public int TotalFavoritesCount { get; set; }

        public int NeedsAttentionCount { get; set; }
    }
}
