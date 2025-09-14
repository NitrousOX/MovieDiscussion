using Microsoft.WindowsAzure.Storage.Table;
using MovieDiscussion.Common;
using MovieDiscussion.Common.Models;
using MovieDiscussionService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class DiscussionController : Controller
    {
        private const string DiscussionTableName = "Discussions";
        private const string BlobContainerName = "discussioncovers";

        // GET: Discussion
        public async Task<ActionResult> Index()
        {
            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);
            var query = new TableQuery<DiscussionEntity>();
            var result = new List<DiscussionEntity>();
            TableContinuationToken token = null;

            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                result.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null);

            return View(result);
        }

        // GET: Discussion/Create
        public ActionResult Create()
        {
            // Only allow authors
            if (Session["IsAuthor"] == null || !(bool)Session["IsAuthor"])
                return RedirectToAction("Index");

            return View(new DiscussionViewModel());
        }

        // POST: Discussion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(DiscussionViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string coverUrl = null;

            // Upload cover image
            if (model.CoverImage != null && model.CoverImage.ContentLength > 0)
            {
                var container = await StorageHelper.GetBlobContainerReferenceAsync(BlobContainerName);
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(model.CoverImage.FileName);
                var blob = container.GetBlockBlobReference(fileName);
                await blob.UploadFromStreamAsync(model.CoverImage.InputStream);
                coverUrl = blob.Uri.ToString();
            }

            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);

            var discussion = new DiscussionEntity(Session["Email"].ToString(), Guid.NewGuid().ToString())
            {
                MovieTitle = model.MovieTitle,
                ReleaseYear = model.ReleaseYear,
                Genre = model.Genre,
                IMDBRating = model.IMDBRating,
                DurationMinutes = model.DurationMinutes,
                Synopsis = model.Synopsis,
                CoverImageUrl = coverUrl
            };

            var insertOperation = TableOperation.Insert(discussion);
            await table.ExecuteAsync(insertOperation);

            return RedirectToAction("Index");
        }
        // GET: Discussion/Edit/{id}
        public async Task<ActionResult> Edit(string id)
        {
            if (Session["IsAuthor"] == null || !(bool)Session["IsAuthor"])
                return RedirectToAction("Index");

            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);
            var retrieveOperation = TableOperation.Retrieve<DiscussionEntity>(Session["Email"].ToString(), id);
            var result = await table.ExecuteAsync(retrieveOperation);
            var discussion = result.Result as DiscussionEntity;

            if (discussion == null)
                return RedirectToAction("Index");

            var model = new DiscussionViewModel
            {
                Id = discussion.RowKey,
                MovieTitle = discussion.MovieTitle,
                ReleaseYear = discussion.ReleaseYear,
                Genre = discussion.Genre,
                IMDBRating = discussion.IMDBRating,
                DurationMinutes = discussion.DurationMinutes,
                Synopsis = discussion.Synopsis,
                CoverImageUrl = discussion.CoverImageUrl
            };

            return View(model);
        }

        // POST: Discussion/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(DiscussionViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);
            var retrieveOperation = TableOperation.Retrieve<DiscussionEntity>(Session["Email"].ToString(), model.Id);
            var result = await table.ExecuteAsync(retrieveOperation);
            var discussion = result.Result as DiscussionEntity;

            if (discussion == null)
                return RedirectToAction("Index");

            // Upload new cover image if provided
            if (model.CoverImage != null && model.CoverImage.ContentLength > 0)
            {
                var container = await StorageHelper.GetBlobContainerReferenceAsync(BlobContainerName);
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(model.CoverImage.FileName);
                var blob = container.GetBlockBlobReference(fileName);
                await blob.UploadFromStreamAsync(model.CoverImage.InputStream);
                discussion.CoverImageUrl = blob.Uri.ToString();
                model.CoverImageUrl = discussion.CoverImageUrl;
            }

            // Update other fields
            discussion.MovieTitle = model.MovieTitle;
            discussion.ReleaseYear = model.ReleaseYear;
            discussion.Genre = model.Genre;
            discussion.IMDBRating = model.IMDBRating;
            discussion.DurationMinutes = model.DurationMinutes;
            discussion.Synopsis = model.Synopsis;

            var updateOperation = TableOperation.Replace(discussion);
            await table.ExecuteAsync(updateOperation);

            ViewBag.Message = "Discussion updated successfully!";
            return View(model);
        }

        // GET: Discussion/Delete/{id}
        [HttpGet]
        public async Task<ActionResult> Delete(string id)
        {
            var email = Session["Email"] as string;
            if (string.IsNullOrEmpty(email) || Session["IsAuthor"] == null || !(bool)Session["IsAuthor"])
                return RedirectToAction("Login", "Account");

            var table = await StorageHelper.GetTableReferenceAsync("Discussions");

            // Retrieve entity by PartitionKey (user) + RowKey (id)
            var retrieveOperation = TableOperation.Retrieve<DiscussionEntity>(email, id);
            var result = await table.ExecuteAsync(retrieveOperation);
            var discussion = result.Result as DiscussionEntity;

            if (discussion == null)
                return HttpNotFound();

            return View(discussion); // Show a confirmation page
        }
        // POST: Discussion/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            var email = Session["Email"] as string;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "Account");

            var table = await StorageHelper.GetTableReferenceAsync("Discussions");

            // Retrieve first
            var retrieveOperation = TableOperation.Retrieve<DiscussionEntity>(email, id);
            var result = await table.ExecuteAsync(retrieveOperation);
            var discussion = result.Result as DiscussionEntity;

            if (discussion != null)
            {
                var deleteOperation = TableOperation.Delete(discussion);
                await table.ExecuteAsync(deleteOperation);
            }

            return RedirectToAction("Index");
        }
    }
}