# Tasmota Provider

This provider supports all Tasmota-flashed devices accessible via the HTTP protocol.

## Configuration

*   **IP Address:** The IP address of the Tasmota device.
*   **Relay Index:** The index of the relay to control (default is `1`).
*   **User / Pass:** (Optional) If you have set web credentials for the Tasmota interface.

## Scanner
This provider supports automatic network scanning. Clicking the "Scanner" icon (magnifying glass) allows you to enter an IP address to automatically discover and import all available relays/channels on the device.

## Authentication
The provider uses Tasmota's HTTP API. If credentials are provided, they are transmitted securely as URL parameters (`user` & `password`) to the device.

## Troubleshooting

### Ghost Switching (Relay Toggling Twice)
Modern ESP-based relays like the **Sonoff Basic R4** or **Mini R4** feature very sensitive switch pins. High-voltage switching can cause electromagnetic interference (EMI), leading to "Ghost Switching" where the device toggles back immediately (approx. 50ms later).

**The Solution:**
1. Open the Tasmota Web UI -> **Configuration** -> **Configure Other**.
2. Disable the physical button/switch pins in your **Template** if not required. 
3. For the **Sonoff Basic R4**, use this interference-free template:
   `{"NAME":"Sonoff Basic R4 (NoGhost)","GPIO":[0,0,0,0,224,0,544,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"FLAG":0,"BASE":1}`
4. To clear any conflicting legacy settings (Rules/Timers), run this in the Tasmota Console:
   `Backlog Rule1 ""; Rule2 ""; Rule3 ""; PulseTime1 0`
