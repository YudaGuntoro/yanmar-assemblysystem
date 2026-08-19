# Assembly System

Demo assembly work record system for **PT. Yanmar Diesel Indonesia**.

This project is based on the existing leaktest system master and is prepared for an Estic nut runner demo. The current demo keeps the original API/table structure for speed, while the user-facing UI and seed data are adjusted to assembly tightening records.

## Included Modules

- JWT login with the existing authentication flow
- Assembly dashboard and daily OK/NG achievement
- Nut runner work record table
- Product model master data
- Estic nut runner parameter master data
- Operator and user master data
- MySQL schema and demo data

## Default Demo Access

```text
Username: root
Password: root_native
```

## Estic Demo Master

Seeded tool and controller:

- `EH2-H1030-P-T` - ESTIC Handy Nutrunner Pulse Only Pistol Cable on Top Type, `6-30 N.m`, `SQ3/8`
- `EH2-HT45-000N*P` - ESTIC Handy2000LitePlus Controller, Fieldbus, Option I/O

Seeded demo channel:

- `CH-ESTIC-01`
- Torque setting `24.00 N.m`
- Torque limit `22.00 N.m` to `26.00 N.m`
- Angle setting `90 deg`
- Angle limit `75 deg` to `105 deg`

## Database

MySQL 8 is required. From the repository root, run:

```powershell
mysql -u root -p -e "source Backend/database/yanmarassy.sql"
```

The script creates `yanmarassy`, the login user, work record tables, Estic master data, and starter records.

## Run Locally

API:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=yanmarassy;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Backend\Web.API\Web.API.csproj
```

Frontend, in another terminal:

```powershell
Set-Location Frontend
npm install
$env:NEXT_PUBLIC_API_BASE_URL="http://localhost:5241"
npm run dev
```

Open `http://localhost:3000`.

## Demo HMI Payload

The existing HMI endpoint can be used for demo posting:

- `POST /api/leaktester/work-records/hmi`

Example payload:

```json
{
  "engine_model": "TF65R-ASSY",
  "serial_no": "ASSY-DEMO-0100",
  "barcode_scan": "TF65R-ASSY ASSY-DEMO-0100",
  "machine_name": "ESTIC Nut Runner 01",
  "operator": "Demo Operator",
  "channel_no": "CH-ESTIC-01",
  "press_set_low": 22.0,
  "press_set_up": 26.0,
  "pressure_input": 24.3,
  "cycle_time": 8.5,
  "judgement": 2
}
```
