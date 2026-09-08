using KeryxControl.Infrastructure;
using KeryxControl.Models;

namespace KeryxControl.ViewModels;

public sealed class TurzxModelOptionViewModel(string id, TurzxDisplayProfile? profile, string language) : ObservableObject
{
    private string _language = language;
    public string Id { get; } = id;
    public TurzxDisplayProfile? Profile { get; } = profile;
    public string DisplayName => Profile is null
        ? (_language == "en" ? "AUTO — detect connected display" : "AUTO — détecter l’écran connecté")
        : Localize(Profile.DisplayName, _language);

    public void SetLanguage(string language) { _language = language; Raise(nameof(DisplayName)); }

    private static string Localize(string value, string language) => language == "en"
        ? value.Replace("3,5", "3.5").Replace("2,1", "2.1").Replace("2,8", "2.8")
            .Replace("5,0", "5.0").Replace("5,2", "5.2").Replace("8,0", "8.0")
            .Replace("8,8", "8.8").Replace("9,2", "9.2").Replace("12,3", "12.3")
            .Replace("série", "serial", StringComparison.OrdinalIgnoreCase)
            .Replace("validé", "validated", StringComparison.OrdinalIgnoreCase)
        : value;
}
