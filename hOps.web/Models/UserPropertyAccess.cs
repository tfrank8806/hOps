namespace hOps.web.Models

{
    public class UserPropertyAccess
    {
        public string ?ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public int PropertyId { get; set; }
        public Property? Property { get; set; }
    }
}
