using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Backend for Tasmota devices using the HTTP API.
    /// Commands: cm?cmnd=Power<CH>%20On, cm?cmnd=Power<CH>%20Off, cm?cmnd=Power<CH>
    /// </summary>
    [ExportBackend("Tasmota", "Tasmota", SupportsScanning = true, SupportsHardwareTimer = false)]
    public class TasmotaBackend : ISmartSwitchBackend {
        private string baseUrl;
        private int channel;
        private string powerCmd;
        private string username;
        private string password;
        private bool isInitialized;
        private readonly System.Threading.SemaphoreSlim _httpLock = new System.Threading.SemaphoreSlim(1, 1);

        public TasmotaBackend() {
        }

        public void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Host URL / IP must not be empty.");
            }

            // Ensure URL starts with http://
            if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                host = "http://" + host;
            }
            baseUrl = host.TrimEnd('/');
            
            string channelStr = config.GetSetting("Channel", "1");
            int.TryParse(channelStr, out this.channel);
            if (this.channel < 1) this.channel = 1;

            // Use indexed Power command for consistency (Power1, Power2...)
            this.powerCmd = $"Power{this.channel}";
            this.username = config.GetSetting("Username");
            this.password = config.GetSetting("Password");
            isInitialized = true;
        }

        private void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        private HttpClient HttpClient => SmartSwitchHttpClient.Instance;

        private HttpRequestMessage CreateRequest(string url) {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                string separator = url.Contains("?") ? "&" : "?";
                url += $"{separator}user={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
            }
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Disable Keep-Alive. ESP8266 drops stale TCP connections, causing .NET to stumble on reuse.
            request.Headers.ConnectionClose = true;
            return request;
        }

        public async Task TurnOnAsync() {
            await SetStateAsync(true, 0);
        }

        public async Task TurnOffAsync() {
            await SetStateAsync(false, 0);
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            await _httpLock.WaitAsync();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={powerCmd}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    
                    // Tasmota returns {"POWER1":"ON"} or {"POWER":"OFF"}
                    // We prioritize the indexed key as it's the most specific.
                    string state = null;
                    string targetKey = powerCmd.ToUpper(); // e.g. POWER1

                    if (json.TryGetValue(targetKey, out var val)) {
                        state = val.ToString();
                    } else if (json.TryGetValue("POWER", out var v0)) {
                        state = v0.ToString();
                    } else {
                        // Case-insensitive fallback: scan all keys for something matching PowerX
                        foreach (var prop in json.Properties()) {
                            if (string.Equals(prop.Name, targetKey, StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(prop.Name, "POWER", StringComparison.OrdinalIgnoreCase)) {
                                state = prop.Value.ToString();
                                break;
                            }
                        }
                    }

                    if (state == null) {
                        throw new Exception($"Could not find state for {powerCmd} in response: {content}");
                    }

                    return string.Equals(state, "ON", StringComparison.OrdinalIgnoreCase);
                });
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to get state: {ex.Message}");
                throw;
            } finally {
                _httpLock.Release();
            }
        }

        public bool SupportsHardwareTimer => false;

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();

            // Safety Check: Avoid power-cycling if target state is already reached
            bool currentState = await GetStateAsync();
            if (currentState == targetState) {
                Logger.Info($"TasmotaBackend: Switch ({baseUrl}, Ch: {channel}) is already {(targetState ? "ON" : "OFF")}. Skipping command.");
                return;
            }

            try {
                string stateVal = targetState ? "1" : "0";
                
                // Pure, clean, standard toggle. No timer rules, no PulseTime manipulation.
                await ExecuteCommandAsync($"{powerCmd} {stateVal}");
                
                Logger.Info($"TasmotaBackend: Set state to {(targetState ? "ON" : "OFF")} ({baseUrl}, Ch: {channel})");
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to set state: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteCommandAsync(string command) {
            await _httpLock.WaitAsync();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={Uri.EscapeDataString(command)}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();

                    // Tasmota often returns HTTP 200 OK even when it fails to parse the command (e.g. under load).
                    // We must read the JSON body to see if it actually accepted the command.
                    var content = await response.Content.ReadAsStringAsync();
                    try {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                        if (json.TryGetValue("Command", out var cmdTarget) && string.Equals(cmdTarget.ToString(), "Unknown", StringComparison.OrdinalIgnoreCase)) {
                            throw new Exception($"Tasmota rejected command as Unknown: {content}");
                        }
                        if (json.TryGetValue("Error", out var errStr)) {
                            throw new Exception($"Tasmota returned an Error: {content}");
                        }
                    } catch (Newtonsoft.Json.JsonReaderException) {
                         // Some very old or weird Tasmota responses might not be perfect JSON.
                         if (content.Contains("\"Command\":\"Unknown\"") || content.Contains("Error")) {
                             throw new Exception($"Tasmota returned invalid/error response: {content}");
                         }
                    }

                    return true;
                });

                // Tasmota answers HTTP 200 OK immediately for Backlog, but executes it slowly (~200ms per command).
                // If we send a new HTTP request (like GetStateAsync) while Backlog is running, Tasmota ABORTS the backlog!
                // We keep the lock held for 750ms to give Tasmota absolute silence to finish switching before the next poll.
                await Task.Delay(750);
            } finally {
                _httpLock.Release();
            }
        }
 
        public System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP Address", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Channel", "Relay Index", ConfigFieldType.Number, true, "1");
                yield return new ConfigFieldDescriptor("Username", "User", ConfigFieldType.Text, false);
                yield return new ConfigFieldDescriptor("Password", "Pass", ConfigFieldType.Password, false);
            }
        }
        public void Dispose() {
            _httpLock?.Dispose();
        }
    }
}
