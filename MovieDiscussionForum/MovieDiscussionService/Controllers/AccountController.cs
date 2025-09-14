using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using MovieDiscussion.Common;
using MovieDiscussion.Common.Models;
using MovieDiscussionService.Models;
using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class AccountController : Controller
    {
        private const string BlobContainerName = "profileimages";

        // GET: Account/Login
        public ActionResult Login()
        {
            return View(new LoginViewModel());
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Get the Azure Table reference
            var table = await StorageHelper.GetTableReferenceAsync("Users");

            // Retrieve user by PartitionKey + RowKey (RowKey = email)
            var retrieveOperation = TableOperation.Retrieve<UserEntity>("User", model.Email);
            var result = await table.ExecuteAsync(retrieveOperation);
            var user = result.Result as UserEntity;

            if (user != null && user.Password == model.Password)
            {
                // Store user info in session
                Session["Username"] = user.FullName;
                Session["Email"] = user.RowKey;
                Session["ProfileImageUrl"] = user.ProfileImageUrl;
                Session["IsAuthor"] = user.IsAuthor;

                return RedirectToAction("Index", "Discussion");
            }

            ViewBag.Message = "Invalid email or password";
            return View(model);
        }

        // GET: Account/Register
        public ActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model, HttpPostedFileBase ProfileImage)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Get Azure Table reference
            var table = await StorageHelper.GetTableReferenceAsync("Users");

            // Check if user already exists
            var retrieveOperation = TableOperation.Retrieve<UserEntity>("User", model.Email);
            var existing = await table.ExecuteAsync(retrieveOperation);
            if (existing.Result != null)
            {
                ModelState.AddModelError("", "A user with this email already exists.");
                return View(model);
            }

            // Upload profile image to Azure Blob Storage
            string imageUrl = null;
            if (ProfileImage != null && ProfileImage.ContentLength > 0)
             {
                var container = await StorageHelper.GetBlobContainerReferenceAsync("profileimages");
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(ProfileImage.FileName);
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                await blob.UploadFromStreamAsync(ProfileImage.InputStream);
                imageUrl = blob.Uri.ToString();
            }

            // Create new user entity
            var userEntity = new UserEntity
            {
                PartitionKey = "User",
                RowKey = model.Email,
                Password = model.Password,
                FullName = model.FullName,
                Gender = model.Gender,
                Country = model.Country,
                City = model.City,
                Address = model.Address,
                ProfileImageUrl = imageUrl,
                IsAuthor = false
            };

            // Save user to Azure Table
            var insertOperation = TableOperation.Insert(userEntity);
            await table.ExecuteAsync(insertOperation);

            ViewBag.Message = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }


        // GET: Account/Edit
        public async Task<ActionResult> Edit()
        {
            // Get user email from session
            var email = Session["Email"] as string;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            // Load user from Azure Table
            var table = await StorageHelper.GetTableReferenceAsync("Users");
            var retrieveOperation = TableOperation.Retrieve<UserEntity>("User", email);
            var result = await table.ExecuteAsync(retrieveOperation);
            var user = result.Result as UserEntity;

            if (user == null)
                return RedirectToAction("Login");

            // Map user entity to EditProfileViewModel
            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                Gender = user.Gender,
                Country = user.Country,
                City = user.City,
                Address = user.Address,
                IsAuthor = user.IsAuthor,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return View(model);
        }

        // POST: Account/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(EditProfileViewModel model, HttpPostedFileBase ProfileImage)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Get user email from session
            var email = Session["Email"] as string;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            // Load user from Azure Table
            var table = await StorageHelper.GetTableReferenceAsync("Users");
            var retrieveOperation = TableOperation.Retrieve<UserEntity>("User", email);
            var result = await table.ExecuteAsync(retrieveOperation);
            var user = result.Result as UserEntity;

            if (user == null)
                return RedirectToAction("Login");

            // Update profile image if a new file is uploaded
            if (ProfileImage != null && ProfileImage.ContentLength > 0)
            {
                var container = await StorageHelper.GetBlobContainerReferenceAsync("profileimages");
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(ProfileImage.FileName);
                var blob = container.GetBlockBlobReference(fileName);
                await blob.UploadFromStreamAsync(ProfileImage.InputStream);

                user.ProfileImageUrl = blob.Uri.ToString();
            }

            // Update other fields
            user.FullName = model.FullName;
            user.Gender = model.Gender;
            user.Country = model.Country;
            user.City = model.City;
            user.Address = model.Address;
            user.IsAuthor = model.IsAuthor;

            // Save changes back to Azure Table
            var updateOperation = TableOperation.Replace(user);
            await table.ExecuteAsync(updateOperation);

            // Ensure the view model always has the current ProfileImageUrl
            model.ProfileImageUrl = user.ProfileImageUrl;

            ViewBag.Message = "Profile updated successfully!";
            return View(model);
        }


        // GET: Account/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
