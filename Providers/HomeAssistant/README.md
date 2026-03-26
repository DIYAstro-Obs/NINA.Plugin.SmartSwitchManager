# Home Assistant Provider

This provider allows you to control devices configured in Home Assistant directly from N.I.N.A.

## Configuration

### Basic Settings
*   **IP / Host:** The address of your Home Assistant instance (e.g., `192.168.1.10` or `homeassistant.local`).
*   **Entity ID:** The ID of the device in Home Assistant (e.g., `switch.telescope_power`).
*   **Long-Lived Token:** A long-lived access token generated within Home Assistant.

### Expert Mode (⚙️ Expert)
When Expert Mode is enabled, additional options become available:
*   **Use SSL (HTTPS):** Enables encrypted connections.
*   **Port:** Default is `8123`.
*   **Path Prefix:** Required if Home Assistant is running behind a reverse proxy with a subfolder (e.g., `/homeassistant`).
*   **Turn On/Off Service:** Allows using alternative services (e.g., `open_cover` for roof controls or `script.run` for complex logic).

## How it works
The provider uses the Home Assistant REST API. When switching, it calls the configured service for the domain specified in the Entity ID. The state is polled regularly to keep the N.I.N.A. UI up to date.

## Timer Support (Home Assistant Script/Timer Provider)

Standard Home Assistant `switch` entities only support simple on/off commands and ignore N.I.N.A.'s "Delay" parameter.
To use timers, you must select the **"Home Assistant (Script/Timer)"** provider when adding the switch in N.I.N.A. This specialized provider enables the "Delay" parameter in the Sequencer and seamlessly passes the actual switch ID, the requested delay, and the desired N.I.N.A. target state (`"on"` or `"off"`) to your Home Assistant Script.

### Creating the Universal Script in Home Assistant

You only need **one single universal script** in Home Assistant to handle all your N.I.N.A. switches! The script will dynamically receive the delay and the target switch ID from N.I.N.A.

1. Open Home Assistant and go to **Settings** -> **Automations & Tags** -> **Scripts**.
2. Click **Add Script** and select **Create new script**.
3. In the top right corner, click the three-dot menu icon (⋮) and choose **Edit in YAML**.
4. Paste the following blueprint code:

```yaml
alias: "N.I.N.A. Universal Switch Timer"
mode: parallel
sequence:
  - delay:
      seconds: "{{ delay_seconds | default(0) }}"
  - service: >
      {% if target_state == 'on' %} switch.turn_on
      {% else %} switch.turn_off
      {% endif %}
    target:
      entity_id: "{{ real_entity_id }}"
```

5. Click **Save** (you do NOT need to replace any placeholders in this code, it is fully dynamic).

### Configuring N.I.N.A.

Now, back in N.I.N.A.:
1. Create a new Smart Switch in the Equipment tab.
2. Set the Provider to **Home Assistant (Script/Timer)**.
3. Set the **Actual State Entity ID** to the **real** switch you want to control (e.g., `switch.observatory_power`). N.I.N.A. will use this to poll the correct ON/OFF state.
4. Set the **Script Entity ID** to the name of your universal script (e.g., `script.n_i_n_a_universal_switch_timer`).
5. Set both Service fields in the Expert settings to `turn_on` (Home Assistant always treats calling a script as `turn_on`).

By using the *Toggle Smart Switch* instruction, N.I.N.A. will send the target state and the delay to the script, while continuing to successfully poll the actual switch's power state!
