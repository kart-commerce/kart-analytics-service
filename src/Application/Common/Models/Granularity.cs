namespace Kart.Analytics.Application.Common.Models;

/// <summary>api-contract.yaml's common `Granularity` query parameter — every dashboard/funnel
/// endpoint except `admin-audit` accepts this, default `Day`.</summary>
public enum Granularity
{
    Hour,
    Day,
    Week,
    Month,
}
