using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;
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

    /// <summary>Un link adjunto al NPI (archivo del PrintCard o evidencia): el dominio exige que sea del propio NPI.</summary>
    public static Guid File(Npi npi, IClock clock, AttachmentContext context = AttachmentContext.PrintCard) =>
        npi.RegisterLink(Designer, clock, "Archivo de prueba", "https://example.com/archivo.pdf", context).Id;

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

    /// <summary>Liga una cotización de SISPRO sin diferencias de estructura (la foto es irrelevante para el dominio).</summary>
    public static SisproQuoteLink LinkQuote(Npi npi, IClock clock, string numero = "678", IReadOnlyList<string>? warnings = null) =>
        npi.LinkSisproQuote(Commercial, clock, numero, "Cotizada", "{}", warnings ?? []);

    /// <summary>NPI activo, asignado, con ambas factibilidades aprobadas y artes validadas, sin cotización todavía.</summary>
    public static Npi CreateFeasibleAwaitingQuote(IClock clock)
    {
        var npi = CreateActiveAssigned(clock);
        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.A);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        npi.ValidateArtwork(Designer, clock);
        return npi;
    }

    /// <summary>Factibilidades, artes validadas y precio aceptado por el cliente — listo para crear PrintCard.</summary>
    public static Npi CreateReadyForPrintCard(IClock clock)
    {
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock);
        npi.AcceptSisproQuote(Commercial, clock, Calendar, null, null);
        return npi;
    }

    /// <summary>NPI con una versión de PrintCard totalmente aprobada (Diseño+Ingeniería+Cliente vía Comercial).</summary>
    public static Npi CreateWithFullyApprovedPrintCard(IClock clock)
    {
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
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
