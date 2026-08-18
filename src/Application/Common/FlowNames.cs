namespace Kart.Analytics.Application.Common;

/// <summary>Business-flow tags for `KartFlowContext.Push`, per kart-conventions.md's per-flow
/// tracing/logging standard and the checkpoint-logging-standard.md taxonomy.</summary>
public static class FlowNames
{
    public const string EventIngestion = "EventIngestion";
    public const string DlqReprocessing = "DlqReprocessing";
    public const string PiiRedaction = "PiiRedaction";
    public const string Reconciliation = "Reconciliation";
    public const string DashboardQuery = "DashboardQuery";
}
