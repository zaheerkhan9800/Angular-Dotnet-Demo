# backend-dotnet — Documentation

This document describes the `backend-dotnet` ASP.NET Core Web API that was added to mirror the existing FastAPI backend. It explains what the project does, how pieces connect, which packages are used, the request/response flows, deployment notes (IIS), and troubleshooting tips.

---

## Project overview

Location: `backend-dotnet/`

Target: .NET 8 (see `BackendDotnet.csproj`)

Main purpose:
- Provide the same functionality as the existing FastAPI backend: simple calculator endpoint and file upload parsing.
- For Excel uploads, parse every worksheet, remove empty rows/columns, and produce per-sheet CSV output (returned to the frontend as CSV strings and preview data).

Primary files of interest
- `Program.cs` — minimal hosting, CORS configuration, controllers registration.
- `Controllers/CalculateController.cs` — POST `/calculate` endpoint (accepts JSON `{ a, b, op }`).
- `Controllers/UploadController.cs` — POST `/upload` endpoint (accepts multipart/form-data `file`). This is the main parsing logic.
- `Models/CalcRequest.cs` — DTO for the calculator endpoint.
- `BackendDotnet.csproj` — project file with package references.
- `README.md` / `documentation.md` — run and deployment instructions.

---

## NuGet packages used

The project uses two main libraries (see the `.csproj` file):

- `ClosedXML` — for reading Excel files (.xlsx / .xls / .xlsm) using OpenXML under the hood. It gives convenient Worksheet/Range APIs.
- `CsvHelper` — for generating CSV strings and reading CSV input when the uploaded file is a CSV/text file.

These packages are selected because they are well-maintained and provide the required Excel/CSV parsing functionality.

---

## Endpoints and behavior

### POST /calculate
- Route: `/calculate`
- Body: JSON `{ "a": number, "b": number, "op": string }` where `op` is one of `+`, `-`, `*`, `/`.
- Behavior: performs the operation and returns `200 OK` with JSON `{ "answer": number }`.
- Errors:
  - Unsupported `op` => `400 Bad Request` with `{ detail: "Unsupported operation" }`.
  - Division by zero => `400 Bad Request` with `{ detail: "Division by zero" }`.
  - Unexpected => `500` with `{ detail: "error message" }`.

### POST /upload
- Route: `/upload`
- Body: `multipart/form-data` with the `file` field containing the uploaded file.

Supported file types and behavior:
- Excel files: `.xlsx`, `.xls`, `.xlsm` (detected by extension). Each worksheet is processed separately.
- CSV/text files: parsed with `CsvHelper`.

Common parsing steps (Excel worksheets)
1. Load workbook from uploaded stream using `new XLWorkbook(stream)`.
2. For each worksheet in `wb.Worksheets`:
   - Determine `used = worksheet.RangeUsed()`; if null, sheet is empty -> returns an empty sheet result.
   - Read all cells in the used range into a 2D list `allRows` where blank/whitespace-only cells are mapped to `null`.
   - Define `CellHasValue(v)` as `v != null && v.Trim() != ""`.
   - Remove fully empty rows: `nonEmptyRows = allRows.Where(row => row has some value)`.
   - Normalize columns and find `lastIdx` = last column index that contains any value across the non-empty rows (this trims trailing empty columns).
   - Use the first non-empty row as `headerRow` (trimmed to `lastIdx+1`). Remaining rows become `dataRows` (trimmed and rows that are empty after trimming are dropped).
   - Generate a CSV string for the sheet using `CsvHelper.CsvWriter` by first writing the header row, then writing each `dataRows` row.
3. Add a sheet result object with these properties: `name`, `headers`, `rows`, `csv` (string).

Response for Excel uploads
- `200 OK` with JSON:

```json
{
  "type": "excel",
  "sheets": [
    {
      "name": "Sheet1",
      "headers": ["Col1","Col2"],
      "rows": [["r1c1","r1c2"],["r2c1","r2c2"]],
      "csv": "Col1,Col2\n r1c1,r1c2\n..."
    },
    {
      "name": "Sheet2",
      "headers": [...],
      "rows": [...],
      "csv": "..."
    }
  ]
}
```

Response for CSV/text uploads
- `200 OK` with JSON similar to the original structure used by the frontend:

```json
{ "type": "csv_or_text", "headers": [...], "rows": [...] }
```

Errors
- Parsing or unexpected exceptions result in `400 Bad Request` (controller catches Exception and returns `{ detail: ex.Message }`).

---

## How the backend connects to the frontend

