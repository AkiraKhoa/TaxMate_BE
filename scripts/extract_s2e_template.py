from copy import deepcopy
from pathlib import Path

from docx import Document


SOURCE = Path(r"G:\ChuaTeThatNghiep6\official-templates\TT-152-2025-TT-BTC.docx")
TARGET = Path(
    r"G:\ChuaTeThatNghiep6\TaxMate_BE\src\TaxMate.Infrastructure\Templates\Tax\2026\mau-s2e-hkd.docx"
)


def main() -> None:
    document = Document(SOURCE)
    body = document.element.body
    elements = list(body)
    selected = [deepcopy(elements[index]) for index in range(122, 127)]
    section_properties = deepcopy(body.sectPr)

    for element in list(body):
        body.remove(element)
    for element in selected:
        body.append(element)
    body.append(section_properties)

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    document.save(TARGET)


if __name__ == "__main__":
    main()
