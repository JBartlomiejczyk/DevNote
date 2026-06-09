using Microsoft.AspNetCore.Identity;

namespace DevNote.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<ConversationNote> Notes { get; set; } = new List<ConversationNote>();
}
