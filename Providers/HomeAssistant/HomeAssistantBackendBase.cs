using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    public abstract class HomeAssistantBackendBase : ISmartSwitchBackend {
        protected string baseUrl = string.Empty;
        protected string entityId = string.Empty;
        protected string haToken = string.Empty;
        protected string turnOnService = "turn_on";
        protected string turnOffService = "turn_off";
        protected bool isInitialized;
        protected string scriptEntityId = string.Empty;

        public virtual void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            // Read and validate basic settings
            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Home Assistant IP or URL must not be empty.");
            }
            
            this.entityId = config.GetSetting("EntityId");
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity ID must not be empty.");
            }

            this.scriptEntityId = config.GetSetting("ScriptEntityId");

            // Read expert settings (with fallbacks)
            bool useSsl = false;
            string useSslStr = config.GetSetting("UseSSL");
            if (!string.IsNullOrWhiteSpace(useSslStr)) {
                bool.TryParse(useSslStr, out useSsl);
            }
            
            int port = 8123;
            string portStr = config.GetSetting("Port");
            if (!string.IsNullOrWhiteSpace(portStr)) {
                int.TryParse(portStr, out port);
            }

            string pathPrefix = config.GetSetting("PathPrefix", "").Trim();
            if (!string.IsNullOrEmpty(pathPrefix) && !pathPrefix.StartsWith("/")) {
                pathPrefix = "/" + pathPrefix; // Ensure leading slash
            }

            this.turnOnService = config.GetSetting("TurnOnService", "turn_on").Trim();
            if (string.IsNullOrEmpty(this.turnOnService)) this.turnOnService = "turn_on";

            this.turnOffService = config.GetSetting("TurnOffService", "turn_off").Trim();
            if (string.IsNullOrEmpty(this.turnOffService)) this.turnOffService = "turn_off";

            // --- Smart URL Building ---
            host = host.Trim();
            string protocol = useSsl ? "https://" : "http://";

            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) {
                protocol = "http://";
                host = host.Substring(7);
            } else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                protocol = "https://";
                host = host.Substring(8);
            }

            host = host.TrimEnd('/'); 

            string finalHostAndPort = host;
            if (!host.Contains(":")) {
                finalHostAndPort = $"{host}:{port}";
            }

            baseUrl = $"{protocol}{finalHostAndPort}{pathPrefix}".TrimEnd('/');
            
            haToken = config.GetSetting("Token")?.Trim() ?? string.Empty;
            isInitialized = true;
        }

        protected void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        protected HttpClient HttpClient => SmartSwitchHttpClient.Instance;

        protected HttpRequestMessage CreateRequest(HttpMethod method, string url) {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(haToken)) {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", haToken);
            }
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        protected async Task ExecuteActionAsync(string serviceToCall, bool isTurnOn, int delaySeconds = 0) {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    object payload;
                    string executionEntity = string.IsNullOrWhiteSpace(scriptEntityId) ? entityId : scriptEntityId;
                    
                    if (SupportsHardwareTimer) {
                        string targetStateString = isTurnOn ? "on" : "off";
                        if (delaySeconds > 0) {
                            payload = new { 
                                entity_id = executionEntity, 
                                variables = new { delay_seconds = delaySeconds, target_state = targetStateString, real_entity_id = entityId }
                            };
                        } else {
                            payload = new { 
                                entity_id = executionEntity, 
                                variables = new { target_state = targetStateString, real_entity_id = entityId }
                            };
                        }
                    } else {
                        payload = new { entity_id = executionEntity };
                    }
                    var jsonBody = JsonConvert.SerializeObject(payload);
                    var parts = executionEntity.Split('.');
                    var domain = parts.Length > 1 ? parts[0] : "switch";
                    
                    using var request = CreateRequest(HttpMethod.Post, $"{baseUrl}/api/services/{domain}/{serviceToCall}");
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"HomeAssistantBackend: Called service {serviceToCall} on {(string.IsNullOrWhiteSpace(scriptEntityId) ? entityId : scriptEntityId)} (Target: {entityId}, Delay: {delaySeconds}s)");
            } catch (Exception ex) {
                Logger.Error($"HomeAssistantBackend: Failed to call service {serviceToCall} on {(string.IsNullOrWhiteSpace(scriptEntityId) ? entityId : scriptEntityId)}: {ex.Message}");
                throw;
            }
        }

        public async Task TurnOnAsync() {
            await ExecuteActionAsync(turnOnService, true);
        }

        public async Task TurnOffAsync() {
            await ExecuteActionAsync(turnOffService, false);
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest(HttpMethod.Get, $"{baseUrl}/api/states/{entityId}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseContent);
                    var stateString = json.Value<string>("state");
                    
                    var isOn = string.Equals(stateString, "on", StringComparison.OrdinalIgnoreCase);
                    Logger.Trace($"HomeAssistantBackend: State query ({entityId}) → state='{stateString}', ison={isOn}");
                    return isOn;
                });
            } catch (Exception ex) {
                Logger.Error($"HomeAssistantBackend: Failed to get state ({entityId}): {ex.Message}");
                throw;
            }
        }

        public abstract bool SupportsHardwareTimer { get; }

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            if (targetState) {
                await ExecuteActionAsync(turnOnService, true, delaySeconds);
            } else {
                await ExecuteActionAsync(turnOffService, false, delaySeconds);
            }
        }
 
        public virtual System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP / Host", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Token", "Long-Lived Token", ConfigFieldType.Password, true);
                yield return new ConfigFieldDescriptor("EntityId", "Entity ID", ConfigFieldType.Text, true);

                yield return new ConfigFieldDescriptor("UseSSL", "Use SSL (HTTPS)", ConfigFieldType.Boolean) { IsExpertOnly = true, DefaultValue = "False" };
                yield return new ConfigFieldDescriptor("Port", "Port", ConfigFieldType.Number) { IsExpertOnly = true, DefaultValue = "8123" };
                yield return new ConfigFieldDescriptor("PathPrefix", "Path Prefix", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "" };
                yield return new ConfigFieldDescriptor("TurnOnService", "Turn On Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_on" };
                yield return new ConfigFieldDescriptor("TurnOffService", "Turn Off Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_off" };
            }
        }
        
        public void Dispose() {
        }
    }
}
