# MeshCom WebDesk

A **Blazor Server** web application for communicating with a [MeshCom 4.0](https://icssw.org/meshcom/) node via UDP (EXTUDP JSON protocol).  
Built with **.NET 10** and **Blazor Interactive Server**.

> **MeshCom Firmware:** Compatible with [icssw-org/MeshCom-Firmware](https://github.com/icssw-org/MeshCom-Firmware) v4.35+

---

## 💡 Motivation

MeshCom always reminds me a little of the good old **Packet Radio** days – digital text communication over radio, simple and direct.

That is why I created this **MeshCom WebDesk**.

and makes a full web client for MeshCom available via a simple URL

---

## Screenshots

![MeshCom Web Client](docs/screenshot.png?v=2)

---

## Features

### 💬 Chat
- **Multi-tab conversations** – each partner (callsign, group, broadcast) gets its own tab
- **Broadcast tab "All"** for `*` / `CQCQCQ` messages
- **Direct messages** – each callsign gets its own tab automatically
- **Group messages** – group destinations appear as `#<group>` tabs with optional whitelist filter
- Smart routing: broadcast replies from a known callsign appear in their direct tab
- **Auto-scroll** to the latest message when a tab is opened or a new message arrives
- **Unread badge** – inactive tabs show a yellow counter badge for new messages
- **ACK delivery indicator** on every outgoing message:
  - `⏳` grey – waiting for node echo (message queued)
  - `✓` blue – node has transmitted over LoRa (sequence number assigned)
  - `✓✓` green – recipient confirmed delivery (APRS ACK received)
- **Clickable callsigns in the monitor** – click any sender or recipient to open a chat tab instantly
- **Audio notification** 🔔 when a new direct message to your own callsign arrives (Web Audio API, no audio file required); mute toggle in the status bar

### 📻 MH – Most Recently Heard
- Live table of all heard stations with last message, timestamp and message count
- **GPS position** parsed from EXTUDP position packets (`lat_dir` / `long_dir` APRS format)
- **Distance calculation** (Haversine) from own position to each heard station
- **Battery level** 🔋 column parsed from `batt` field in position/telemetry packets, colour-coded (🟢 >60% / 🟡 >30% / 🔴 ≤30%)
- **Hardware badge** – short hardware name from `hw_id` field (e.g. `T-BEAM`, `T-ECHO`, `HELTEC-V3`)
- **Firmware tooltip** – hover the callsign to see firmware version, hardware ID and first-heard time
- **RSSI / SNR** signal quality with colour coding (green / yellow / red)
- Altitude correctly converted from APRS feet to metres
- 🗺️ OpenStreetMap link per station
- Own position extracted automatically from the node's `type:"pos"` UDP beacon
- **Browser GPS** button to use device geolocation as own position
- Click 💬 to open a chat tab with any station

### 📡 Monitor (lower pane)
- Structured display with type badge (`MSG` / `POS` / `TEL` / `ACK` / `SYS`), direction (`RX` / `TX`), routing and signal
- **Full relay path** shown inline for relayed messages: `OE1XAR-62 ⟶ DL0VBK-12 ⟶ DB0KH-11 → all`
- **Telemetry rows** (`type:"tele"`) display temperature 🌡️, humidity 💧, pressure 🧭 and battery 🔋
- Colour-coded rows: green for TX, cyan for position beacons, purple for telemetry, gold for ACKs
- **UDP registration packet** (`{"type":"info",...}`) sent on startup is shown as a `SYS` TX entry
- Newest entry always at the top; configurable history limit (`MonitorMaxMessages`)
- Collapsible on mobile (toggle button)

### 📊 Status bar
- UDP socket state (🟢 Active / 🔴 Inactive) and registration status
- Last RX timestamp, sender callsign, RSSI / SNR with colour coding
- TX counter, own callsign, device IP:Port
- Own GPS position with source label (Node / Browser GPS)
- 🔔 / 🔕 Sound notification toggle

### 🔄 Deduplication
- Incoming messages are deduplicated using the `msg_id` field (unique hex ID from the node)
- Fallback chain: `msg_id` → `{NNN}` sequence number → message text
- Duplicate suppression window: 10 minutes (rolling cache, auto-pruned)

### 💾 State Persistence
- Chat tabs, MH list, monitor history and **own GPS position** are saved to disk on shutdown
- State is restored automatically on startup – no waiting for the first position beacon
- Auto-save every 5 minutes; data stored in `DataPath` (configurable)

### ℹ️ About page
- Displays assembly version (e.g. `v1.4.1`), build timestamp and links
- Version is also shown in the **navigation bar** next to the app title
- Author contact: [dh1fr@darc.de](mailto:dh1fr@darc.de)

### ⚙️ Settings page
- Web-based configuration editor at `/settings` – edit all settings directly in the browser
- Changes are written to `appsettings.override.json` in `DataPath` (Docker-safe read-only mount supported)
- Most settings apply **immediately without restart**
- Settings that still require a restart: **Listen-IP / Listen-Port** (socket binding) and **Log-Path / Log-Retention** (Serilog)

### 🌐 UI Language
- Full bilingual interface: **Deutsch 🇩🇪** and **English 🇬🇧**
- Language is selected in **Settings → Language** and persisted in `appsettings.override.json`
- Switching applies **instantly** across all pages without any page reload or restart

### 📡 Beacon (Bake)
- **Periodic beacon** – sends a configurable text to a configurable group at a fixed interval
- Interval is configurable in whole hours (minimum 1 h); first transmission after one full interval
- Enabled / disabled via `BeaconEnabled` flag – applies **live** without restart
- **`{version}` placeholder** in `BeaconText` is replaced with the running application version at send time
- **Status indicator** in the status bar: pulsing `●` dot with next scheduled send time; turns yellow when < 10 min away
- Beacon appears in the monitor feed and in the corresponding group chat tab

### ↩️ Auto-Reply
- Sends a configurable reply text automatically when a **brand-new direct chat tab** is opened by an incoming message (first contact from a callsign)
- Enabled / disabled via `AutoReplyEnabled` – applies **live** without restart
- **`{version}` placeholder** in `AutoReplyText` is replaced with the running application version at send time  
  Example: `MeshCom WebDesk V{version}` → `MeshCom WebDesk V1.4.1`
- **Test button** in Settings – send the auto-reply text immediately to any callsign without waiting for an incoming message

### 📊 Telemetry (Telemetrie-Sender)
- **Periodic telemetry messages**
- **Source-agnostic**: any system can write the JSON file – Home Assistant, Node-RED, MQTT bridge, shell script, etc.
- **HTTP POST endpoint** `POST /api/telemetry` – external sources (e.g. Home Assistant) can push JSON directly; no shared filesystem needed; protected by optional `X-Api-Key` header
- **Flexible mapping** – unlimited key → label / unit / decimal-places pairs, fully configurable in the Settings UI without touching source code
- **Auto-split**: if all values exceed 150 chars, messages are automatically split into `TM1:` / `TM2:` / … with a 2-second pause between packets
- **Destination** – group (e.g. `#262`), broadcast (`*`) or direct callsign (e.g. `OE1KBC-1`)
- **Status indicator** in the status bar analogue to the beacon
- **Live preview** in Settings: shows current file values, formatted output per entry, and exact LoRa message(s)
- **Instant send button** in Settings for immediate test send without waiting for the interval
- Example messages: `TM: 🌡=10.7C 🧭=1022hPa 💧=86% 🌬=0.0m/s` or split into `TM1:` / `TM2:` when needed
- 📖 **[Home Assistant integration guide](docs/homeassistant-telemetry.md)** – complete example with weather station sensors, `rest_command` and automation

### 📝 Logging (Serilog)
- Rolling daily log files with configurable retention
- Optional UDP traffic log (`LogUdpTraffic`) for offline analysis

### 🗄️ Database integration (Beta)
- Optional persistent storage of all monitor data to an external database
- **MySQL / MariaDB**: writes each monitor entry as a row via parametrised `INSERT` (uses `MySqlConnector`)
- **InfluxDB 2**: writes each monitor entry as a point via HTTP Line Protocol (`/api/v2/write`)
- Provider selection in **Settings → 🗄️ Datenbank (Beta)**: `none` / `mysql` / `influxdb2`
- **"Test connection"** button: detects missing database, table or bucket and offers **automatic creation** with a single click
- **Optional insert logging** – every successful write is logged at `Information` level; privacy notice shown in Settings
- Provider and connection settings change **live without restart**

### 💬 Message length validation
- MeshCom LoRa packets are limited to **149 characters** of message text
- **Character counter** `X/149` next to the input field: grey → yellow (≥ 130) → red bold (≥ 145)
- `maxlength="149"` prevents over-long input in the browser
- **Server-side guard** in `SendMessageAsync`: logs a warning and aborts send if text exceeds 149 characters

### 🗺️ Live Map
- Interactive map at `/map` powered by **Leaflet.js + OpenStreetMap**
- **APRS-style markers**: filled circle colour-coded by RSSI (🟢 > −90 / 🟡 > −105 / 🔴 ≤ −105 dBm) + callsign label below
- **Own position** shown as gold diamond ◆ (APRS convention)
- **Popup** on click: callsign, last message, RSSI, battery, altitude
- **Auto-fit bounds** – map zooms to include all visible stations automatically
- **Compact info bar** at the bottom: `📡 N Station(en) · 📍 MyCallsign` – clean one-liner regardless of station count
- Updates in real-time as new position beacons arrive
- Nav link 🗺️ added to the navigation bar

### 🔗 Webhook
- **HTTP POST** to a configurable URL on incoming events
- Configurable **triggers**: chat messages / position beacons / telemetry (each individually)
- **JSON payload**: `event`, `timestamp`, `from`, `to`, `text`, `rssi`, `snr`, `latitude`, `longitude`, `altitude`, `battery`, `firmware`, `relay_path`, `src_type`
- Fire-and-forget (10 s timeout); errors logged and swallowed – never blocks reception
- Configured in **Settings → 🔗 Webhook**; changes apply **live without restart**
- Compatible with **Home Assistant** webhooks, Node-RED, n8n, IFTTT, custom endpoints

### 📱 PWA – Progressive Web App
- **Installable** on any device via the browser's "Add to Home Screen" / "Install" prompt
- `manifest.webmanifest` with name, icon, `display: standalone`, shortcuts (Chat + Map)
- **Minimal service worker** – enables install prompt; full offline not possible (Blazor Server requires live connection)
- **Apple meta tags** for iOS Safari Add-to-Home-Screen
- Custom **antenna SVG icon** in the app colour scheme

---

## Architecture

```
MeshcomWebDesk/              ← Blazor Server (ASP.NET Core host)
│  Program.cs                  ← DI setup, Serilog, hosted services
│  appsettings.json            ← All configuration
│
├─ Components/
│  ├─ App.razor                ← HTML shell + JS helpers + Leaflet CDN + SW registration
│  ├─ Layout/
│  │    MainLayout.razor       ← Top navigation bar
│  └─ Pages/
│       Chat.razor             ← Chat tabs + monitor pane + status bar
│       Mh.razor               ← Most Recently Heard table + own position
│       Map.razor              ← Live Leaflet map with APRS-style markers
│       Settings.razor         ← Web-based configuration editor
│       About.razor            ← Version / copyright / build info + PayPal donation link
│       Clear.razor            ← Data reset page
│
├─ Helpers/
│     GeoHelper.cs             ← Haversine, coordinate formatting, OSM links
│     MeshcomLookup.cs         ← hw_id → hardware name table, firmware formatter
│
├─ Models/
│     MeshcomMessage.cs        ← Message model (from/to/text/GPS/RSSI/ACK/relay/telemetry)
│     MeshcomSettings.cs       ← Strongly-typed config (IOptions)
│     TelemetryMappingEntry.cs ← Telemetry mapping entry (JSON key → label + unit + decimals)
│     DatabaseSettings.cs      ← DB provider + connection settings + LogInserts
│     WebhookSettings.cs       ← Webhook URL + trigger flags
│     ChatTab.cs               ← Tab model with UnreadCount
│     HeardStation.cs          ← MH list entry (GPS, signal, battery, hardware, firmware)
│     ConnectionStatus.cs      ← Live UDP status + own GPS position
│     PersistenceSnapshot.cs   ← Serialisable state snapshot (tabs, MH, monitor, own GPS)
│
├─ wwwroot/
│     map.js                   ← Leaflet JS helpers (init, updateMarkers, APRS icons)
│     manifest.webmanifest     ← PWA manifest (name, icon, display:standalone, shortcuts)
│     service-worker.js        ← Minimal SW – enables install prompt
│     icons/icon.svg           ← Antenna icon in app colour scheme
│
└─ Services/
      MeshcomUdpService.cs     ← BackgroundService: UDP RX/TX, JSON parsing, ACK matching, beacon timer
      ChatService.cs           ← Singleton: routing, tabs, MH list, monitor, deduplication, webhook trigger
      DataPersistenceService.cs← BackgroundService: load/save state to JSON on disk
      SettingsService.cs       ← Writes appsettings.override.json in DataPath (Docker-safe); changes applied live via IOptionsMonitor
      LanguageService.cs       ← Singleton: UI language switching (de/en); T(de,en) helper; OnChange event for instant re-render
      WebhookService.cs        ← HTTP POST fire-and-forget on message / position / telemetry events
      Database/
        IMonitorDataSink.cs    ← Interface: WriteAsync(MeshcomMessage)
        MySqlMonitorSink.cs    ← MySQL / MariaDB write sink (MySqlConnector)
        InfluxDbMonitorSink.cs ← InfluxDB 2 write sink (HTTP Line Protocol)
        MonitorSinkService.cs  ← Routes each write to the active provider; IOptionsMonitor-aware
        DatabaseSetupService.cs← Connection test + automatic schema creation (DB, table, bucket)
```

---

## Configuration

All settings in `MeshcomWebDesk/appsettings.json`:

```json
"Meshcom": {
  "ListenIp":           "0.0.0.0",       // bind address (0.0.0.0 = all interfaces)
  "ListenPort":         1799,            // local UDP port
  "DeviceIp":           "192.168.1.60",  // MeshCom node IP
  "DevicePort":         1799,            // MeshCom node UDP port
  "MyCallsign":         "NOCALL-1",       // own callsign
  "LogPath":            "C:\\Temp\\Logs",// log file directory
  "LogRetainDays":      30,              // log file retention in days
  "LogUdpTraffic":      false,           // log every UDP packet to file
  "MonitorMaxMessages": 1000,            // max monitor history (oldest dropped)
  "GroupFilterEnabled": true,            // only show whitelisted group tabs
  "Groups":             ["#20","#262"],  // whitelisted groups (GroupFilterEnabled=true)
  "DataPath":           "C:\\Temp\\MeshcomData", // persistent state directory
  "AutoReplyEnabled":   false,           // send auto-reply on first contact
  "AutoReplyText":      "...",           // auto-reply text; {version} → app version
  "BeaconEnabled":      false,           // send periodic beacon (Bake)
  "BeaconGroup":        "#262",          // target group for beacon
  "BeaconText":         "...",           // beacon text; {version} → app version
  "BeaconIntervalHours": 1,              // beacon interval in hours (minimum 1)
  "TelemetryEnabled":      false,        // send periodic telemetry message
  "TelemetryFilePath":     "/data/telemetry.json", // source JSON file (written by HA, script etc.)
  "TelemetryGroup":        "#262",       // destination: group (#262), broadcast (*), or callsign
  "TelemetryScheduleHours":   "11,15",      // send at 11:00 and 15:00 (comma-separated hours 0–23)
  "TelemetryApiEnabled":   false,        // enable POST /api/telemetry HTTP endpoint
  "TelemetryApiKey":       "",           // optional X-Api-Key for the endpoint (empty = no auth)
  "Language":              "de",         // UI language: "de" (German) or "en" (English)
  "Database": {                          // optional database sink (Beta)
    "Provider":              "none",     // "none" | "mysql" | "influxdb2"
    "MySqlConnectionString": "",         // e.g. "Server=localhost;Database=meshcom;User=mc;Password=secret;"
    "MySqlTableName":        "meshcom_monitor", // created automatically via Settings → Anlegen
    "InfluxUrl":             "http://localhost:8086",
    "InfluxToken":           "",
    "InfluxOrg":             "meshcom",
    "InfluxBucket":          "meshcom",
    "LogInserts":            false       // log every successful write at Information level
  },
  "Webhook": {
    "Enabled":     false,              // send HTTP POST on events
    "Url":         "",                 // target URL (HTTP POST, JSON body)
    "OnMessage":   true,               // fire on incoming chat messages
    "OnPosition":  false,              // fire on incoming position beacons
    "OnTelemetry": false               // fire on incoming telemetry
  },
  "TelemetryMapping": [                  // any number of entries; configure in Settings UI
    { "JsonKey": "aussentemp",  "Label": "🌡",  "Unit": "C",   "Decimals": 1 },
    { "JsonKey": "luftdruck",   "Label": "🧭",  "Unit": "hPa", "Decimals": 1 },
    { "JsonKey": "pv_leistung", "Label": "☀",  "Unit": "kW",  "Decimals": 2 }
  ]
}
```

### LAN access (iPad / mobile)

The `lan` launch profile binds to all network interfaces:

```powershell
# In Visual Studio: select profile "lan" next to the Run button
# Then open in browser on any device in the same network:
http://192.168.x.x:5162
```

### UDP traffic logging

Set `"LogUdpTraffic": true` to write every packet to the log file:

```
[INF] [UDP-RX] 192.168.1.60:1799 {"src_type":"lora","type":"msg","src":"DH1FR-1",...}
[INF] [UDP-TX] 192.168.1.60:1799 {"type":"msg","dst":"DH1FR-1","msg":"Hello"}
```

Filter the log file:
```powershell
Select-String "\[UDP-RX\]" C:\Temp\Logs\MeshcomWebDesk-*.log
Select-String "\[UDP-TX\]" C:\Temp\Logs\MeshcomWebDesk-*.log
```

---

## EXTUDP Protocol

This client communicates with the MeshCom node using the **EXTUDP JSON protocol** defined in the [MeshCom firmware](https://github.com/icssw-org/MeshCom-Firmware).

### Packet types

| `type` | Description | Handled as |
|--------|-------------|------------|
| `msg`  | Chat message (direct, broadcast, group, ACK) | Chat tab + monitor |
| `pos`  | Position beacon with GPS coordinates | MH list + monitor |
| `tele` | Telemetry (temperature, humidity, pressure, battery) | MH list + monitor |

### Example packets

| Direction | Example |
|-----------|---------|
| Registration | `{"type":"info","src":"NOCALL-2"}` |
| Chat RX (direct) | `{"src_type":"lora","type":"msg","src":"NOCALL-1","dst":"NOCALL-2","msg":"Hello{034","msg_id":"5DFC7187","rssi":-95,"snr":12,"firmware":35,"fw_sub":"p"}` |
| Chat RX (relayed) | `{"src_type":"lora","type":"msg","src":"OE1XAR-62,DL0VBK-12,DB0KH-11","dst":"*","msg":"...","rssi":-109,"snr":5}` |
| Position RX | `{"src_type":"lora","type":"pos","src":"DB0MGN-1,...","lat":50.57,"lat_dir":"N","long":10.42,"long_dir":"E","alt":1243,"batt":100,"hw_id":42,"firmware":35,"fw_sub":"p","rssi":-108,"snr":5}` |
| Telemetry RX | `{"src_type":"lora","type":"tele","src":"DB0MGN-1,...","batt":100,"temp1":20.6,"hum":0,"qnh":1031.4}` |
| Chat TX | `{"type":"msg","dst":"NOCALL-1","msg":"Hello"}` |
| ACK RX | `{"src_type":"udp","type":"msg","src":"NOCALL-1","dst":"NOCALL-2","msg":"NOCALL-2  :ack034","msg_id":"A177E139"}` |

### ACK delivery tracking

1. Outgoing message sent → `⏳` pending
2. Node echo arrives with sequence marker `{034}` → stored, indicator changes to `✓`
3. Recipient sends APRS ACK `:ack034` → message marked as delivered `✓✓`

### Hardware IDs (`hw_id`)

| ID | Short name | Hardware |
|----|-----------|---------|
| 1–3 | TLORA-V1/V2 | TTGO LoRa32 |
| 4–6, 12 | T-BEAM | TTGO T-Beam |
| 7 | T-ECHO | LilyGO T-Echo |
| 8 | T-DECK | LilyGO T-Deck |
| 9 | RAK4631 | Wisblock RAK4631 |
| 10–11, 43 | HELTEC-V1/V2/V3 | Heltec WiFi LoRa 32 |
| 39 | EBYTE-E22 | Ebyte LoRa E22 |

> **Note:** Altitude in position packets follows APRS convention (feet). The client converts to metres automatically.

---

## Requirements

> 💡 **No build required:** Ready-to-run binaries for Windows and Linux are available under [Releases](https://github.com/DH1FR/MeshcomWebDesk/releases/latest).

- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) *(ASP.NET Core Runtime, required to run the Windows binary)*
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) *(only required for build from source)*
- A reachable MeshCom node running firmware [v4.35+](https://github.com/icssw-org/MeshCom-Firmware/releases) with EXTUDP enabled
- UDP port 1799 open (Windows Firewall / router)

### ⚠️ Windows SmartScreen warning

When running the `.exe` for the first time, Windows may show **"Windows protected your PC"**.  
This happens because the binary is not code-signed.

**To run it anyway:**
1. Click **"More info"** in the SmartScreen dialog
2. Click **"Run anyway"**

**Alternative:** Right-click the `.exe` → **Properties** → check **"Unblock"** → OK

---

## Build & Run

```powershell
cd MeshcomWebDesk
dotnet run --launch-profile lan    # accessible from all devices in the LAN
# or
dotnet run                         # localhost only
```

Then open `http://localhost:5162` (or `http://<your-ip>:5162` for LAN access).

---

## 🐳 Docker – Deployment on Linux

### Prerequisites

```bash
# Install Docker + Docker Compose plugin (Debian / Ubuntu / Raspberry Pi OS)
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-plugin

# Add current user to the docker group (no sudo needed)
sudo usermod -aG docker $USER
newgrp docker
```

### Initial setup & start

```bash
# Clone repository
git clone https://github.com/DH1FR/MeshcomWebDesk.git
cd MeshcomWebDesk

# Create optional config file (overrides embedded defaults)
cp deploy/appsettings.linux.json appsettings.json
nano appsettings.json          # set DeviceIp, MyCallsign, Groups etc.

# Build image and start container
docker compose up -d --build
```

The container runs in the background and restarts automatically (`restart: unless-stopped`).  
Web interface: **http://\<Linux-IP\>:5162**

> **Note:** `network_mode: host` is required so the container can receive UDP packets from the MeshCom device.

### Changing the configuration

Either edit `appsettings.json` (next to `docker-compose.yml`) or use environment variables in `docker-compose.yml`:

```yaml
environment:
  - Meshcom__DeviceIp=192.168.1.60
  - Meshcom__MyCallsign=NOCALL-1
  - Meshcom__GroupFilterEnabled=true
  - Meshcom__Groups__0=#OE
  - Meshcom__Groups__1=#Test
```

> **Settings saved via the UI** are written to `DataPath/appsettings.override.json` (inside the `./data` volume).  
> The `appsettings.json` mount stays **read-only** (`:ro`) – no container rebuild needed after UI changes.

After any change to `docker-compose.yml` or `appsettings.json`:

```bash
docker compose up -d
```

---

### 🔄 Updating to a new version

Pull the latest changes, rebuild the image and replace the container:

```bash
cd MeshcomWebDesk

# Fetch latest changes
git pull origin master

# Rebuild image and replace container (brief downtime)
docker compose up -d --build

# Remove unused old image (optional)
docker image prune -f
```

### Useful Docker commands

```bash
# Check container status
docker compose ps

# Follow live logs (Ctrl+C to exit)
docker compose logs -f

# Stop container
docker compose stop

# Stop and remove container (config & logs are preserved)
docker compose down

# Stop, remove container and delete image (full reset)
docker compose down --rmi local
```

---

## 💻 Direct installation (without Docker)

Docker is the recommended deployment method. If you prefer not to use Docker, download the binary directly – it is **framework-dependent**, meaning the **.NET 10 Runtime** must be installed on the target machine (no SDK needed).

> 📦 **Download:** [GitHub Releases](https://github.com/DH1FR/MeshcomWebDesk/releases/latest)

---

### Windows

**Prerequisites:**
- [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
# Unzip to e.g. C:\meshcom
Expand-Archive MeshcomWebDesk-vX.Y.Z-win-x64.zip -DestinationPath C:\meshcom

# Edit configuration
notepad C:\meshcom\appsettings.json   # set DeviceIp, MyCallsign

# Start
cd C:\meshcom
.\MeshcomWebDesk.exe
```

Open browser: **http://localhost:5162**

> To run automatically at Windows startup, register as a Windows service:
> ```powershell
> sc.exe create MeshcomWebDesk binPath="C:\meshcom\MeshcomWebDesk.exe" start=auto
> sc.exe start MeshcomWebDesk
> ```

---

### Linux (systemd)
**Prerequisites:**
- [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
# Install .NET 10 Runtime (Debian / Ubuntu / Raspberry Pi OS)
sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-10.0

# Extract archive
mkdir meshcom && tar -xzf MeshcomWebDesk-vX.Y.Z-linux-x64.tar.gz -C meshcom
cd meshcom

# Edit configuration (MyCallsign, DeviceIp etc.)
nano appsettings.json

# Install as systemd service (starts automatically at boot)
sudo bash install.sh
```

Web interface: **http://\<Linux-IP\>:5162**

**Useful commands after installation:**
```bash
journalctl -u meshcom-webclient -f     # live log
systemctl status meshcom-webclient     # status
systemctl restart meshcom-webclient    # restart after config change
```

---

### macOS (Intel & Apple Silicon)

**Prerequisites:**
- [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) for macOS

```bash
# Extract the archive (choose the right binary for your CPU)
# Apple Silicon (M1/M2/M3):
tar -xzf MeshcomWebDesk-vX.Y.Z-osx-arm64.tar.gz -C ~/meshcom

# Intel Mac:
tar -xzf MeshcomWebDesk-vX.Y.Z-osx-x64.tar.gz -C ~/meshcom

cd ~/meshcom

# Edit configuration
nano appsettings.json      # set DeviceIp, MyCallsign

# Allow execution (macOS Gatekeeper)
xattr -d com.apple.quarantine MeshcomWebDesk

# Start
./MeshcomWebDesk
```

Open browser: **http://localhost:5162**

> **macOS Gatekeeper:** If you see *"cannot be opened because it is from an unidentified developer"*,  
> run `xattr -d com.apple.quarantine ./MeshcomWebDesk` once before starting.

---

### Linux (systemd)

The shipped `appsettings.json` contains placeholder values – the following **must** be set before first start:

| Key | Description | Example |
|-----|-------------|---------|
| `MyCallsign` | Your own callsign | `NOCALL-1` |
| `DeviceIp` | IP address of the MeshCom node | `192.168.1.60` |
| `LogPath` | Directory for log files | `./logs` / `/var/log/meshcom` |
| `DataPath` | Directory for persistent state | `./data` / `/opt/meshcom/data` |

---

## ⚖️ Legal

### Copyright
© 2025–2026 Ralf Altenbrand (DH1FR) · All rights reserved.

### Usage
This software is made available for **licensed radio amateurs** for **private, non-commercial use**.  
Commercial use is not permitted without explicit written consent from the author.

### Disclaimer
**Use at your own risk.**  
The author accepts no liability for damages of any kind – including but not limited to damage to hardware, network infrastructure, radio equipment or data loss – caused by the use of this software.  
The software is provided without any warranty.

### License
See [LICENSE](LICENSE)

---

## 🔒 Privacy / Datenschutz

> 🇩🇪 **Deutsch** | 🇬🇧 English below

### 🇩🇪 Datenschutzhinweis

MeshCom WebDesk verarbeitet **Funkamateure-Daten** – Rufzeichen und Nachrichtentexte, die über das MeshCom-Netz übertragen werden.  
Diese Daten sind per se öffentlich (LoRa-Funk ist für jeden empfangbar), können aber personenbezogen im Sinne der DSGVO sein.

#### Was gespeichert werden kann

| Funktion | Gespeicherte Daten | Wo |
|---|---|---|
| **Log-Datei** (`LogUdpTraffic: true`) | Rufzeichen, Nachrichtentexte, GPS-Koordinaten, RSSI/SNR – als Rohdaten jedes UDP-Pakets | `LogPath` auf dem Server |
| **Datenbank** (`Database.Provider != "none"`) | Alle Monitor-Einträge: Rufzeichen, Nachrichtentexte, Zeitstempel, GPS, RSSI, Batterie, Firmware | Externer Datenbankserver |
| **DB-Insert-Log** (`LogInserts: true`) | Vollständige SQL-`INSERT`-Anweisungen mit allen Feldinhalten | `LogPath` auf dem Server |
| **Persistenz** (`DataPath`) | Chat-Verlauf, MH-Liste, Monitor-History, eigene GPS-Position | `DataPath` auf dem Server |

#### Empfehlungen

- **UDP-Traffic-Log** (`LogUdpTraffic`) nur aktivieren, wenn zur Fehlersuche nötig; danach wieder deaktivieren.
- **DB-Insert-Log** (`LogInserts`) nur kurzfristig zur Fehlersuche aktivieren; enthält vollständige Nachrichtentexte und Rufzeichen.
- Die **Datenbankanbindung** ist für den Betrieb im **eigenen, abgesicherten Netz** vorgesehen. Externe Datenbankserver sollten verschlüsselte Verbindungen verwenden (`SslMode=Required` im Connection String).
- **Log-Aufbewahrung** (`LogRetainDays`) auf den minimal notwendigen Zeitraum setzen.
- Der Betrieb dieser Software unterliegt den für Funkamateure geltenden **datenschutzrechtlichen Regelungen** (DSGVO, BDSG, ggf. nationale Amateurfunkgesetze). Der Betreiber ist selbst verantwortlich für die rechtskonforme Nutzung.

---

### 🇬🇧 Privacy Notice

MeshCom WebDesk processes **amateur radio data** – callsigns and message texts transmitted over the MeshCom mesh network.  
This data is inherently public (LoRa radio is receivable by anyone), but may constitute personal data under GDPR.

#### What can be stored

| Feature | Data stored | Location |
|---|---|---|
| **Log file** (`LogUdpTraffic: true`) | Callsigns, message texts, GPS coordinates, RSSI/SNR – raw content of every UDP packet | `LogPath` on the server |
| **Database** (`Database.Provider != "none"`) | All monitor entries: callsigns, message texts, timestamps, GPS, RSSI, battery, firmware | External database server |
| **DB insert log** (`LogInserts: true`) | Full SQL `INSERT` statements with all field values | `LogPath` on the server |
| **Persistence** (`DataPath`) | Chat history, MH list, monitor history, own GPS position | `DataPath` on the server |

#### Recommendations

- Enable **UDP traffic logging** (`LogUdpTraffic`) only for troubleshooting; disable it afterwards.
- Enable **DB insert logging** (`LogInserts`) only briefly for debugging; it contains full message texts and callsigns.
- The **database sink** is intended for use within your **own secured network**. External database servers should use encrypted connections (`SslMode=Required` in the connection string).
- Set **log retention** (`LogRetainDays`) to the minimum period necessary.
- Operation of this software is subject to the **data protection regulations** applicable to amateur radio operators (GDPR, national regulations). The operator is solely responsible for lawful use.

---

© by Ralf Altenbrand (DH1FR) 2025–2026

---

## 📋 Changelog

### v1.6.1
- **fix:** 🗺️ **Map – "Noch keine GPS-Position"** wurde angezeigt obwohl bereits Stationen auf der Karte eingetragen waren – `StateHasChanged()` wird jetzt nach `UpdateMarkersAsync()` aufgerufen
- **fix:** 🗺️ **Map – Station-Strip** ersetzt durch kompakte einzeilige Info-Leiste (`📡 N Station(en) · 📍 Rufzeichen`) – bei vielen Stationen keine unleserliche Liste mehr

### v1.6.0
- **feat:** 🗺️ **Live Map** – interactive Leaflet.js + OpenStreetMap map at `/map`; APRS-style circle markers colour-coded by RSSI; own position as gold diamond; auto-fit bounds; real-time updates
- **feat:** 🔗 **Webhook** – HTTP POST fire-and-forget on incoming messages, position beacons and/or telemetry; configurable URL and triggers in Settings → 🔗 Webhook
- **feat:** 📱 **PWA** – installable as Progressive Web App ("Add to Home Screen"); `manifest.webmanifest`, minimal service worker, Apple meta tags, custom antenna SVG icon
- **feat:** ☕ **Donation link** – PayPal link on the About page (`paypal.me/DH1FR`)
- **docs:** Architecture, Configuration and Changelog updated

### v1.5.0
- **feat:** 🗄️ **Database integration (Beta)** – optional MySQL/MariaDB or InfluxDB 2 sink writes every monitor entry to an external database
- **feat:** **Settings → Datenbank (Beta)** – provider dropdown, connection fields, "Test connection" button with automatic DB/table/bucket creation
- **feat:** **Optional insert logging** (`LogInserts`) – logs the full SQL `INSERT` at Information level; privacy notice shown in Settings UI
- **feat:** **Message length guard** – character counter `X/149` in the chat input, `maxlength="149"` enforced in the browser and server-side warning log when limit is exceeded
- **docs:** 🔒 **Privacy / Datenschutz** section added to README – covers log files, database storage, DB insert log and persistence; includes recommendations for GDPR-compliant operation

### v1.4.3
- **feat:** `TimeOffsetHours` setting – configurable UTC offset for timestamp display (supports half-hour offsets, e.g. `5.5` for IST)
- **fix:** Docker container now runs with `TZ=Europe/Berlin` by default (`tzdata` installed) – `DateTime.Now` correctly reflects MEZ/MESZ including automatic DST switching; `TimeOffsetHours = 0` stays correct in Docker

### v1.4.2
- **feat:** `{version}` placeholder supported in `AutoReplyText` and `BeaconText` – replaced with the running application version at send time
- **feat:** **Test button** for Auto-Reply in Settings – send to any callsign immediately without waiting for an incoming message
- **feat:** Application version shown in the **navigation bar** next to the app title
- **feat:** UDP registration packet (`{"type":"info",...}`) sent on startup now appears in the monitor feed as a `SYS TX` entry
- **feat:** Automatic greeting sent to `DH1FR-2` whenever a new chat tab for that callsign is opened (incoming or manual)

### v1.4.1
- **feat:** `{version}` placeholder supported in `AutoReplyText` and `BeaconText` – replaced with the running application version at send time
- **feat:** **Test button** for Auto-Reply in Settings – send to any callsign immediately without waiting for an incoming message
- **feat:** Application version shown in the **navigation bar** next to the app title
- **feat:** UDP registration packet (`{"type":"info",...}`) sent on startup now appears in the monitor feed as a `SYS TX` entry
- **feat:** Automatic greeting sent to `DH1FR-2` whenever a new chat tab for that callsign is opened (incoming or manual)

### v1.4.0
- **fix:** UDP registration packet no longer sends `dst` or `msg` fields – prevents broadcasting "info" text over the mesh and gateway on startup
- **feat:** Author e-mail address `dh1fr@darc.de` added to About page

### v1.3.0
- Telemetry sender with HTTP POST endpoint (`POST /api/telemetry`)
- Live telemetry preview and instant-send button in Settings
- Home Assistant integration guide
- Auto-split of long telemetry messages (`TM1:` / `TM2:`)
- Configurable telemetry schedule hours

### v1.2.0
- Periodic beacon (Bake) with status indicator
- State persistence (chat tabs, MH list, monitor, own GPS)
- Browser GPS button for own position
- Audio notification for incoming direct messages
- Full relay path display in monitor

### v1.1.0
- ACK delivery tracking (`⏳` / `✓` / `✓✓`)
- MH list with GPS distance, battery level and hardware badge
- Web-based Settings editor
- Multi-language UI (de / en / it / es)

### v1.0.0
- Initial release
