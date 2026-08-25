using TaxMate.Model.DTO.Tax;

namespace TaxMate.Service.Interfaces;

public interface IQttCalculationEngine
{
    QttCalculationPreviewResponse Calculate(QttPreviewResponse preview);
}
