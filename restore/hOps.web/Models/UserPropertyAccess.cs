namespace hOps.web.Models
{
    public class UserPropertyAccess
    {
        public string ApplicationUserId { get; set; } = string.Empty;
        public ApplicationUser ApplicationUser { get; set; } = default!;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;
    }
}
