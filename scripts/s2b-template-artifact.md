# S2b template extraction contract

- Reference: `G:\ChuaTeThatNghiep6\official-templates\TT-152-2025-TT-BTC.docx`.
- Output: `src\TaxMate.Infrastructure\Templates\Tax\2026\mau-s2b-hkd.docx`.
- Preserve the source package styles, theme, fonts, table borders, merged cells,
  header labels, signature block, and page geometry.
- Retain only body elements 67-73: business header table, S2b title/location/period
  paragraphs, unit paragraph, official ledger table, and signature paragraphs.
- Runtime-editable slots: business name, address, tax code, business location,
  period, ledger rows for each business category, VAT totals, export date, and
  representative name.
- Group ledger sections by business category; VAT rate is an attribute of the
  category and must not be the grouping key.
- The source file remains byte-for-byte unchanged.
