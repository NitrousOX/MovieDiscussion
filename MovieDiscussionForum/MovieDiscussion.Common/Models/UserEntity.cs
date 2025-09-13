using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class UserEntity : TableEntity
    {
        public UserEntity() { }

        public UserEntity(string email)
        {
            PartitionKey = "User";
            RowKey = email; // unique per user
        }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Gender { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string PasswordHash { get; set; }
        public string ProfileImageUrl { get; set; } // Blob storage path
        public bool IsAuthor { get; set; }
    }
}
