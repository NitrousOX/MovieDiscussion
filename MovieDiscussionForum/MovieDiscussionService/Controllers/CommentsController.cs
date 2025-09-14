using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using MovieDiscussion.Common;
using MovieDiscussion.Common.Models;
using MovieDiscussionService.Models;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class CommentsController : Controller
    {
        // POST: Comments/Create
        [HttpPost]
        public async Task<ActionResult> Create(CommentModel newComment)
        {
            if (!ModelState.IsValid)
                return View(newComment);

            // 1️⃣ Save comment in Azure Table Storage
            CloudTable commentsTable = await StorageHelper.GetTableReferenceAsync("Comments");

            var commentId = Guid.NewGuid().ToString();
            var commentEntity = new CommentEntity(newComment.DiscussionId, commentId)
            {
                UserId = newComment.UserId,
                Text = newComment.Text,
                PostedAt = DateTime.UtcNow
            };

            TableOperation insertOperation = TableOperation.Insert(commentEntity);
            await commentsTable.ExecuteAsync(insertOperation);

            // 2️⃣ Send message to queue with the new comment ID
            CloudQueue notificationsQueue = await StorageHelper.GetQueueReferenceAsync("commentnotificationsqueue");
            var message = new CloudQueueMessage(commentEntity.RowKey); // RowKey = commentId
            await notificationsQueue.AddMessageAsync(message);

            // Redirect to the discussion details page
            return RedirectToAction("Details", "Discussions", new { id = newComment.DiscussionId });
        }
    }
}
