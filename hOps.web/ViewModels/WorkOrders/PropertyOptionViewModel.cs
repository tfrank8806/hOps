namespace hOps.web.ViewModels.WorkOrders
{
    public class PropertyOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
