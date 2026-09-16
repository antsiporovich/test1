using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Web.Models;

/// <summary>NOTES-1: add / edit a PM note via modal form.
/// Only ever bound inside PM-authorized controller actions.</summary>
public class NoteFormViewModel
{
    public int ApplicationId { get; set; }

    /// <summary>0 for a new note; positive for an existing note being edited.</summary>
    public int NoteId { get; set; }

    [Required(ErrorMessage = "Note body is required.")]
    [MaxLength(4000, ErrorMessage = "Note may not exceed 4 000 characters.")]
    [Display(Name = "Note")]
    public string? Body { get; set; }
}
