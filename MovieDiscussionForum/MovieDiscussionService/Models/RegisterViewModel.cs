using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MovieDiscussionService.Models
{
    public class RegisterViewModel
    {
        public string FullName { get; set; }
        public string Gender { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}