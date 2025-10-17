namespace hOps.web.Models
{
    public class WorkOrderProperty
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = null!;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
    }
}
