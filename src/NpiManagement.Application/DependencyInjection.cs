using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Npis.Validation;

namespace NpiManagement.Application;

/// <summary>Composición de dependencias de Application — casos de uso y sus validadores.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateNpiDraftCommandHandler>();
        services.AddScoped<UpdateNpiDraftCommandHandler>();
        services.AddScoped<GetNpiByIdQueryHandler>();
        services.AddScoped<ListNpisQueryHandler>();
        services.AddScoped<GetNpiAuditQueryHandler>();
        services.AddScoped<GetNpiTimerEventsQueryHandler>();
        services.AddScoped<GetNpiAssigneesQueryHandler>();

        // Fase 5 — comandos del flujo (workflow.md, 26 acciones).
        services.AddScoped<SubmitNpiCommandHandler>();
        services.AddScoped<AssignEngineeringCommandHandler>();
        services.AddScoped<AssignDesignCommandHandler>();
        services.AddScoped<ReassignCommercialCommandHandler>();
        services.AddScoped<ApproveEngineeringFeasibilityCommandHandler>();
        services.AddScoped<ApproveDesignFeasibilityCommandHandler>();
        services.AddScoped<RequestEngineeringChangesCommandHandler>();
        services.AddScoped<RejectNoFeasibilityCommandHandler>();
        services.AddScoped<ValidateArtworkCommandHandler>();
        services.AddScoped<CreatePrintCardVersionCommandHandler>();
        services.AddScoped<ApprovePrintCardCommandHandler>();
        services.AddScoped<SendPrintCardToClientCommandHandler>();
        services.AddScoped<RequestClientChangesCommandHandler>();
        services.AddScoped<RejectByClientCommandHandler>();
        services.AddScoped<LoadPrintCardIntoSystemCommandHandler>();
        services.AddScoped<RegisterSapPtCommandHandler>();
        services.AddScoped<RegisterSapOrderCommandHandler>();
        services.AddScoped<RegisterEtCommandHandler>();
        services.AddScoped<LoadEtIntoSisproCommandHandler>();
        services.AddScoped<RejectSuppliesTemporaryCommandHandler>();
        services.AddScoped<ResolveSuppliesAgreementCommandHandler>();
        services.AddScoped<RejectSuppliesTotalCommandHandler>();
        services.AddScoped<RequestInformationCommandHandler>();
        services.AddScoped<ResolveClientInformationCommandHandler>();
        services.AddScoped<RespondCommercialInformationCommandHandler>();
        services.AddScoped<CancelNpiCommandHandler>();

        // Fase 6 — comentarios y archivos.
        services.AddScoped<AddCommentCommandHandler>();
        services.AddScoped<GetNpiCommentsQueryHandler>();
        services.AddScoped<RegisterAttachmentCommandHandler>();
        services.AddScoped<GetAttachmentForDownloadQueryHandler>();

        // Fase 7 — tablero de control.
        services.AddScoped<GetDashboardSummaryQueryHandler>();

        services.AddScoped<IValidator<CreateNpiDraftCommand>, CreateNpiDraftCommandValidator>();
        services.AddScoped<IValidator<UpdateNpiDraftCommand>, UpdateNpiDraftCommandValidator>();

        return services;
    }
}
