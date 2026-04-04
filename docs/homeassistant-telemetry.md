# Home Assistant – Telemetrie-Integration

Diese Anleitung zeigt, wie du Wetterdaten (oder beliebige andere Sensordaten) aus **Home Assistant** als Telemetrienachrichten über den MeshCom WebClient ins LoRa-Netzwerk sendest.

## Funktionsweise

```
Home Assistant                WebClient                   MeshCom Node
─────────────────            ──────────────────          ─────────────
Sensordaten               →  liest JSON-Datei         →  sendet Textnachricht
schreibt JSON-Datei          alle X Stunden              ins LoRa-Netz
```

Home Assistant schreibt stündlich eine JSON-Datei mit den aktuellen Messwerten.  
Der WebClient liest diese Datei und sendet eine kompakte Textnachricht an eine konfigurierte Gruppe.

---

## Schritt 1 – Shell Command in Home Assistant

Füge folgenden Block in deine `configuration.yaml` ein:

```yaml
shell_command:
  export_meshcom_telemetry: >-
    python3 -c "
    import json, datetime;
    data = {
      'timestamp':        datetime.datetime.now(datetime.timezone.utc).isoformat(),
      'aussentemp':        {{ states('sensor.tempoutside_2')                              | float(0) }},
      'luftdruck':         {{ states('sensor.weatherstation_rel_pressure')                | float(0) }},
      'luftfeuchtigkeit':  {{ states('sensor.weatherstation_rel_humidity_outside')        | float(0) }},
      'wind_speed':        {{ states('sensor.weatherstation_wind_speed')                  | float(0) }},
      'wind_gust':         {{ states('sensor.weatherstation_wind_gust_2')                 | float(0) }},
      'wind_dir':          {{ states('sensor.weatherstation_wind_dir')                    | float(0) }},
      'regen_24h':         {{ states('sensor.weatherstation_rain_24h')                    | float(0) }},
      'regen_gesamt':      {{ states('sensor.weatherstation_rain_total')                  | float(0) }}
    };
    open('/config/meshcom_telemetry.json', 'w').write(json.dumps(data, indent=2))
    "
```

> **Pfadhinweis:** `/config/` ist der Standard-Konfigurationspfad in Home Assistant OS und
> Home Assistant Docker. Passe den Pfad an, wenn du eine andere Installation verwendest.

---

## Schritt 2 – Automatisierung in Home Assistant

Füge folgende Automatisierung in deine `automations.yaml` ein:

```yaml
- alias: "MeshCom Telemetrie exportieren"
  description: "Wetterdaten jede Stunde für MeshCom WebClient in JSON-Datei schreiben"
  trigger:
    - platform: time_pattern
      minutes: "0"          # jede volle Stunde
  action:
    - service: shell_command.export_meshcom_telemetry
  mode: single
```

Nach einem Neustart von Home Assistant (`Entwicklerwerkzeuge → YAML neu laden → Shell Commands`)
kannst du den Shell Command einmalig manuell ausführen, um die Datei sofort zu erzeugen.

---

## Schritt 3 – Erzeugte JSON-Datei (Beispiel)

```json
{
  "timestamp": "2026-04-04T10:00:00+00:00",
  "aussentemp":       10.7,
  "luftdruck":        1022.3,
  "luftfeuchtigkeit": 86.0,
  "wind_speed":       0.0,
  "wind_gust":        0.0,
  "wind_dir":         180.0,
  "regen_24h":        0.9,
  "regen_gesamt":     0.0
}
```

Die Datei enthält **alle** Messwerte. Im WebClient konfigurierst du, welche davon
(maximal 5) gesendet werden.

---

## Schritt 4 – WebClient Settings konfigurieren

Da MeshCom maximal **5 Telemetriewerte** pro Nachricht unterstützt, wähle die für dich
relevanten Werte aus. Zwei fertige Varianten:

### Variante A – Temperatur, Druck, Feuchte, Wind

```json
"TelemetryEnabled":       true,
"TelemetryFilePath":      "/app/data/meshcom_telemetry.json",
"TelemetryGroup":         "#262",
"TelemetryIntervalHours": 1,
"TelemetryMapping": [
  { "JsonKey": "aussentemp",       "Label": "temp.out", "Unit": "C",   "Decimals": 1 },
  { "JsonKey": "luftdruck",        "Label": "luftdr",   "Unit": "hPa", "Decimals": 1 },
  { "JsonKey": "luftfeuchtigkeit", "Label": "humid",    "Unit": "%",   "Decimals": 0 },
  { "JsonKey": "wind_speed",       "Label": "wind",     "Unit": "m/s", "Decimals": 1 },
  { "JsonKey": "wind_gust",        "Label": "boe",      "Unit": "m/s", "Decimals": 1 }
]
```

