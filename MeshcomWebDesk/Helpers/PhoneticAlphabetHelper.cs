namespace MeshcomWebDesk.Helpers;

/// <summary>
/// Converts amateur radio callsigns into NATO phonetic alphabet spelling.
/// Letters use the international NATO alphabet (identical in all languages).
/// Digits are spoken in the selected UI language.
/// Example: "DH1FR" (de) → "Delta Hotel Ein Foxtrot Romeo"
/// </summary>
public static class PhoneticAlphabetHelper
{
    // NATO phonetic alphabet – international, language-independent
    private static readonly Dictionary<char, string> Nato = new()
    {
        { 'A', "Alfa"     }, { 'B', "Bravo"    }, { 'C', "Charlie"  },
        { 'D', "Delta"    }, { 'E', "Echo"      }, { 'F', "Foxtrot"  },
        { 'G', "Golf"     }, { 'H', "Hotel"     }, { 'I', "India"    },
        { 'J', "Juliett"  }, { 'K', "Kilo"      }, { 'L', "Lima"     },
        { 'M', "Mike"     }, { 'N', "November"  }, { 'O', "Oscar"    },
        { 'P', "Papa"     }, { 'Q', "Quebec"    }, { 'R', "Romeo"    },
        { 'S', "Sierra"   }, { 'T', "Tango"     }, { 'U', "Uniform"  },
        { 'V', "Victor"   }, { 'W', "Whiskey"   }, { 'X', "X-ray"    },
        { 'Y', "Yankee"   }, { 'Z', "Zulu"      },
    };

    // Digits spoken in the respective UI language
    private static readonly Dictionary<string, string[]> Digits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = ["Null", "Eins", "Zwei", "Drei", "Vier", "Fünf", "Sechs", "Sieben", "Acht", "Nein"],
        ["en"] = ["Zero", "One",  "Two",  "Three","Four", "Five", "Six",   "Seven",  "Eight","Nine"],
        ["it"] = ["Zero", "Uno",  "Due",  "Tre",  "Quattro","Cinque","Sei","Sette",  "Otto", "Nove"],
        ["es"] = ["Cero", "Uno",  "Dos",  "Tres", "Cuatro","Cinco","Seis","Siete",  "Ocho", "Nueve"],
    };

    // SSID separator word per language
    private static readonly Dictionary<string, string> Stroke = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = "Strich",
        ["en"] = "Stroke",
        ["it"] = "Tratto",
        ["es"] = "Guión",
    };

    /// <summary>
    /// Spells a callsign phonetically.
    /// Letters → NATO alphabet, digits → language-specific words, '-' → stroke word.
    /// </summary>
    /// <param name="callsign">Callsign, e.g. "DH1FR" or "OE1KBC-2".</param>
    /// <param name="lang">Language code: "de", "en", "it" or "es". Defaults to "de".</param>
    /// <returns>Phonetic spelling as a space-separated string.</returns>
    public static string Spell(string callsign, string lang = "de")
    {
        if (string.IsNullOrWhiteSpace(callsign)) return string.Empty;

        var digits  = Digits.TryGetValue(lang, out var d)  ? d  : Digits["de"];
        var stroke  = Stroke.TryGetValue(lang, out var s)  ? s  : Stroke["de"];

        var parts = new List<string>();
        foreach (var ch in callsign.ToUpperInvariant())
        {
            if (Nato.TryGetValue(ch, out var word))
                parts.Add(word);
            else if (ch >= '0' && ch <= '9')
                parts.Add(digits[ch - '0']);
            else if (ch == '-')
                parts.Add(stroke);
            // other characters (/, .) are silently skipped
        }

        return string.Join(" ", parts);
    }
}
