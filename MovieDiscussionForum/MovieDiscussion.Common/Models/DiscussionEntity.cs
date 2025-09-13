// File: Models/DiscussionEntity.cs
using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class DiscussionEntity : TableEntity
    {
        public DiscussionEntity() { }

        public DiscussionEntity(string genre, string id)
        {
            PartitionKey = genre; // group by genre
            RowKey = id;          // unique ID for discussion
        }

        public string MovieTitle { get; set; }
        public int Year { get; set; }
        public string ImdbRating { get; set; }
        public string Synopsis { get; set; }
        public string Duration { get; set; }
        public string PosterUrl { get; set; } // Blob storage path
        public double AverageUserRating { get; set; }
    }
}
