#nullable enable

namespace hOps.web.Models
{
    public class Property
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Address { get; set; }
    }
}
