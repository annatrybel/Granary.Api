namespace Granary.Api.Models.Dto.Recipes
{
    public class VoteRecipeRequest
    {
        public bool IsLike { get; set; }
    }

    public class VoteResultDto
    {
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public bool? UserVote { get; set; } 
    }
}