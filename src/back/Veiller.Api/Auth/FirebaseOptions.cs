using System.ComponentModel.DataAnnotations;

namespace Veiller.Api.Auth;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    [Required]
    public string ProjectId { get; set; } = "";

    [Required]
    public string WebApiKey { get; set; } = "";
}
