# UART - Unified Avalonia Realtime Terminal

A cross-platform serial terminal for embedded engineers, built with Avalonia UI.

Designed as a modern alternative to TeraTerm/PuTTY with first-class macro support and HEX/ASCII display.

## Features

- **Connection management** — Auto-detect COM ports, configure baud rate / data bits / parity / stop bits / flow control
- **HEX / ASCII display** — Toggle between HEX and ASCII view in real time, with timestamps
- **Macro buttons** — Register commands with name, payload, and newline type; execute with one click
- **Macro persistence** — Save/load macros as JSON
- **Send history** — Navigate previous commands with Up/Down keys
- **Session save** — Connection settings and macros are automatically saved and restored on next launch
- **Dark theme** — Fluent dark UI

## Platform support

| OS | Status |
|----|--------|
| Windows | ✅ |
| Linux | ✅ |
| macOS | ✅ (build from source) |

## Download

Pre-built binaries are available on the [Releases](../../releases) page.

| File | Platform |
|------|----------|
| `UART-win-x64.zip` | Windows x64 |
| `UART-linux-x64.tar.gz` | Linux x64 |

No installer required — just extract and run.

> **Linux:** You may need to add your user to the `dialout` group to access serial ports:
> ```bash
> sudo usermod -aG dialout $USER
> # log out and back in
> ```

## Build from source

**Requirements:** .NET 8 SDK

```bash
git clone https://github.com/ya-uhs/uart.git
cd uart
dotnet run --project src/UART
```

**Publish self-contained binary:**

```bash
# Linux
dotnet publish src/UART/UART.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./out

# Windows
dotnet publish src/UART/UART.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./out
```

## Tech stack

| Component | Technology |
|-----------|------------|
| UI framework | Avalonia UI 11 |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Serial communication | System.IO.Ports |
| Runtime | .NET 8 |
| Settings | System.Text.Json |

## Architecture notes

Serial data arrives on a background thread. To keep the UI responsive at 115200 bps and above, received bytes are queued into a `ConcurrentQueue` and flushed to the terminal by a `DispatcherTimer` every 16 ms (~60 fps).

```
SerialPort.DataReceived (background thread)
    └── ConcurrentQueue<byte[]>
            └── DispatcherTimer (16 ms, UI thread)
                    └── terminal TextBlock
```

## License

MIT
