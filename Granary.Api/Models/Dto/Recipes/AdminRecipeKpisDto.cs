namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeKpisDto
    {
        public int TotalRecipes { get; set; }
        public int OfficialRecipes { get; set; }
        public int CommunityRecipes { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int PendingVerification { get; set; }
        public int TotalCookedMeals { get; set; }
    }
}
