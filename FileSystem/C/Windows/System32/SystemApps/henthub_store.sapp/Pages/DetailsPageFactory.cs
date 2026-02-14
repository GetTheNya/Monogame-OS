using System;
using TheGame.Core.OS;

namespace HentHub;

public static class DetailsPageFactory {
    public static BaseDetailsPage Create(StoreApp app, Process process) {
        if (app.ExtensionType != null && app.ExtensionType.Equals("widget", StringComparison.OrdinalIgnoreCase)) {
            return new WidgetDetailsPage(app, process);
        }
        
        return new ApplicationDetailsPage(app, process);
    }
}
