**Overview**

This document explains what is different between the original .NET 8 implementation (in `backend-dotnet`) and the .NET Framework 4.8 port in `backend-dotnet48`. It also documents how the 4.8 backend is structured and how the important pieces work together, plus run and troubleshooting instructions.

**High-Level Differences**
- **Target framework:** `backend-dotnet` uses .NET 8 (net8.0); `backend-dotnet48` targets .NET Framework 4.8 (`net48`).
- **Hosting model:** .NET 8 uses ASP.NET Core with Kestrel and the new minimal/top-level `Program.cs`/`Startup` patterns. `backend-dotnet48` uses OWIN self-hosting (`Microsoft.Owin.Hosting`) with ASP.NET Web API 2 (classic System.Web-based stack) because ASP.NET Core runs on .NET Core, not .NET Framework.
- **Project file:** `backend-dotnet` uses packages and implicit features for .NET Core (e.g., `Microsoft.AspNetCore.App` and implicit usings). `backend-dotnet48/BackendDotnet48.csproj` uses framework `<Reference Include="System.Web" />` and explicit NuGet packages for OWIN and Web API (`Microsoft.Owin.Hosting`, `Microsoft.AspNet.WebApi`, `Microsoft.AspNet.WebApi.Owin`, `Microsoft.AspNet.WebApi.Cors`) plus `ClosedXML` and `CsvHelper`.
- **Language features:** .NET Framework 4.8 projects typically use C# 7.3 or lower by default. Nullable reference types and implicit usings (C# 8/9/10 features) were removed/disabled in this port.
- **DI and middleware:** ASP.NET Core uses `IServiceCollection`, `IApplicationBuilder`, and built-in DI. Web API 2 + OWIN uses `HttpConfiguration` plus OWIN pipeline; there's no `IServiceCollection` by default and middleware registration differs.

