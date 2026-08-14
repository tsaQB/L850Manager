# L850 Manager

L850 Manager is a native Windows desktop utility for the Fibocom L850-GL modem in MBIM composition (`2cb7:0007`). It combines Windows WWAN status with a constrained AT-command surface for live radio data, SMS, LTE band selection, cell scanning, and expert PCI/EARFCN lock workflows.

## Current scope

- Live modem, network, signal, serving-cell, voltage, and carrier overview
- Industrial-console Overview with a live RSRP history chart, metric cards, and dynamic serving/neighbor table
- Separate Radio and Cells workflows with responsive narrow-window layouts
- Restrained Fluent engineering design system with unified native-control accent, dense status tables, and responsive FDD/TDD band policy groups
- SIM SMS inbox polling, send with confirmation, and confirmed deletion
- 3G/4G mode and LTE band selection with readback verification
- Bounded XMCI serving/neighbor scan
- Expert PCI/EARFCN set/clear with NVM barriers, single reset, rediscovery, registration wait, and post-condition checks
- Conditional removable-eSIM detection through the eUICC ISD-R application, with a privacy-safe handoff to Windows eSIM profile management
- Sanitized diagnostics UI

## Safety

The app recognizes only the reviewed L850-GL command surface. Mutations require explicit confirmation. PCI/EARFCN operations do not automatically retry a write or reset after an uncertain outcome. IMEI, IMSI, ICCID, MCC/MNC, TAC, and Cell ID are not written to application logs.

The eSIM item appears only after the inserted removable card accepts the standard ISD-R eUICC application. Profile add, activation, switching, rename, and removal are handed to Windows Settings because Microsoft's direct `ESimProfile` management API is restricted to approved mobile-operator applications. Activation codes, EID, ICCID, QR contents, and profile identifiers are never logged by L850 Manager.

## Requirements

- Windows 10 build 17763 or later; Windows 11 recommended
- .NET 10 SDK for source builds
- Fibocom L850-GL in MBIM USB composition with a responsive CDC ACM AT port

## Build

```powershell
dotnet restore
dotnet build
dotnet run --project L850Manager.csproj
```

The packaged WinUI 3 command-line workflow requires Windows Developer Mode for local launch.

## Status

This repository is under active hardware validation. Do not treat an AT acknowledgement alone as proof that a radio mutation succeeded; the app reports the exact verification stage.
