namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Protocols {
        /// <summary>
        /// Registers a custom URI scheme for the current or a specific application.
        /// </summary>
        public static void Register(string scheme, string appId, string description, string icon) {
            ProtocolManager.RegisterProtocol(scheme, appId, description, icon);
        }

        /// <summary>
        /// Launches a custom URI scheme after user confirmation.
        /// </summary>
        public static void Launch(string uri, string sourceAppId) {
            ProtocolManager.LaunchProtocol(uri, sourceAppId);
        }

        /// <summary>
        /// Gets the AppID registered for a specific protocol scheme.
        /// </summary>
        public static string GetApp(string scheme) {
            return ProtocolManager.GetAppForProtocol(scheme);
        }
    }
}
