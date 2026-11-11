using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface IInsightsService
{
    Task<InventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReorderSuggestionDto>> GetReorderSuggestionsAsync(
        ReorderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(
        int days, CancellationToken cancellationToken = default);
}
