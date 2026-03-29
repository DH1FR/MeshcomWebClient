# MeshCom Web Client

A Blazor Server web application for communicating with a [MeshCom](https://icssw.org/meshcom/) device via UDP (EXTUDP protocol).  
Built with **.NET 10** and **Blazor Interactive Server**.

---

## Features

- 📡 **UDP communication** with a MeshCom node via the EXTUDP JSON protocol
- 💬 **Chat interface** with automatic tab management per conversation partner
- 📢 **Broadcast tab** ("Alle") for CQCQCQ / `*` messages
- 🔀 **Direct messages** – each callsign gets its own tab automatically
- 👥 **Group messages** – group destinations appear as `#<group>` tabs
- 📋 **Raw data feed** – lower pane shows all received UDP frames
- 🔁 **Auto-registration** – sends EXTUDP info packet on startup so the device starts delivering data
- 📝 **File logging** via Serilog with configurable path and retention

---

## Architecture

```
MeshcomWebClient/          ← Blazor Server (ASP.NET Core host)
│  Program.cs              ← DI setup, Serilog, hosted service
│  appsettings.json        ← Configuration (IP, port, callsign, log path)
│
├─ Components/Pages/
│     Chat.razor           ← Main UI (tabs + raw feed)
│
├─ Models/
│     MeshcomMessage.cs    ← Message model (from/to/text/raw)
│     MeshcomSettings.cs   ← Strongly-typed config (IOptions)
│     ChatTab.cs           ← Tab model
│
└─ Services/
      MeshcomUdpService.cs ← BackgroundService: UDP RX/TX + registration
      ChatService.cs       ← Singleton: tab routing + message store

MeshcomWebClient.Client/   ← Blazor WebAssembly client project
```

---

## Configuration

Edit `MeshcomWebClient/appsettings.json`:

```json
"Meshcom": {
  "ListenIp":     "0.0.0.0",       // local bind address
  "ListenPort":   1799,             // local UDP port
  "DeviceIp":     "192.168.1.60",  // MeshCom node IP
  "DevicePort":   1799,             // MeshCom node UDP port
  "MyCallsign":   "DH1FR-2",       // your callsign
  "LogPath":      "C:\\Temp\\Logs",// log file directory
  "LogRetainDays": 30              // log retention in days
}
```

---

## EXTUDP Protocol

| Direction | Format |
|-----------|--------|
| Registration | `{"type":"info","src":"DH1FR-2","dst":"*","msg":"info"}` |
| Receive (RX) | `{"src_type":"lora","type":"msg","src":"DH1FR-1","dst":"DH1FR-2","msg":"Hello{034",...}` |
| Send (TX)    | `{"type":"msg","dst":"DH1FR-1","msg":"Hello"}` |

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A reachable MeshCom node with EXTUDP enabled
- UDP port 1799 open on the host

---

## Build & Run

```powershell
cd MeshcomWebClient
dotnet run
```

Then open `https://localhost:5001` in your browser.

---

## License

MIT – see [LICENSE](LICENSE)