**Files (what they do & how they differ)**
- `BackendDotnet48.csproj`:
  - Targets `net48`.
  - Adds a framework reference to `System.Web` (so types like `Route` resolve) and NuGet packages: `Microsoft.Owin.Hosting`, `Microsoft.Owin.Host.HttpListener`, `Microsoft.AspNet.WebApi`, `Microsoft.AspNet.WebApi.Owin`, `Microsoft.AspNet.WebApi.Cors`, `ClosedXML`, and `CsvHelper`.
  - No `<Nullable>` or `<ImplicitUsings>` settings (these require newer C# / SDK features).

- `Program.cs` (in this project it contains the Web API `Startup` class):
  - Implements `Startup.Configuration(IAppBuilder app)` for OWIN.
  - Creates an `HttpConfiguration`, enables CORS using `EnableCorsAttribute` from `Microsoft.AspNet.WebApi.Cors`, configures routes with `config.Routes.MapHttpRoute(...)`, and calls `app.UseWebApi(config)` to attach Web API to the OWIN pipeline.
  - Note: In the .NET 8 version the Startup/Program wiring used `WebApplication` / `IWebHostBuilder` and `services.AddCors()` / `app.UseCors()` — those are not available in the classic Web API + OWIN stack.

- `Startup.cs` (in this project it contains `Program.Main`):
  - Contains the console `Main` that starts the OWIN host with `WebApp.Start<Startup>(url)` on `http://localhost:5000/` and waits for user input to stop.
  - In .NET 8 this would have been handled by `dotnet run` / Kestrel with `app.Run()`.

- `Models/CalcRequest.cs`:
  - Plain old CLR object (POCO) with properties `A`, `B`, and `Op`.
  - Model binding in Web API uses `FromBody` or inferred binding on parameter types when the request content is JSON. In ASP.NET Core model binding is similar but uses different attributes (`[FromBody]` exists there too).

- `Controllers/CalculateController.cs`:
  - Uses `System.Web.Http.ApiController` with `[RoutePrefix("calculate")]` and `[HttpPost]` action that returns `IHttpActionResult`.
  - Behavior: accepts a JSON body with `{ "A": number, "B": number, "Op": "+" }` and returns `{ "answer": value }` (or appropriate error codes).
  - In ASP.NET Core this controller used `ControllerBase` / `IActionResult` and attribute routing like `[Route("[controller]")]`. On .NET Framework we use classic Web API routes and `ApiController` semantics.

- `Controllers/UploadController.cs`:
  - Uses `ApiController` and processes `multipart/form-data` via `MultipartMemoryStreamProvider` (no `IFormFile` available). It reads the uploaded file bytes, detects extension, and processes Excel files using `ClosedXML` or CSV using `CsvHelper`.
  - For Excel: uses `XLWorkbook` to iterate worksheets, compute headers and rows, generate CSV string.
  - For CSV: uses `CsvReader` to parse rows and trims trailing empty columns.
  - Returns JSON describing parsed sheets or rows. This logic mirrors the .NET 8 version but uses Web API primitives for reading multipart content.

**Routing and endpoints**
- Two route templates are registered in `Program.cs` OWIN configuration:
  - `api/{controller}/{id}` (default route) — e.g. `http://localhost:5000/api/calculate`
  - `{controller}` (controller-only route) — e.g. `http://localhost:5000/calculate` or `http://localhost:5000/upload`
- Because the port uses OWIN and Web API 2 routes, the endpoints your Angular app expects (`/calculate` and `/upload`) will work (controller-only route). If you used the `/api` route you can also call `/api/calculate`.

**CORS**
- CORS is enabled via `Microsoft.AspNet.WebApi.Cors.EnableCorsAttribute` and applied to `HttpConfiguration` in `Program.cs`:
  - Allowed origin: `http://localhost:4200`
  - Allowed headers & methods: `*`
- This mirrors the intent of the ASP.NET Core `services.AddCors()` setup but uses Web API's CORS extension.

**How to run locally**
1. Ensure you have the .NET 4.8 Developer Pack / targeting pack installed and a compatible SDK (Visual Studio or `dotnet` SDK that supports building `net48` projects). If building from the command line ensure `dotnet` supports building .NET Framework projects (the .NET SDK can build many project types but Visual Studio is often simpler for .NET Framework projects).
2. From the project folder:

```powershell
cd C:\Users\ZAHEER KHAN\Documents\Angular14-Demo\backend-dotnet48
dotnet restore
dotnet run
```

3. The console will show: `Server is running at http://localhost:5000/`. Leave the console open while testing.

**Sample requests**
- Calculate (JSON):

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/calculate" -Method Post -ContentType "application/json" -Body '{"A":10,"B":5,"Op":"+"}'
```

- Upload (multipart form) PowerShell example:

```powershell
#$file = Get-Item "C:\path\to\file.csv"
#$form = @{ file = Get-Item $file }
#Invoke-RestMethod -Uri "http://localhost:5000/upload" -Method Post -Form $form
```

Note: `Invoke-RestMethod`'s `-Form` parameter in PowerShell expects different shapes; the `UploadController` reads multipart bodies, so any client that sends multipart/form-data will work (Postman or an HTML form).

**Troubleshooting & gotchas**
- If the build complains about `Route` or other `System.Web` types, ensure the project contains the framework reference:

```xml
<Reference Include="System.Web" />
```

and that you have the .NET Framework 4.8 targeting pack installed.
- If you saw errors about C# `nullable` or `ImplicitUsings`, those features are not supported for older language versions / targeting frameworks — removed for this project.
- If `dotnet run` fails to start the OWIN host on `http://localhost:5000/`, check for port conflicts or Windows firewall rules.
- If clients cannot access endpoints from the Angular app, verify CORS origin is allowed (`http://localhost:4200`) and that the correct port and path are used.

**Why this approach?**
- ASP.NET Core (used in the .NET 8 project) runs on .NET Core / .NET 5+ and cannot be targeted to the full .NET Framework 4.8. To keep the same API surface and behavior while targeting `net48` we ported to OWIN + ASP.NET Web API 2. This gives similar controller/action programming model and allows use of `ClosedXML` and `CsvHelper` which have .NET Framework builds.

**Next steps / optional improvements**
- Add automated tests for endpoints (xUnit + Web API test host or integration tests using a test client).
- Improve DI by integrating an IoC container (Autofac or SimpleInjector) into Web API + OWIN pipeline if you need more advanced service registration.
- Add logging (e.g., `Serilog` or `NLog`) and configure request logging in OWIN pipeline.

**Files to look at**
- `BackendDotnet48.csproj` — project configuration and package references
- `Program.cs` — Web API + OWIN configuration (routing, CORS)
- `Startup.cs` — `Main` to host the OWIN server
- `Controllers/CalculateController.cs` — calculate endpoint
- `Controllers/UploadController.cs` — upload & parsing endpoint
- `Models/CalcRequest.cs` — request model

If you want, I can also:
- Add example Postman collection for testing endpoints.
- Add small integration tests or a simple local script for uploading a file to the `upload` endpoint.

---

