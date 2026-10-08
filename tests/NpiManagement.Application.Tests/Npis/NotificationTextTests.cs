using FluentAssertions;
using NpiManagement.Application.Npis.Commands;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public sealed class NotificationTextTests
{
    [Fact]
    public void Changes_requested_tells_how_many_fields_and_what_engineering_said() =>
        NotificationText.ChangesRequested("NPI-0007", 2, "El ancho no coincide").Should()
            .Be("Ingeniería solicitó cambios en el NPI NPI-0007 (2 campos por corregir): El ancho no coincide");

    [Fact]
    public void Resubmitted_tells_how_many_fields_changed_and_the_comment() =>
        NotificationText.Resubmitted("NPI-0007", 1, "Corregí el ancho").Should()
            .Be("Comercial reenvió el NPI NPI-0007 con 1 campo actualizado. Comentario: Corregí el ancho");

    [Fact]
    public void Long_texts_are_clipped_to_fit_the_notification() =>
        NotificationText.ChangesRequested("NPI-0007", 0, new string('x', 2000)).Length.Should().BeLessThan(500);
}
