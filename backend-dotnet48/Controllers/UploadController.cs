using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using ClosedXML.Excel;
using CsvHelper;

namespace BackendDotnet48.Controllers
{
    [RoutePrefix("upload")]
    public class UploadController : ApiController
    {
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Post()
        {
            if (!Request.Content.IsMimeMultipartContent())
                return BadRequest("Invalid file format");

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            var file = provider.Contents.FirstOrDefault();
            if (file == null)
                return BadRequest("No file uploaded");

            var fileName = file.Headers.ContentDisposition.FileName.Trim('\"');
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            object parsed;

            try
            {
                var fileBytes = await file.ReadAsByteArrayAsync();

                if (ext == ".xlsx" || ext == ".xls" || ext == ".xlsm")
                {
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        using (var wb = new XLWorkbook(ms))
                        {
                            var sheetResults = new List<object>();

                            foreach (var worksheet in wb.Worksheets)
                            {
                                var used = worksheet.RangeUsed();
                                if (used == null)
                                {
                                    sheetResults.Add(new
                                    {
                                        name = worksheet.Name,
                                        headers = new string[] { },
                                        rows = new List<List<string>>(),
                                        csv = string.Empty
                                    });
                                    continue;
                                }

                                var allRows = new List<List<string>>();
                                var firstRow = used.RangeAddress.FirstAddress.RowNumber;
                                var lastRow = used.RangeAddress.LastAddress.RowNumber;
                                var firstCol = used.RangeAddress.FirstAddress.ColumnNumber;
                                var lastCol = used.RangeAddress.LastAddress.ColumnNumber;

                                for (int r = firstRow; r <= lastRow; r++)
                                {
                                    var row = new List<string>();
                                    for (int c = firstCol; c <= lastCol; c++)
                                    {
                                        var val = worksheet.Cell(r, c).GetString();
                                        row.Add(string.IsNullOrWhiteSpace(val) ? null : val);
                                    }
                                    allRows.Add(row);
                                }

                                Func<string, bool> CellHasValue = (v) => !(v == null || string.IsNullOrWhiteSpace(v));
                                Func<List<string>, bool> RowIsEmpty = (row) => row.All(c => !CellHasValue(c));

                                var nonEmptyRows = allRows.Where(r => !RowIsEmpty(r)).ToList();

                                if (!nonEmptyRows.Any())
                                {
                                    sheetResults.Add(new
                                    {
                                        name = worksheet.Name,
                                        headers = new string[] { },
                                        rows = new List<List<string>>(),
                                        csv = string.Empty
                                    });
                                    continue;
                                }

                                int maxCols = nonEmptyRows.Max(r => r.Count);
                                int lastIdx = -1;
                                for (int i = 0; i < maxCols; i++)
                                {
                                    if (nonEmptyRows.Any(r => CellHasValue(r[i])))
                                        lastIdx = i;
                                }

                                if (lastIdx == -1)
                                {
                                    sheetResults.Add(new
                                    {
                                        name = worksheet.Name,
                                        headers = new string[] { },
                                        rows = new List<List<string>>(),
                                        csv = string.Empty
                                    });
                                    continue;
                                }

                                var headerRow = nonEmptyRows[0].Take(lastIdx + 1).Select(c => c ?? string.Empty).ToList();
                                var dataRows = nonEmptyRows.Skip(1)
                                    .Select(r => r.Take(lastIdx + 1).Select(c => c ?? string.Empty).ToList())
                                    .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
                                    .ToList();

                                string csvString;
                                using (var sw = new StringWriter())
                                using (var cw = new CsvHelper.CsvWriter(sw, CultureInfo.InvariantCulture))
                                {
                                    foreach (var h in headerRow) cw.WriteField(h);
                                    cw.NextRecord();

                                    foreach (var r in dataRows)
                                    {
                                        foreach (var f in r) cw.WriteField(f);
                                        cw.NextRecord();
                                    }

                                    cw.Flush();
                                    csvString = sw.ToString();
                                }

                                sheetResults.Add(new
                                {
                                    name = worksheet.Name,
                                    headers = headerRow,
                                    rows = dataRows,
                                    csv = csvString
                                });
                            }

                            parsed = new { type = "excel", sheets = sheetResults };
                        }
                    }
                }
                else
                {
                    // CSV / text parsing
                    using (var ms = new MemoryStream(fileBytes))
                    using (var sr = new StreamReader(ms))
                    using (var csv = new CsvReader(sr, CultureInfo.InvariantCulture))
                    {
                        var allRows = new List<List<string>>();
                        while (csv.Read())
                        {
                            var rec = csv.Context.Parser.Record;
                            allRows.Add(rec.ToList());
                        }

                        Func<string, bool> CsvCellHasValue = (v) => !(v == null || string.IsNullOrWhiteSpace(v));
                        Func<List<string>, bool> CsvRowIsEmpty = (row) => row.All(c => !CsvCellHasValue(c));

                        var filtered = allRows.Where(r => !CsvRowIsEmpty(r)).ToList();
                        if (!filtered.Any())
                        {
                            parsed = new
                            {
                                type = "csv_or_text",
                                headers = new string[] { },
                                rows = new List<List<string>>()
                            };
                        }
                        else
                        {
                            int maxCols = filtered.Max(r => r.Count);
                            var norm = filtered.Select(r => r.Concat(Enumerable.Repeat(string.Empty, maxCols - r.Count)).ToList()).ToList();

                            int lastIdx = -1;
                            for (int i = 0; i < maxCols; i++)
                            {
                                if (norm.Any(r => CsvCellHasValue(r[i])))
                                    lastIdx = i;
                            }

                            if (lastIdx == -1)
                            {
                                parsed = new
                                {
                                    type = "csv_or_text",
                                    headers = new string[] { },
                                    rows = new List<List<string>>()
                                };
                            }
                            else
                            {
                                var headers = norm[0].Take(lastIdx + 1).ToList();
                                var rows = norm.Skip(1).Select(r => r.Take(lastIdx + 1).ToList()).Where(r => !CsvRowIsEmpty(r)).ToList();
                                parsed = new { type = "csv_or_text", headers = headers, rows = rows };
                            }
                        }
                    }
                }

                return Ok(parsed);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
