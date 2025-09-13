using System.ComponentModel.DataAnnotations;

namespace MovieDiscussionService.Models
{
    public class CommentModel
    {
        [Required]
        public string DiscussionId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required, StringLength(1000)]
        public string Text { get; set; }
    }
}
