using System.ComponentModel.DataAnnotations.Schema;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("UserRecipeInteractions")]
    public class UserRecipeInteraction
    {
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        public Guid RecipeId { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public Recipe Recipe { get; set; } = null!;
        public bool IsFavorite { get; set; } = false;
        public bool? Vote { get; set; }

        public DateTime? FavoritedAt { get; set; }
        public DateTime? VotedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}