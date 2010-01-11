using System.Linq;
using System.Web.Mvc;
using Orchard.Blogs.Extensions;
using Orchard.Blogs.Models;
using Orchard.Blogs.Services;
using Orchard.Blogs.ViewModels;
using Orchard.Data;
using Orchard.Localization;
using Orchard.ContentManagement;
using Orchard.Mvc.Results;
using Orchard.Security;
using Orchard.UI.Notify;

namespace Orchard.Blogs.Controllers {
    [ValidateInput(false)]
    public class BlogController : Controller, IUpdateModel {
        private readonly IOrchardServices _services;
        private readonly ISessionLocator _sessionLocator;
        private readonly IAuthorizer _authorizer;
        private readonly INotifier _notifier;
        private readonly IBlogService _blogService;

        public BlogController(IOrchardServices services, ISessionLocator sessionLocator, IAuthorizer authorizer, INotifier notifier, IBlogService blogService) {
            _services = services;
            _sessionLocator = sessionLocator;
            _authorizer = authorizer;
            _notifier = notifier;
            _blogService = blogService;
            T = NullLocalizer.Instance;
        }

        private Localizer T { get; set; }

        public ActionResult List() {
            var model = new BlogsViewModel {
                Blogs = _blogService.Get().Select(b => _services.ContentManager.BuildDisplayModel(b, "Summary"))
            };

            return View(model);
        }

        //TODO: (erikpo) Should move the slug parameter and get call and null check up into a model binder
        public ActionResult Item(string blogSlug) {
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            var model = new BlogViewModel {
                Blog = _services.ContentManager.BuildDisplayModel(blog, "Detail")
            };

            return View(model);
        }

        public ActionResult Create() {
            //TODO: (erikpo) Might think about moving this to an ActionFilter/Attribute
            if (!_authorizer.Authorize(Permissions.CreateBlog, T("Not allowed to create blogs")))
                return new HttpUnauthorizedResult();

            Blog blog = _services.ContentManager.New<Blog>("blog");

            if (blog == null)
                return new NotFoundResult();

            var model = new CreateBlogViewModel {
                Blog = _services.ContentManager.BuildEditorModel(blog)
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult Create(CreateBlogViewModel model) {
            //TODO: (erikpo) Might think about moving this to an ActionFilter/Attribute
            if (!_authorizer.Authorize(Permissions.CreateBlog, T("Couldn't create blog")))
                return new HttpUnauthorizedResult();

            model.Blog = _services.ContentManager.UpdateEditorModel(_services.ContentManager.New<Blog>("blog"), this);

            if (!ModelState.IsValid)
                return View(model);
            
            _services.ContentManager.Create(model.Blog.Item.ContentItem);

            //TEMP: (erikpo) ensure information has committed for this record
            var session = _sessionLocator.For(typeof(BlogRecord));
            session.Flush();

            return Redirect(Url.BlogForAdmin(model.Blog.Item.Slug));
        }

        public ActionResult Edit(string blogSlug) {
            //TODO: (erikpo) Might think about moving this to an ActionFilter/Attribute
            if (!_authorizer.Authorize(Permissions.ModifyBlog, T("Not allowed to edit blog")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            var model = new BlogEditViewModel {
                Blog = _services.ContentManager.BuildEditorModel(blog)
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult Edit(string blogSlug, FormCollection input) {
            if (!_authorizer.Authorize(Permissions.ModifyBlog, T("Couldn't edit blog")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            var model = new BlogEditViewModel {
                Blog = _services.ContentManager.UpdateEditorModel(blog, this)
            };

            if (!ModelState.IsValid)
                return View(model);

            _notifier.Information(T("Blog information updated"));

            return Redirect(Url.BlogsForAdmin());
        }

        //[HttpPost] <- todo: (heskew) make all add/edit/remove POST only and verify the AntiForgeryToken
        public ActionResult Delete(string blogSlug) {
            if (!_authorizer.Authorize(Permissions.DeleteBlog, T("Couldn't delete blog")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            _blogService.Delete(blog);

            _notifier.Information(T("Blog was successfully deleted"));

            return Redirect(Url.BlogsForAdmin());
        }

        bool IUpdateModel.TryUpdateModel<TModel>(TModel model, string prefix, string[] includeProperties, string[] excludeProperties) {
            return TryUpdateModel(model, prefix, includeProperties, excludeProperties);
        }
        void IUpdateModel.AddModelError(string key, LocalizedString errorMessage) {
            ModelState.AddModelError(key, errorMessage.ToString());
        }

    }
}