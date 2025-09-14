// File: Models/DiscussionViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace MovieDiscussionService.Models
{
    public class DiscussionViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "Movie title is required")]
        public string MovieTitle { get; set; }

        [Required(ErrorMessage = "Release year is required")]
        [Range(1900, 2100, ErrorMessage = "Enter a valid year")]
        public int ReleaseYear { get; set; }

        [Required(ErrorMessage = "Genre is required")]
        public string Genre { get; set; }

        [Required(ErrorMessage = "IMDB Rating is required")]
        [Range(0, 10, ErrorMessage = "Rating must be between 0 and 10")]
        public double IMDBRating { get; set; }

        [Required(ErrorMessage = "Duration is required")]
        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes")]
        public int DurationMinutes { get; set; }

        [Required(ErrorMessage = "Synopsis is required")]
        public string Synopsis { get; set; }

        public HttpPostedFileBase CoverImage { get; set; }
        public string CoverImageUrl { get; set; }
    }
}
