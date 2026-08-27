# S2c template extraction contract

- Reference: `G:\ChuaTeThatNghiep6\official-templates\TT-152-2025-TT-BTC.docx`.
- Reference SHA-256: `95C3F4EEBF6027E37078AF36DC68C4B5A05156441A5A4A41B31A810C74CEF11A`.
- Output: `src\TaxMate.Infrastructure\Templates\Tax\2026\mau-s2c-hkd.docx`.
- Preserve the source package styles, theme, fonts, table borders, merged cells,
  header labels, signature block, and page geometry.
- Retain only body elements 81-90: business header table, S2c title, business
  location, period, unit, official ledger table, date and signature paragraphs.
- The ledger has 13 rows and 4 columns. Rows 0-2 are immutable headers. Runtime
  slots are rows 3-12: revenue, total reasonable expenses, expense buckets
  a/b/c/d/đ/e, net income, and PIT payable.
- Buckets c (depreciation) and đ (loan interest) are unsupported in the current
  TaxMate scope and remain zero; they must never be merged into another bucket.
- Runtime-editable header slots: business name, address, tax code, business
  location, period, unit, export date, and representative name.
- The source file remains byte-for-byte unchanged.
