using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels
{
    public class PassOnLogFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Log Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Body")]
        [DataType(DataType.MultilineText)]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();

        public List<PassOnLogPropertyOptionViewModel> PropertyOptions { get; set; } = new();

        public bool ShowPropertySelection => PropertyOptions.Count > 1;

        [Display(Name = "Attachments")]
        public List<IFormFile>? Files { get; set; } = new();

        public List<PassOnLogAttachmentViewModel> ExistingAttachments { get; set; } = new();

        public List<int> AttachmentsToDelete { get; set; } = new();
    }

    public class PassOnLogPropertyOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class PassOnLogIndexViewModel
    {
        public List<PassOnLogListItemViewModel> Logs { get; set; } = new();
        public PassOnLogFiltersViewModel Filters { get; set; } = new();
        public bool CanCreateLog { get; set; }
    }

    public class PassOnLogListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public UserAvatarViewModel CreatorAvatar { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public bool IsUnread { get; set; }
        public List<string> PropertyNames { get; set; } = new();
        public int CommentCount { get; set; }
        public string Preview { get; set; } = string.Empty;
    }

    public class PassOnLogFiltersViewModel
    {
        public string SortOrder { get; set; } = "newest";

        [DataType(DataType.Date)]
        [Display(Name = "From")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "To")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Creator")]
        public List<string> CreatorIds { get; set; } = new();

        [Display(Name = "Keyword")]
        public string? SearchTerm { get; set; }

        public List<int> PropertyIds { get; set; } = new();

        public List<SelectListItem> CreatorOptions { get; set; } = new();
        public List<SelectListItem> SortOptions { get; set; } = new();
        public List<PassOnLogPropertyOptionViewModel> PropertyOptions { get; set; } = new();
        public bool ShowUnreadOnly { get; set; }

        public void Normalize()
        {
            SortOrder = string.IsNullOrWhiteSpace(SortOrder) ? "newest" : SortOrder.Trim().ToLowerInvariant();
            SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim();
            CreatorIds = CreatorIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            PropertyIds = PropertyIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }
    }

    public class PassOnLogDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public UserAvatarViewModel CreatorAvatar { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> PropertyNames { get; set; } = new();
        public List<PassOnLogCommentViewModel> Comments { get; set; } = new();
        public List<PassOnLogViewerViewModel> Viewers { get; set; } = new();
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public PassOnLogCommentInputModel NewComment { get; set; } = new();
        public List<PassOnLogAttachmentViewModel> Attachments { get; set; } = new();
        public int? NextLogId { get; set; }
        public int? PreviousLogId { get; set; }
    }

    public class PassOnLogCommentViewModel
    {
        public int Id { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public UserAvatarViewModel CreatorAvatar { get; set; } = new();
    }

    public class PassOnLogViewerViewModel
    {
        public string Name { get; set; } = string.Empty;
        public DateTime ViewedAt { get; set; }
    }

    public class PassOnLogCommentInputModel
    {
        public int LogId { get; set; }

        [Required]
        [MaxLength(2000)]
        [Display(Name = "Comment")]
        public string Body { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
        [Display(Name = "Add Images")]
        public List<IFormFile>? Files { get; set; } = new();
    }

    public class PassOnLogAttachmentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
