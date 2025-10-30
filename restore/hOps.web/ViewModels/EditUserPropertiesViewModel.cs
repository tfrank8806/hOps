using hOps.web.Models;

public class EditUserPropertiesViewModel
{
    public string UserId { get; set; } = "";
    public string? Email { get; set; }

    public List<Property> PropertyList { get; set; } = new List<Property>();
    public List<int>? SelectedPropertyIds { get; set; }

    // Role assignment
    public List<string> AllRoles { get; set; } = new List<string>();
    public List<string>? SelectedRoles { get; set; }
}
