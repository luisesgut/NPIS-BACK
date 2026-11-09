using NpiManagement.Domain;

namespace NpiManagement.Application.Npis;

/// <summary>
/// Hitos que dan por completada cada etapa de la ruta de 10 etapas del NPI. Es espejo de
/// buildStages() en el frontend (src/lib/npiStages.ts), que pinta la ruta en el detalle: si cambia
/// la regla de una etapa, hay que cambiarla en ambos lados o el listado y el detalle mostrarán
/// avances distintos para el mismo NPI.
///
/// Se expresa sobre banderas y no sobre el agregado para que el listado pueda calcularlas en la
/// misma consulta SQL, sin cargar las colecciones de cada NPI.
///
/// Las 2 etapas de cotización (SISPRO) se agregaron el 2026-10-27. Un NPI que ya tenía PrintCard
/// entonces nunca pasó por ellas: HasPrintCard las da por cumplidas para que su avance no se quede
/// atorado (es la misma excepción que hace el dominio al exigir la cotización).
/// </summary>
public sealed record NpiStageMilestones(
    bool EngineeringFeasibilityApproved,
    bool DesignFeasibilityApproved,
    bool PrintCardSentToClient,
    bool ClientApprovedPrintCard,
    bool PrintCardLoaded,
    bool PtRegistered,
    bool SapOrderRegistered,
    bool EtLoadedInSispro,
    bool SisproQuoteLinked = false,
    bool SisproQuoteAccepted = false,
    bool HasPrintCard = false)
{
    /// <summary>
    /// 10 etapas con la cotización encendida; 8 con ella apagada (SisproQuoteGate), porque entonces
    /// las 2 de cotización ni se muestran en la ruta. El front pide el interruptor a la API para contar igual.
    /// </summary>
    public static int StageCount => SisproQuoteGate.Enabled ? 10 : 8;

    public int CompletedStages
    {
        get
        {
            var quoteStages = SisproQuoteGate.Enabled;
            bool[] stagesDone =
            [
                true, // 1. Comercial: la solicitud existe desde que se crea el NPI.
                EngineeringFeasibilityApproved,
                DesignFeasibilityApproved,
                quoteStages && (SisproQuoteLinked || SisproQuoteAccepted || HasPrintCard), // 4. Cotización ligada (vigente).
                quoteStages && (SisproQuoteAccepted || HasPrintCard),                      // 5. Precio aceptado por el cliente.
                PrintCardSentToClient,
                ClientApprovedPrintCard,
                PrintCardLoaded && PtRegistered, // 8. Alta en sistemas: Diseño y Ingeniería, ambos.
                SapOrderRegistered,
                EtLoadedInSispro,
            ];
            return stagesDone.Count(done => done);
        }
    }

    /// <summary>Estados de la cotización que cuentan como "ligada": las reemplazadas o invalidadas ya no valen.</summary>
    public static bool CountsAsLinked(SisproQuoteStatus status) =>
        status is not (SisproQuoteStatus.Superseded or SisproQuoteStatus.Invalidated);

    public static NpiStageMilestones FromNpi(Npi npi) => new(
        npi.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria),
        npi.FeasibilityReviews.Any(f => f.Area == AreaType.Diseno),
        npi.PrintCardSentToClientAt is not null,
        npi.PrintCardVersions.Any(v => v.Approvals.Any(a => a.Role == ApproverRole.Client)),
        npi.PrintCardLoadedAt is not null,
        npi.PtRegisteredAt is not null,
        npi.PedidoSapNumber is not null,
        npi.EtLoadedInSisproAt is not null,
        npi.SisproQuotes.Any(q => CountsAsLinked(q.Status)),
        npi.SisproQuoteAccepted,
        npi.PrintCardVersions.Count > 0);
}
