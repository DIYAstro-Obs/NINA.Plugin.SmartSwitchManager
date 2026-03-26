using NINA.Plugin.SmartSwitchManager.Core;

namespace NINA.Plugin.SmartSwitchManager.Backends {
    [ExportBackend("HomeAssistant", "Home Assistant", SupportsHardwareTimer = false)]
    public class HomeAssistantBackend : HomeAssistantBackendBase {
        public override bool SupportsHardwareTimer => false;
    }
}