- The Angular frontend (in this workspace) uploads a file to `/upload` and expects the JSON returned above.
- When the response contains `sheets`, the frontend displays a list of sheets and a preview for the selected sheet; the user can download the CSV string for the selected sheet (the UI triggers a client-side download using the CSV string returned in `csv`).
- For single-file CSV uploads, the frontend uses the headers/rows returned to show a table.

Naming conventions and expectations
- The frontend assumes sheet objects include `name`, `headers`, `rows`, and `csv` (CSV text). The `downloadCsv` logic uses `sheet.csv` to create a Blob and trigger a download.

---

## Deployment & IIS hosting

This project is ready to be published and hosted behind IIS using the ASP.NET Core Module (ANCM). Deploy steps (summary):

1. Build & publish:
```powershell
cd backend-dotnet
dotnet restore
dotnet publish -c Release -o .\publish
```

2. The publish folder will contain `BackendDotnet.dll`, `web.config` and all runtime files. Point an IIS Site's physical path to the `publish` folder and create an Application Pool set to **No Managed Code**.

3. Install prerequisites on the server (if not already present):
   - .NET runtime corresponding to the target framework (and the ASP.NET Core Hosting Bundle for Windows/IIS). The Hosting Bundle installs the IIS module `AspNetCoreModuleV2`.
   - IIS features: `Static Content` if you serve static files. `URL Rewrite` is optional for SPA fallback rules.

4. Web.config
- The publish process usually creates a working `web.config`. If you edit or create one, ensure it is well-formed XML and does not contain extra trailing content (invalid XML will cause `HTTP 500.19` and "Handler Not yet determined").
- Example (recommended while debugging):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\BackendDotnet.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="InProcess" />
  </system.webServer>
</configuration>
```

5. Permissions
- Ensure the IIS AppPool identity (e.g. `IIS_IUSRS` or the configured AppPool user) has read + execute access to the publish folder.

6. Logging & troubleshooting
- Enable `stdoutLogEnabled="true"` temporarily in web.config and create a `logs` folder in `publish` with appropriate permissions. Use the generated `stdout_*` files to see startup errors.
- Check Event Viewer (Windows Logs -> Application) for `IIS AspNetCore Module V2` or `.NET Runtime` errors.
- Common failures are missing Hosting Bundle, malformed `web.config`, or permission issues.

---

## Performance and edge cases

- The controller reads the full workbook into memory. For extremely large Excel files (many MBs per sheet or many sheets), this can be memory intensive. If your users upload very large files, consider:
  - Streaming processing or row-by-row processing.
  - Returning downloadable files (zip) instead of embedding CSV text in JSON to reduce payload size.
  - Limiting maximum upload size in the server or proxy.

- CSV string generation uses `CsvHelper` and returns UTF-8 text without BOM. If consumers require a BOM (e.g., some Excel versions), adjust the encoding when creating the Blob on the client or modify server-side generation.

---

## Example usage (curl)

Upload a file (example using `curl`):

```bash
curl -v -F "file=@./samples/multi-sheet.xlsx" http://localhost:5001/upload
```

Calculator example:

```bash
curl -H "Content-Type: application/json" -d '{"a":4,"b":2,"op":"/"}' http://localhost:5001/calculate
# response: { "answer": 2 }
```

---

## Recommended next improvements

- Add a `GET /heartbeat` or `/health` endpoint (simple and already optionally added in `Program.cs`) so load balancers and health checks can easily validate app is up.
- Provide a `POST /upload/zip` or `GET /upload/{id}/zip` that returns a single ZIP archive containing all sheet CSVs for easier download (recommended for many sheets).
- Add request size limits and validation to the upload endpoint; return a helpful error when clients upload files larger than permitted.
- Add unit tests for parsing logic, particularly edge-cases around empty rows/columns and mixed datatypes.

---

## Where to look in this repo

- `backend-dotnet/Controllers/UploadController.cs` — parsing & CSV generation logic (core of this feature)
- `frontend/src/app/parsed-result.component.*` — shows how the frontend consumes the `sheets` response (sheet list, preview, download)
- `backend-dotnet/BackendDotnet.csproj` — package references
- `backend-dotnet/Program.cs` — CORS and controller registration; useful when you need to allow Angular's origin

---

If you want, I can:
- Add a `GET /download-all` endpoint that returns a ZIP with all sheets (server-side zipping). I can implement that and update the frontend to call it.
- Add more examples of responses and a simplified sequence diagram showing client ↔ server interactions.

Tell me which follow-up you'd like and I'll implement it next.
