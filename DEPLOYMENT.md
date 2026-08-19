# Assembly System Local Deployment

Catatan ini untuk menjalankan project Berkut / Assembly System di mesin lokal Windows.

## Lokasi Project

```powershell
cd "D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System"
```

## Port Yang Dipakai

- Backend API: `http://localhost:5241`
- Frontend: `http://127.0.0.1:3002`
- Database: MySQL `127.0.0.1:3306`, database `yanmarassy`

## Stop Project

Jalankan ini sebelum build atau run ulang supaya port dan file `.next\standalone` tidak terkunci.

```powershell
$ports = @(3002, 5241)
foreach ($port in $ports) {
  Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}
```

## Build Backend

```powershell
dotnet build Backend\Web.API\Web.API.csproj --no-restore
```

## Build Frontend

```powershell
cd "D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System\Frontend"
npm run build
Copy-Item -Recurse -Force .next\static .next\standalone\.next\static
Copy-Item -Recurse -Force public .next\standalone\public
cd ..
```

## Run Backend

```powershell
cd "D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System"

$apiCmd = 'cmd.exe /c set ConnectionStrings__DefaultConnection=Server=127.0.0.1;Port=3306;User ID=root;Password=root_native;Database=yanmarassy;SslMode=None;AllowPublicKeyRetrieval=True;&& set FRONTEND_ORIGIN=http://localhost:3000&& set FRONTEND_ORIGIN_ALT=http://127.0.0.1:3000&& set JWT_ISSUER=AssemblySystem&& set JWT_AUDIENCE=AssemblySystem.Frontend&& set JWT_SIGNING_KEY=AssemblySystem-Local-Jwt-Signing-Key-2026-Change-Me&& set JWT_EXPIRES_HOURS=8&& set SWAGGER_ENABLED=true&& set ASPNETCORE_URLS=http://localhost:5241&& dotnet Backend\Web.API\bin\Debug\net8.0\Web.API.dll'

Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
  CommandLine = $apiCmd
  CurrentDirectory = 'D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System'
} | Out-Null
```

## Run Frontend

```powershell
cd "D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System"

$frontCmd = 'cmd.exe /c set NEXT_PUBLIC_API_BASE_URL=&& set SERVER_API_BASE_URL=http://localhost:5241&& set PORT=3002&& node server.js'

Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
  CommandLine = $frontCmd
  CurrentDirectory = 'D:\Project\Private\PT.Yanmar Diesel Indonesia\Assembly System\Frontend\.next\standalone'
} | Out-Null
```

## Quick Check

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5241/swagger -TimeoutSec 10 | Select-Object StatusCode
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:3002/torque-master -TimeoutSec 10 | Select-Object StatusCode
```

Expected result: both return `200`.

## Run Cepat Besok

Urutan cepat ketika user minta "run project berkut":

1. Stop project dengan command `Stop Project`.
2. Build backend.
3. Build frontend dan copy static/public ke standalone.
4. Run backend.
5. Run frontend.
6. Check URL `http://127.0.0.1:3002`.

## Troubleshooting

Jika frontend build gagal dengan `EBUSY .next\standalone`, berarti frontend masih jalan. Jalankan `Stop Project`, lalu build ulang.

Jika login menampilkan `NetworkError when attempting to fetch resource`, biasanya backend belum jalan di port `5241`. Jalankan `Run Backend`, lalu refresh browser.

## Deploy VPS From Windows CMD

Pastikan project sudah ada di VPS, file `.env` sudah dibuat dari `.env.example`, dan remote GitHub Smart Engine Assembly System tersedia. Di project lokal ini remote GitHub bernama `yanmar`.

Jalankan dari folder project Windows:

```cmd
PullBuildRunVps.cmd root@123.123.123.123 /var/www/assembly-system main
```

Dengan MQTT worker:

```cmd
PullBuildRunVps.cmd root@123.123.123.123 /var/www/assembly-system main worker
```

Jika di VPS remote GitHub bernama `origin`, tambahkan parameter remote:

```cmd
PullBuildRunVps.cmd root@123.123.123.123 /var/www/assembly-system main worker origin
```

Clean redeploy: pull dari GitHub, remove container lama, build ulang tanpa cache, lalu up lagi:

```cmd
RedeployCleanVps.cmd root@123.123.123.123 /var/www/assembly-system main
```

Clean redeploy dengan MQTT worker:

```cmd
RedeployCleanVps.cmd root@123.123.123.123 /var/www/assembly-system main worker
```

Jika di VPS remote GitHub bernama `origin`:

```cmd
RedeployCleanVps.cmd root@123.123.123.123 /var/www/assembly-system main worker origin
```
