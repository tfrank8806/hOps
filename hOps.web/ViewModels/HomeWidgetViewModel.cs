using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class HomeWidgetViewModel
    {
        public HomeWidgetViewModel(string title, IReadOnlyCollection<object> items, string controllerName, string actionName)
        {
            Title = title;
            Items = items;
            ControllerName = controllerName;
            ActionName = actionName;
        }

        public string Title { get; }
        public IReadOnlyCollection<object> Items { get; }
        public string ControllerName { get; }
        public string ActionName { get; }
    }
}
