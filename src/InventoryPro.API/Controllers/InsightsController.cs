using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPro.API.Controllers;

/// <summary>Decision-support endpoints: what to reorder, how long stock lasts, where cash is tied up.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Staff")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class InsightsController : ControllerBase
{
    private readonly IInsightsService _insights;

    /// <summary>Creates the controller.</summary>
    public InsightsController(IInsightsService insights) => _insights = insights;

    /// <summary>Inventory value, stock health counts, slow-moving stock value and recent sales.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InventorySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventorySummaryDto>> GetSummary(CancellationToken cancellationToken)
        => Ok(await _insights.GetSummaryAsync(cancellationToken));

    /// <summary>
    /// Products ranked by urgency with days of cover and a suggested order quantity, based on
    /// recent sales velocity, supplier lead time and a safety buffer.
    /// </summary>
    [HttpGet("reorder-suggestions")]
    [ProducesResponseType(typeof(IReadOnlyList<ReorderSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ReorderSuggestionDto>>> GetReorderSuggestions(
        [FromQuery] ReorderQuery query, CancellationToken cancellationToken)
        => Ok(await _insights.GetReorderSuggestionsAsync(query, cancellationToken));

    /// <summary>Daily revenue and order counts for the last N days (1-365), zero-filled.</summary>
    [HttpGet("sales-trend")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SalesTrendPointDto>>> GetSalesTrend(
        [FromQuery] SalesTrendQuery query, CancellationToken cancellationToken = default)
        => Ok(await _insights.GetSalesTrendAsync(query.Days, cancellationToken));
}
