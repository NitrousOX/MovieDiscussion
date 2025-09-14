// File: Models/DiscussionEntity.cs
using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class DiscussionEntity : TableEntity
    {
        public DiscussionEntity() { }

        public DiscussionEntity(string userId, string id)
        {
            PartitionKey = userId; // group by author
            RowKey = id;          // unique ID for discussion
        }

        public string MovieTitle { get; set; }
        public int ReleaseYear { get; set; }
        public string Genre { get; set; }
        public double IMDBRating { get; set; }
        public string Synopsis { get; set; }
        public int DurationMinutes { get; set; }
        public string CoverImageUrl { get; set; }
    }
}
