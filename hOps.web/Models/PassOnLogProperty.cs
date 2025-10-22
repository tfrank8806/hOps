using System.ComponentModel.DataAnnotations.Schema;

namespace hOps.web.Models
{
    public class PassOnLogProperty
    {
        public int PassOnLogId { get; set; }

        [InverseProperty(nameof(PassOnLog.Properties))]
        public PassOnLog PassOnLog { get; set; } = default!;

        public int PropertyId { get; set; }

        [InverseProperty(nameof(Property.PassOnLogLinks))]
        public Property Property { get; set; } = default!;
    }
}
