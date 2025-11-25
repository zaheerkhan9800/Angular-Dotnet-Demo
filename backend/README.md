# Backend

Simple FastAPI backend for calculator endpoint.

Install and run:

```powershell
python -m venv .venv; .\.venv\Scripts\Activate.ps1; pip install -r requirements.txt; uvicorn main:app --reload --port 8000
```

Endpoint:
- POST /calculate
  - body: { "a": number, "b": number, "op": "+"|"-"|"*"|"/" }
  - response: { "answer": number }
