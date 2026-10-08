using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Domain.Tests;

/// <summary>Actores y fábricas comunes para las pruebas del agregado Npi.</summary>
public static class NpiTestFactory
{
    public static readonly Actor Commercial = new(Guid.NewGuid(), "UsuarioComercial", "Comercial Uno");
    public static readonly Actor EngineeringAdmin = new(Guid.NewGuid(), "AdminIngenieria", "Admin Ingeniería");
    public static readonly Actor Engineer = new(Guid.NewGuid(), "UsuarioIngenieria", "Ingeniero Uno");
    public static readonly Actor DesignAdmin = new(Guid.NewGuid(), "AdminDiseno", "Admin Diseño");
    public static readonly Actor Designer = new(Guid.NewGuid(), "UsuarioDiseno", "Diseñador Uno");

    public static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();

    public static Npi CreateDraft(IClock clock) =>
        Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "Cliente Prueba", NumeroCotizacion = "1001" }, Priority.Media);

    public static Npi CreateActive(IClock clock, int sequenceValue = 1)
    {
        var npi = CreateDraft(clock);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(sequenceValue));
        return npi;
    }

    public static Npi CreateActiveAssigned(IClock clock, int sequenceValue = 1)
    {
        var npi = CreateActive(clock, sequenceValue);
        npi.AssignEngineering(Engineer.UserId, EngineeringAdmin, clock);
        npi.AssignDesign(Designer.UserId, DesignAdmin, clock);
        return npi;
    }

    /// <summary>NPI activo, asignado, con ambas factibilidades aprobadas y artes validadas — listo para crear PrintCard.</summary>
    public static Npi CreateReadyForPrintCard(IClock clock)
    {
        var npi = CreateActiveAssigned(clock);
        npi.ApproveDesignFeasibility(Designer, clock, null);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        npi.ValidateArtwork(Designer, clock);
        return npi;
    }

    /// <summary>NPI con una versión de PrintCard totalmente aprobada (Diseño+Ingeniería+Cliente vía Comercial).</summary>
    public static Npi CreateWithFullyApprovedPrintCard(IClock clock)
    {
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, Guid.NewGuid());
        return npi;
    }

    /// <summary>NPI con el PrintCard cargado en sistema (pero ciclo aún activo — decisión 3).</summary>
    public static Npi CreateWithPrintCardLoaded(IClock clock)
    {
        var npi = CreateWithFullyApprovedPrintCard(clock);
        npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);
        return npi;
    }
}
