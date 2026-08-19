# Assembly System Backend

Layered .NET 8 backend for the PT. Yanmar Diesel Indonesia Assembly System demo.

## Database

Use MySQL 8 and run the bootstrap from the repository root:

```powershell
mysql -u root -p -e "source Backend/database/yanmarassy.sql"
```

Local connection string example:

```text
Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=yanmarassy;SslMode=None;AllowPublicKeyRetrieval=True;
```

## Run API

```powershell
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=yanmarassy;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Backend\Web.API\Web.API.csproj
```

## Core Endpoints

- `GET|POST /api/leaktester/work-records`
- `POST /api/leaktester/work-records/hmi`
- `GET|POST /api/leaktester/engine-models`
- `GET|POST /api/leaktester/operators`
- `GET|PUT /api/leaktester/settings`
