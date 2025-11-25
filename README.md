# Angular14-Demo — Calculator Example

This workspace contains a small Angular (v14) frontend and a FastAPI backend wired together to perform simple arithmetic calculations.

Overview
--------
- Frontend: `frontend/` — Angular app. A simple calculator UI posts calculation requests to the backend and shows the result on a separate page.
- Backend: `backend/` — Dotnet app exposing a single POST `/calculate` endpoint that performs the arithmetic and returns the result.

Architecture and data flow
--------------------------
1. The user opens the frontend at `http://localhost:4200/`.
2. The Angular router default route (`''`) renders `CalculatorComponent` (`frontend/src/app/calculator.component.ts/html`).
	 - The component collects two numbers (`a`, `b`) and an operator (`op` set to one of +, -, *, /).
	 - When the user clicks the "= Calculate" button the component sends an HTTP POST to `http://localhost:8000/calculate` with JSON: { a, b, op }.
3. The FastAPI backend (`backend/main.py`) receives the request, computes the result, and returns JSON { answer: number }.
	 - Errors such as division-by-zero or unsupported operator return 400 responses with a descriptive `detail`.
4. On success the frontend navigates to the `/result` route (served by `ResultComponent`) and passes both the original request and the backend response in the router navigation state.
	 - `ResultComponent` displays the expression (e.g. "2 + 3") and the returned answer.

File upload flow (new)
----------------------
1. On the home page (`CalculatorComponent`) there's a file input and an Upload button. The UI accepts any file type but is optimized for `.xlsx` excel files for now.
2. When the user selects a file and clicks Upload, the frontend sends a multipart/form-data POST to `http://localhost:8000/upload` with the selected file under the `file` field.
3. The backend (`/upload` endpoint in `backend/main.py`) attempts to parse the file:
	 - If `openpyxl` is installed and the file has an Excel extension (`.xlsx`, `.xlsm`, ...), it will parse the first worksheet into rows and headers.
	 - Otherwise it will try to decode the file as UTF-8 and parse CSV rows.
	 - If parsing fails the backend returns a 400 error with a helpful message.
4. On successful parse the backend returns JSON like:

```json
{
	"type": "excel",
	"headers": ["Col A", "Col B", ...],
	"rows": [[value1, value2, ...], ...]
}
```

5. The frontend navigates to the `/result` page and passes this parsed JSON as `fileResponse` in the navigation state. The `ResultComponent` will display the parsed data in a table (or show the JSON when headers are missing).

Backend notes
-------------
- The backend requirements now include `openpyxl` (see `backend/requirements.txt`). If you want Excel parsing, install requirements and ensure `openpyxl` is present in the environment.


Files of interest
-----------------
- Frontend
	- `frontend/src/app/calculator.component.ts` — UI + logic to POST calculation requests.
	- `frontend/src/app/calculator.component.html` — calculator inputs, operator select, Calculate and Reset buttons.
	- `frontend/src/app/result.component.ts` & `.html` — shows the result page and consumes router state.
	- `frontend/src/app/app-routing.module.ts` — routes: `''` -> Calculator, `/result` -> Result.
	- `frontend/src/app/app.module.ts` — registers components and imports `FormsModule` and `HttpClientModule`.

- Backend
	- `backend/main.py` — FastAPI app with POST `/calculate`. Uses Pydantic model `CalcRequest` to validate { a, b, op }.
	- `backend/requirements.txt` — dependencies: fastapi, uvicorn, pydantic, etc.

How the calculation endpoint behaves
----------------------------------
- Request: POST /calculate
	- Body: { "a": number, "b": number, "op": "+" | "-" | "*" | "/" }
- Success response: 200 { "answer": number }
- Error responses:
	- 400 with "Division by zero" if op is "/" and b == 0.
	- 400 with "Unsupported operation" if op is not one of the supported ops.

Running the project (recommended order)
---------------------------------------
1) Start the backend (FastAPI)

```powershell
cd c:\Users\Projects\Angular14-Demo\backend
python -m venv .venv; .\.venv\Scripts\Activate.ps1; pip install -r requirements.txt; uvicorn main:app --reload --port 8000
```

This will start the API at http://127.0.0.1:8000. The frontend expects the endpoint at `http://localhost:8000/calculate`.

2) Start the frontend (Angular)

```powershell
cd c:\Users\Projects\Angular14-Demo\frontend
npm install
npm start
```

Open http://localhost:4200/ in your browser. The calculator UI is the default route.

Quick example request (curl)
----------------------------
If you want to test the backend directly while the server is running:

```powershell
curl -X POST http://localhost:8000/calculate -H "Content-Type: application/json" -d '{"a": 5, "b": 7, "op": "+"}'
```

Expected response:

```json
{"answer":12}
```

