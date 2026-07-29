# Hybrid Offline QR Synchronization

Each station runs the WPF application with its own embedded SQLite database. The
central PostgreSQL database is optional at startup: weighing and printing continue
locally when it is unavailable, and pending records synchronize automatically when
connectivity returns.

## Station configuration

Configure these values under **Machine Settings**:

- `QR Prefix`: common company prefix, for example `P`
- `Site Code`: unique plant/site code, for example `S01`
- `Line Code`: production line code, for example `L02`
- `Machine Code`: unique scale/station code, for example `WM03`
- `Emergency Offline Serial Start`: reserve a different range for each station

Example QR:

```text
P-S01-L02-WM03-20260728-12.345-00000042
```

Use one application instance per weighing station. Multiple scales on separate PCs
must use different Site/Line/Machine identities. If several scales are attached to
one PC, run separately configured station installations or extend the serial-port
screen to manage multiple ports.

## Central PostgreSQL / Aiven

Enable central synchronization and paste either:

- an Npgsql key/value connection string; or
- an Aiven `postgres://` / `postgresql://` service URI.

The application creates:

- `qr_serial_state` for atomic global serial block allocation
- `qr_weigh_records` for idempotent central record synchronization

The PostgreSQL user needs permission to create tables/indexes and read/write these
tables. After first initialization, permissions can be restricted to the created
objects.

## Offline behavior

1. The station consumes its locally persisted central serial block.
2. If no central block remains, it uses the configured emergency range.
3. Every record is saved to local SQLite before printing.
4. Reprints retain the original QR and serial.
5. The background worker retries failed central synchronization.

Emergency ranges must be unique per station. Recommended examples:

```text
WM01: 90000001
WM02: 91000001
WM03: 92000001
```

Do not delete a station's SQLite database during production. It contains the local
outbox and the station's current serial-block position.
