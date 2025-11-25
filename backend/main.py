from fastapi import FastAPI, HTTPException, UploadFile, File
from pydantic import BaseModel
from fastapi.middleware.cors import CORSMiddleware
import io
import csv
try:
    import openpyxl
except Exception:
    openpyxl = None

class CalcRequest(BaseModel):
    a: float
    b: float
    op: str

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.post('/calculate')
def calculate(req: CalcRequest):
    a = req.a
    b = req.b
    op = req.op
    try:
        if op == '+':
            ans = a + b
        elif op == '-':
            ans = a - b
        elif op == '*':
            ans = a * b
        elif op == '/':
            if b == 0:
                raise HTTPException(status_code=400, detail="Division by zero")
            ans = a / b
        else:
            raise HTTPException(status_code=400, detail="Unsupported operation")
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

    return {"answer": ans}


@app.post('/upload')
async def upload_file(file: UploadFile = File(...)):
    # Try to parse as Excel (xlsx) if openpyxl is available
    content = await file.read()
    parsed = None
    if openpyxl and file.filename.lower().endswith(('xlsx', 'xlsm', 'xltx', 'xltm')):
        try:
            wb = openpyxl.load_workbook(filename=io.BytesIO(content), data_only=True)
            sheet = wb.active
            all_rows = [list(r) for r in sheet.iter_rows(values_only=True)]

            # helpers to determine emptiness
            def cell_has_value(v):
                return not (v is None or (isinstance(v, str) and v.strip() == ''))

            def row_is_empty(row):
                return all(not cell_has_value(c) for c in row)

            # drop fully-empty rows
            nonempty_rows = [r for r in all_rows if not row_is_empty(r)]

            if not nonempty_rows:
                parsed = {"type": "excel", "headers": [], "rows": []}
            else:
                # normalize row lengths
                max_cols = max(len(r) for r in nonempty_rows)
                norm_rows = [list(r) + [None] * (max_cols - len(r)) for r in nonempty_rows]

                # find last column index that has any value
                def col_has_value(idx):
                    for r in norm_rows:
                        if cell_has_value(r[idx]):
                            return True
                    return False

                last_idx = -1
                for i in range(max_cols):
                    if col_has_value(i):
                        last_idx = i

                if last_idx == -1:
                    parsed = {"type": "excel", "headers": [], "rows": []}
                else:
                    # treat first non-empty row as header
                    header_row = norm_rows[0][:last_idx+1]
                    data_rows = [r[:last_idx+1] for r in norm_rows[1:]]

                    # drop fully-empty data rows after trimming
                    data_rows = [r for r in data_rows if not row_is_empty(r)]

                    headers = [str(c) if c is not None else '' for c in header_row]
                    parsed = {"type": "excel", "headers": headers, "rows": data_rows}
        except Exception as e:
            # fallback to CSV/text parsing
            parsed = None

    if parsed is None:
        # try CSV
        try:
            s = content.decode('utf-8')
            reader = csv.reader(io.StringIO(s))
            all_rows_raw = [r for r in reader]

            def csv_cell_has_value(v):
                return not (v is None or str(v).strip() == '')

            def csv_row_is_empty(row):
                return all(not csv_cell_has_value(c) for c in row)

            filtered = [r for r in all_rows_raw if not csv_row_is_empty(r)]

            if not filtered:
                parsed = {"type": "csv_or_text", "headers": [], "rows": []}
            else:
                max_cols = max(len(r) for r in filtered)
                norm = [list(r) + [''] * (max_cols - len(r)) for r in filtered]

                last_idx = -1
                for i in range(max_cols):
                    for r in norm:
                        if csv_cell_has_value(r[i]):
                            last_idx = i
                            break

                if last_idx == -1:
                    parsed = {"type": "csv_or_text", "headers": [], "rows": []}
                else:
                    headers = [r for r in norm[0][:last_idx+1]]
                    rows = [r[:last_idx+1] for r in norm[1:]]
                    rows = [r for r in rows if not csv_row_is_empty(r)]
                    parsed = {"type": "csv_or_text", "headers": headers, "rows": rows}
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Could not parse uploaded file: {e}")

    return parsed
