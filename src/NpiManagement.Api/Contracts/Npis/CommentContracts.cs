using System.ComponentModel.DataAnnotations;

namespace NpiManagement.Api.Contracts.Npis;

public sealed record AddCommentRequest([Required] string Text);
