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
        services.AddScoped<GetCaptureHistoryQueryHandler>();
        services.AddScoped<NpiManagement.Application.Bobinas.BobinaCalculatorService>();

        // Fase 5 — comandos del flujo (workflow.md, 26 acciones).
        services.AddScoped<SubmitNpiCommandHandler>();
        services.AddScoped<StartCommercialEditCommandHandler>();
        services.AddScoped<AssignEngineeringCommandHandler>();
        services.AddScoped<AssignDesignCommandHandler>();
        services.AddScoped<ReassignCommercialCommandHandler>();
        services.AddScoped<ApproveEngineeringFeasibilityCommandHandler>();
        services.AddScoped<ReconfirmEngineeringFeasibilityCommandHandler>();
        services.AddScoped<ApproveDesignFeasibilityCommandHandler>();
        services.AddScoped<UpdateDesignClassificationCommandHandler>();
        services.AddScoped<SetDesignInstructionsCommandHandler>();
        services.AddScoped<DeleteAttachmentCommandHandler>();
        services.AddScoped<RequestEngineeringChangesCommandHandler>();
        services.AddScoped<RejectNoFeasibilityCommandHandler>();
        services.AddScoped<RequestDesignChangesCommandHandler>();
        services.AddScoped<RejectDesignNoFeasibilityCommandHandler>();
        services.AddScoped<RejectArtworkCommandHandler>();
        services.AddScoped<SetArtworkObservationCommandHandler>();
        services.AddScoped<SetPrintCardObservationCommandHandler>();
        services.AddScoped<UndoLastActionCommandHandler>();
        services.AddScoped<ReopenNpiCommandHandler>();
        services.AddScoped<CorrectChangeRequestAreaCommandHandler>();
        services.AddScoped<DeleteNpiCommandHandler>();
        services.AddScoped<NpiManagement.Application.Npis.Turns.GetMyTurnsQueryHandler>();
        services.AddScoped<NpiManagement.Application.ClientRecipients.ClientRecipientsService>();
        services.AddScoped<NpiManagement.Application.Npis.Turns.NpiSituationResolver>();
        services.AddScoped<ResolveArtworkRejectionCommandHandler>();
        services.AddScoped<ValidateArtworkCommandHandler>();
        services.AddScoped<CreatePrintCardVersionCommandHandler>();
        services.AddScoped<ReplacePrintCardFileCommandHandler>();
        services.AddScoped<ApprovePrintCardCommandHandler>();
        services.AddScoped<SendPrintCardToClientCommandHandler>();
        services.AddScoped<RequestClientChangesCommandHandler>();
        services.AddScoped<SetClientChangeItemCommandHandler>();
        services.AddScoped<RejectPrintCardCommandHandler>();
        services.AddScoped<RejectByClientCommandHandler>();
        services.AddScoped<LoadPrintCardIntoSystemCommandHandler>();
        services.AddScoped<RegisterSapPtCommandHandler>();
        services.AddScoped<RegisterSapOrderCommandHandler>();
        services.AddScoped<RegisterEtCommandHandler>();
        services.AddScoped<LoadEtIntoSisproCommandHandler>();
        services.AddScoped<LinkSisproQuoteCommandHandler>();
        services.AddScoped<AcceptSisproQuoteCommandHandler>();
        services.AddScoped<RequestSisproQuoteAdjustmentCommandHandler>();
        services.AddScoped<RejectByPriceCommandHandler>();
        services.AddScoped<ConfirmClosureProposalCommandHandler>();
        services.AddScoped<DeclineClosureProposalCommandHandler>();
        services.AddScoped<WithdrawClosureProposalCommandHandler>();
        services.AddScoped<RejectSuppliesTemporaryCommandHandler>();
        services.AddScoped<ResolveSuppliesAgreementCommandHandler>();
        services.AddScoped<RejectSuppliesTotalCommandHandler>();
        services.AddScoped<RequestInformationCommandHandler>();
        services.AddScoped<ResolveClientInformationCommandHandler>();
        services.AddScoped<RespondCommercialInformationCommandHandler>();
        services.AddScoped<RespondEngineeringInformationCommandHandler>();
        // Editar o retirar una solicitud ya enviada (2026-11-04).
        services.AddScoped<EditInformationRequestCommandHandler>();
        services.AddScoped<WithdrawInformationRequestCommandHandler>();
        services.AddScoped<EditChangeRequestCommandHandler>();
        services.AddScoped<WithdrawChangeRequestCommandHandler>();
        services.AddScoped<CancelNpiCommandHandler>();

        // Fase 6 — comentarios y archivos.
        services.AddScoped<AddCommentCommandHandler>();
        services.AddScoped<GetNpiCommentsQueryHandler>();
        services.AddScoped<MarkChatReadCommandHandler>();
        services.AddScoped<GetChatParticipantsQueryHandler>();
        services.AddScoped<GetChatUnreadQueryHandler>();
        services.AddScoped<ChatAccessQueryHandler>();
        services.AddScoped<RegisterAttachmentCommandHandler>();
        services.AddScoped<RegisterLinkCommandHandler>();
        services.AddScoped<GetAttachmentForDownloadQueryHandler>();

        // Fase 7 — tablero de control.
        services.AddScoped<GetDashboardSummaryQueryHandler>();
        // Dashboard por rol (2026-11-09). Las metas se registran en la API desde "Dashboard:Metas".
        services.AddScoped<NpiManagement.Application.Dashboard.DashboardOverviewQueryHandler>();

        // Nombres de cliente: listado para sugerencias y unificar duplicados (2026-11-04).
        services.AddScoped<NpiManagement.Application.Clients.ListClientNamesQueryHandler>();
        services.AddScoped<NpiManagement.Application.Clients.MergeClientNamesCommandHandler>();

        services.AddScoped<IValidator<CreateNpiDraftCommand>, CreateNpiDraftCommandValidator>();
        services.AddScoped<IValidator<UpdateNpiDraftCommand>, UpdateNpiDraftCommandValidator>();

        return services;
    }
}
