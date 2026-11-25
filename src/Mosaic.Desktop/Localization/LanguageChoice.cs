using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Localization;

// The language this process shows. Owned by App: a screen asks for another one here, and the window follows.
public sealed class LanguageChoice(SettingsStore store, LanguageOption start)
{
    public LanguageOption Chosen { get; private set; } = start;

    public event EventHandler<LanguageOption>? Changed;

    public void Choose(LanguageOption language)
    {
        // also reached when a screen opens on the language already shown, where saving would turn the OS language into a stored choice
        if (string.Equals(Chosen.Code, language.Code, StringComparison.Ordinal))
        {
            return;
        }

        Chosen = language;

        store.Write(store.Read() with { LanguageCode = language.Code });

        Changed?.Invoke(this, language);
    }
}
