using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class HomeWidgetViewModel
    {
        public HomeWidgetViewModel(
            string title,
            IReadOnlyCollection<object> items,
            string controllerName,
            string actionName,
            string? createControllerName = null,
            string? createActionName = null,
            string? createButtonText = null,
            string? createFragment = null)
        {
            Title = title;
            Items = items;
            ControllerName = controllerName;
            ActionName = actionName;
            CreateControllerName = createControllerName;
            CreateActionName = createActionName;
            CreateButtonText = createButtonText;
            CreateFragment = createFragment;
        }

        public string Title { get; }
        public IReadOnlyCollection<object> Items { get; }
        public string ControllerName { get; }
        public string ActionName { get; }
        public string? CreateControllerName { get; }
        public string? CreateActionName { get; }
        public string? CreateButtonText { get; }
        public string? CreateFragment { get; }
    }
}
