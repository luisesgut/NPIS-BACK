using FluentAssertions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Validation;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public sealed class NpiCaptureUnitsValidationTests
{
    private readonly NpiCaptureDtoValidator _validator = new();

    [Theory]
    [InlineData("", "")]
    [InlineData("pulg", "galgas")]
    [InlineData("cm", "mils")]
    [InlineData("mm", "micras")]
    public void Accepts_the_units_offered_by_the_form(string medidas, string calibre) =>
        _validator.Validate(new NpiCaptureDto { UnidadMedidas = medidas, UnidadCalibre = calibre }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("pies", "")]
    [InlineData("", "um")]
    public void Rejects_unknown_units(string medidas, string calibre) =>
        _validator.Validate(new NpiCaptureDto { UnidadMedidas = medidas, UnidadCalibre = calibre }).IsValid.Should().BeFalse();
}
