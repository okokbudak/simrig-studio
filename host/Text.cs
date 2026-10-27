// The few texts of the tray program, in Turkish and English (the rest of Studio has its own language switch).

using System.Globalization;

static class Text
{
    static readonly bool Turkish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

    public static string T(string tr, string en) => Turkish ? tr : en;
}
