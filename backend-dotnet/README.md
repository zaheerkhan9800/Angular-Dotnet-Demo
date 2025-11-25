# backend-dotnet

This is a companion ASP.NET Core Web API that mirrors the existing FastAPI backend in this workspace.

Endpoints:
- POST /calculate  (JSON body: { a, b, op })
- POST /upload     (multipart/form-data file upload)

How to run
1. Ensure you have .NET SDK installed (6.0+, recommended 8.0).
2. From this folder:

```powershell
cd "c:\Users\ZAHEER KHAN\Documents\Angular14-Demo\backend-dotnet"
dotnet restore
dotnet run
```

The API will listen on the default Kestrel URL (usually https://localhost:5001 and http://localhost:5000). If you need to run on a specific port, set `ASPNETCORE_URLS`.

Notes
- The CSV parsing uses CsvHelper. Excel parsing uses ClosedXML (which uses OpenXML under the hood).
- CORS is configured to allow `http://localhost:4200` so the Angular frontend can call it.
- The upload endpoint trims fully-empty rows and trailing empty columns and returns JSON of the form `{ type, headers, rows }`.
