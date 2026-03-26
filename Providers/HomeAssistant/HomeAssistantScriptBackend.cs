using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System.Collections.Generic;

namespace NINA.Plugin.SmartSwitchManager.Backends {
    [ExportBackend("HomeAssistantScript", "Home Assistant (Script/Timer)", SupportsHardwareTimer = true)]
    public class HomeAssistantScriptBackend : HomeAssistantBackendBase {
        public override bool SupportsHardwareTimer => true;

        public override void Initialize(SmartSwitchConfig config) {
            base.Initialize(config);
            // Scripts in HA must always be invoked via the 'turn_on' service.
            // Using 'turn_off' on a script aborts the execution instead.
            this.turnOnService = "turn_on";
            this.turnOffService = "turn_on";
        }

        public override IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP / Host", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Token", "Long-Lived Token", ConfigFieldType.Password, true);
                yield return new ConfigFieldDescriptor("EntityId", "Actual State Entity ID", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("ScriptEntityId", "Script Entity ID", ConfigFieldType.Text, true);

                yield return new ConfigFieldDescriptor("UseSSL", "Use SSL (HTTPS)", ConfigFieldType.Boolean) { IsExpertOnly = true, DefaultValue = "False" };
                yield return new ConfigFieldDescriptor("Port", "Port", ConfigFieldType.Number) { IsExpertOnly = true, DefaultValue = "8123" };
                yield return new ConfigFieldDescriptor("PathPrefix", "Path Prefix", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "" };
                yield return new ConfigFieldDescriptor("TurnOnService", "Turn On Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_on" };
                yield return new ConfigFieldDescriptor("TurnOffService", "Turn Off Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_off" };
            }
        }
    }
}
