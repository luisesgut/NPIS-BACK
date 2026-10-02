using NpiManagement.Domain;

namespace NpiManagement.Application.Npis;

/// <summary>
/// Hitos que dan por completada cada etapa de la ruta de 8 etapas del NPI. Es espejo de
/// buildStages() en el frontend (src/lib/npiStages.ts), que pinta la ruta en el detalle: si cambia
/// la regla de una etapa, hay que cambiarla en ambos lados o el listado y el detalle mostrarán
/// avances distintos para el mismo NPI.
///
/// Se expresa sobre banderas y no sobre el agregado para que el listado pueda calcularlas en la
/// misma consulta SQL, sin cargar las colecciones de cada NPI.
/// </summary>
public sealed record NpiStageMilestones(
    bool EngineeringFeasibilityApproved,
    bool DesignFeasibilityApproved,
    bool PrintCardSentToClient,
    bool ClientApprovedPrintCard,
    bool PrintCardLoaded,
    bool PtRegistered,
    bool SapOrderRegistered,
    bool EtLoadedInSispro)
{
    public const int StageCount = 8;

    public int CompletedStages
    {
        get
        {
            bool[] stagesDone =
            [
                true, // 1. Comercial: la solicitud existe desde que se crea el NPI.
                EngineeringFeasibilityApproved,
                DesignFeasibilityApproved,
                PrintCardSentToClient,
                ClientApprovedPrintCard,
                PrintCardLoaded && PtRegistered, // 6. Alta en sistemas: Diseño y Ingeniería, ambos.
                SapOrderRegistered,
                EtLoadedInSispro,
            ];
            return stagesDone.Count(done => done);
        }
    }

    public static NpiStageMilestones FromNpi(Npi npi) => new(
        npi.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria),
        npi.FeasibilityReviews.Any(f => f.Area == AreaType.Diseno),
        npi.PrintCardSentToClientAt is not null,
        npi.PrintCardVersions.Any(v => v.Approvals.Any(a => a.Role == ApproverRole.Client)),
        npi.PrintCardLoadedAt is not null,
        npi.PtRegisteredAt is not null,
        npi.PedidoSapNumber is not null,
        npi.EtLoadedInSisproAt is not null);
}
