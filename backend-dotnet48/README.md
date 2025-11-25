# backend-dotnet48

This is a .NET Framework 4.8 version of the ASP.NET Core backend.

## Endpoints
- `POST /calculate` - JSON body: `{ a, b, op }`
- `POST /upload` - multipart/form-data file upload

## How to run

1. Ensure you have .NET Framework 4.8+ and Visual Studio 2019+ (or dotnet CLI with framework support).
2. From this folder:

```powershell
cd backend-dotnet48
dotnet restore
dotnet run
```

The API will listen on `http://localhost:5000` (ASP.NET Core on .NET 4.8 uses Kestrel).

## Notes
- Uses ClosedXML 0.95.4 and CsvHelper 27.2.1 (compatible with .NET 4.8).
- CORS is configured to allow `http://localhost:4200` for Angular frontend.
