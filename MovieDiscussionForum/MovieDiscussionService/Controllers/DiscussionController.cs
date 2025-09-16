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
        private const string ReactionTableName = "Reactions";
        private const string FollowTableName = "Follows";

        public async Task<ActionResult> Index(string searchTitle = "", string searchGenre = "", string sortBy = "", int page = 1)
        {
            int pageSize = 3; // koliko diskusija po strani

            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);
            var query = new TableQuery<DiscussionEntity>();
            var discussions = new List<DiscussionEntity>();
            var followTable = await StorageHelper.GetTableReferenceAsync(FollowTableName);
            var allFollows = new List<FollowEntity>();
            var userId = Session["Email"] as string;


            TableContinuationToken token = null;
            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                discussions.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null);


            TableContinuationToken fToken = null;
            do
            {
                var fSeg = await followTable.ExecuteQuerySegmentedAsync(new TableQuery<FollowEntity>(), fToken);
                allFollows.AddRange(fSeg.Results);
                fToken = fSeg.ContinuationToken;
            } while (fToken != null);

            var userFollows = allFollows
                .Where(f => f.RowKey == userId)
                .ToDictionary(f => f.PartitionKey, f => f.IsFollowing);

            ViewBag.UserFollows = userFollows;

            // --- Filter ---
            if (!string.IsNullOrEmpty(searchTitle))
                discussions = discussions.Where(d => d.MovieTitle.IndexOf(searchTitle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (!string.IsNullOrEmpty(searchGenre))
                discussions = discussions.Where(d => d.Genre.IndexOf(searchGenre, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // --- Sort ---
            /*if (sortBy == "likes")
                discussions = discussions.OrderByDescending(d => reactionCounts.ContainsKey(d.RowKey) ? reactionCounts[d.RowKey].Likes : 0).ToList();
            else if (sortBy == "dislikes")
                discussions = discussions.OrderByDescending(d => reactionCounts.ContainsKey(d.RowKey) ? reactionCounts[d.RowKey].Dislikes : 0).ToList();
            */
            // --- Pagination ---
            int totalItems = discussions.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages)); // ograniči page

            var pagedDiscussions = discussions.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(pagedDiscussions);
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



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleFollow(string discussionId)
        {
            var userId = Session["Email"] as string;
            if (string.IsNullOrEmpty(userId))
                return new HttpUnauthorizedResult();

            var table = await StorageHelper.GetTableReferenceAsync(FollowTableName);

            // Proveri da li već postoji follow
            var retrieve = TableOperation.Retrieve<FollowEntity>(discussionId, userId);
            var result = await table.ExecuteAsync(retrieve);
            var existing = result.Result as FollowEntity;

            if (existing == null)
            {
                // kreiraj novi follow
                var newFollow = new FollowEntity(discussionId, userId);
                await table.ExecuteAsync(TableOperation.Insert(newFollow));
                return Json(new { Following = true });
            }
            else
            {
                // ukloni follow
                await table.ExecuteAsync(TableOperation.Delete(existing));
                return Json(new { Following = false });
            }
        }

        // GET: Discussion/Details/{id}
        // GET: Discussion/Details/{id}
        public async Task<ActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("Index");

            var table = await StorageHelper.GetTableReferenceAsync(DiscussionTableName);

            // Traži po RowKey, ignoriši PartitionKey
            var query = new TableQuery<DiscussionEntity>()
                            .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, id));

            var segment = await table.ExecuteQuerySegmentedAsync(query, null);
            var discussion = segment.Results.FirstOrDefault();

            if (discussion == null)
                return HttpNotFound();

            // Uzmi komentare
            var commentsTable = await StorageHelper.GetTableReferenceAsync("Comments");
            var queryComments = new TableQuery<CommentEntity>()
                                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, id));
            var comments = new List<CommentEntity>();
            TableContinuationToken token = null;
            do
            {
                var seg = await commentsTable.ExecuteQuerySegmentedAsync(queryComments, token);
                comments.AddRange(seg.Results);
                token = seg.ContinuationToken;
            } while (token != null);

            ViewBag.DiscussionId = id;
            ViewBag.Comments = comments.OrderBy(c => c.PostedAt).ToList();

            return View(discussion);
        }



    }
}