**Gesendete Nachricht:** `TM: temp.out=10.7C luftdr=1022.3hPa humid=86% wind=0.0m/s boe=0.0m/s`

---

### Variante B – Temperatur, Druck, Feuchte, Regen, Windrichtung

```json
"TelemetryEnabled":       true,
"TelemetryFilePath":      "/app/data/meshcom_telemetry.json",
"TelemetryGroup":         "#262",
"TelemetryIntervalHours": 1,
"TelemetryMapping": [
  { "JsonKey": "aussentemp",       "Label": "temp.out",  "Unit": "C",    "Decimals": 1 },
  { "JsonKey": "luftdruck",        "Label": "luftdr",    "Unit": "hPa",  "Decimals": 1 },
  { "JsonKey": "luftfeuchtigkeit", "Label": "humid",     "Unit": "%",    "Decimals": 0 },
  { "JsonKey": "regen_24h",        "Label": "rain.24h",  "Unit": "l/m2", "Decimals": 1 },
  { "JsonKey": "wind_dir",         "Label": "wind.dir",  "Unit": "°",    "Decimals": 0 }
]
```

**Gesendete Nachricht:** `TM: temp.out=10.7C luftdr=1022.3hPa humid=86% rain.24h=0.9l/m2 wind.dir=180°`

---

## Dateipfad – Docker-Szenario

Da der WebClient in Docker läuft, muss Home Assistant die JSON-Datei in das
**WebClient-Data-Volume** schreiben, das auf dem Docker-Host gemountet ist.

### Aufbau

```
Docker-Host:               /opt/meshcom/data/meshcom_telemetry.json
  ↕  (Volume-Mount ./data:/app/data)
WebClient-Container:       /app/data/meshcom_telemetry.json
```

### Shell Command Pfad anpassen

Ändere den Pfad im Shell Command auf das Data-Verzeichnis des WebClients auf dem Host:

```yaml
shell_command:
  export_meshcom_telemetry: >-
    python3 -c "
    ...
    open('/opt/meshcom/data/meshcom_telemetry.json', 'w').write(...)
    "
```

### TelemetryFilePath im WebClient

```json
"TelemetryFilePath": "/app/data/meshcom_telemetry.json"
```

---

### Home Assistant selbst in Docker

Falls Home Assistant ebenfalls als Docker-Container läuft, füge in der HA
`docker-compose.yml` oder im Container-Start-Befehl ein zusätzliches Volume hinzu:

```yaml
# Home Assistant docker-compose.yml
services:
  homeassistant:
    volumes:
      - /opt/meshcom/data:/meshcom_export   # WebClient-Data-Verzeichnis einbinden
```

Schreibe dann im Shell Command nach `/meshcom_export/meshcom_telemetry.json`:

```yaml
shell_command:
  export_meshcom_telemetry: >-
    python3 -c "
    ...
    open('/meshcom_export/meshcom_telemetry.json', 'w').write(...)
    "
```

---

## Sensor-Referenz (verwendete Entity-IDs)

| JSON-Key | HA Entity-ID | Einheit | Beschreibung |
|----------|-------------|---------|-------------|
| `aussentemp` | `sensor.tempoutside_2` | °C | Außentemperatur |
| `luftdruck` | `sensor.weatherstation_rel_pressure` | hPa | Relativer Luftdruck |
| `luftfeuchtigkeit` | `sensor.weatherstation_rel_humidity_outside` | % | Außenluftfeuchtigkeit |
| `wind_speed` | `sensor.weatherstation_wind_speed` | m/s | Windgeschwindigkeit |
| `wind_gust` | `sensor.weatherstation_wind_gust_2` | m/s | Windböen |
| `wind_dir` | `sensor.weatherstation_wind_dir` | ° | Windrichtung |
| `regen_24h` | `sensor.weatherstation_rain_24h` | l/m² | Regen letzte 24h |
| `regen_gesamt` | `sensor.weatherstation_rain_total` | l/m² | Regen gesamt |

---

## Weitere Sensoren hinzufügen

Die JSON-Datei kann beliebig viele Werte enthalten. Du musst **keinen WebClient-Code ändern** –
füge einfach neue Felder zur JSON-Datei hinzu und konfiguriere das Mapping in den
WebClient-Settings über `/settings`.

Beispiel PV-Anlage zusätzlich:

```yaml
# In configuration.yaml ergänzen:
      'pv_leistung':   {{ states('sensor.pv_power') | float(0) }},
      'batt_soc':      {{ states('sensor.battery_soc') | float(0) }}
```

```json
// In TelemetryMapping tauschen (max. 5):
{ "JsonKey": "pv_leistung", "Label": "PV", "Unit": "kW", "Decimals": 2 }
```
