using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

public enum ChoiceKind
{
    /// <summary>Si/No (ej. "¿Activar el efecto de X?", "¿Aplicar este paso opcional?").</summary>
    YesNo,

    /// <summary>Elegir entre <see cref="ChoiceRequest.Min"/> y <see cref="ChoiceRequest.Max"/> cartas de <see cref="ChoiceRequest.Candidates"/>.</summary>
    SelectCards,

    /// <summary>Elegir una de <see cref="ChoiceRequest.Options"/> (ej. en que Campo Invocar, en que posicion).</summary>
    SelectOption,

    /// <summary>Solo mostrar cartas que estaban ocultas (la mano rival, una carta excavada) hasta que el jugador las vea: se responde con "Aceptar".</summary>
    Reveal
}

/// <summary>
/// Para la IA: si las cartas elegidas salen ganando (añadir a la mano,
/// Invocar) o perdiendo (descartar, destruir, desterrar) para su dueño.
/// </summary>
public enum ChoicePurpose { Neutral, Benefit, Harm }

/// <summary>
/// Una decision que el motor necesita de un jugador en medio de la activacion
/// o resolucion de un efecto. Mientras <see cref="Battle.DuelState.PendingChoice"/>
/// no sea null el duelo queda en pausa: la UI (o la IA) responde con
/// <c>DuelEngine.AnswerYesNo</c>/<c>AnswerCards</c>/<c>AnswerOption</c>.
/// </summary>
public sealed class ChoiceRequest
{
    public ChoiceKind Kind { get; }
    public PlayerSide Chooser { get; }
    public string Prompt { get; }
    public Card? Source { get; }
    public IReadOnlyList<CardRef> Candidates { get; }
    public int Min { get; }
    public int Max { get; }
    public IReadOnlyList<string> Options { get; }
    public ChoicePurpose Purpose { get; }

    /// <summary>
    /// Verdadero si las cartas candidatas se muestran boca arriba aunque sean
    /// de la mano rival (ej. "cada jugador elige 1 carta de la mano de su
    /// adversario" despues de mostrar las manos).
    /// </summary>
    public bool RevealCandidates { get; set; }

    /// <summary>
    /// Verdadero si es la ventana de respuesta a un ataque (la opcion 0 es
    /// "No activar nada"; el resto, cartas o efectos que se pueden activar).
    /// </summary>
    public bool IsResponseWindow { get; set; }

    /// <summary>Que se esta eligiendo, para la IA (ej. "declare_name": declarar el nombre de una carta).</summary>
    public string Tag { get; set; } = "";

    public bool Answered { get; private set; }
    public bool Yes { get; private set; }
    public IReadOnlyList<int> Selected { get; private set; } = Array.Empty<int>();
    public int Option { get; private set; } = -1;

    private ChoiceRequest(ChoiceKind kind, PlayerSide chooser, string prompt, Card? source,
        IReadOnlyList<CardRef>? candidates, int min, int max, IReadOnlyList<string>? options, ChoicePurpose purpose)
    {
        Kind = kind;
        Chooser = chooser;
        Prompt = prompt;
        Source = source;
        Candidates = candidates ?? Array.Empty<CardRef>();
        Min = min;
        Max = max;
        Options = options ?? Array.Empty<string>();
        Purpose = purpose;
    }

    public static ChoiceRequest YesNo(PlayerSide chooser, string prompt, Card? source) =>
        new(ChoiceKind.YesNo, chooser, prompt, source, null, 0, 0, null, ChoicePurpose.Neutral);

    public static ChoiceRequest Pick(PlayerSide chooser, string prompt, Card? source, IReadOnlyList<string> options) =>
        new(ChoiceKind.SelectOption, chooser, prompt, source, null, 0, 0, options, ChoicePurpose.Neutral);

    /// <summary>Muestra cartas a <paramref name="viewer"/> (boca arriba) hasta que las acepte.</summary>
    public static ChoiceRequest Reveal(PlayerSide viewer, string prompt, Card? source, IReadOnlyList<CardRef> cards) =>
        new(ChoiceKind.Reveal, viewer, prompt, source, cards, 0, 0, null, ChoicePurpose.Neutral) { RevealCandidates = true };

    public static ChoiceRequest Cards(PlayerSide chooser, string prompt, Card? source, IReadOnlyList<CardRef> candidates, int min, int max, ChoicePurpose purpose)
    {
        max = Math.Min(max, candidates.Count);
        min = Math.Min(min, max);
        return new ChoiceRequest(ChoiceKind.SelectCards, chooser, prompt, source, candidates, min, max, null, purpose);
    }

    /// <summary>Las cartas elegidas (solo <see cref="ChoiceKind.SelectCards"/>).</summary>
    public IReadOnlyList<CardRef> SelectedCards => Selected.Select(i => Candidates[i]).ToList();

    /// <summary>Verdadero si no hay nada que decidir (se responde sola sin preguntarle a nadie).</summary>
    public bool IsForced => Kind == ChoiceKind.SelectCards && (Max == 0 || Candidates.Count <= Min);

    public void Acknowledge() => Answered = true;

    public void AnswerYesNo(bool yes) { Yes = yes; Answered = true; }

    public void AnswerOption(int option) { Option = option; Answered = true; }

    public void AnswerCards(IReadOnlyList<int> indices) { Selected = indices.ToList(); Answered = true; }

    /// <summary>Valida una respuesta de cartas: indices distintos, en rango y entre Min y Max.</summary>
    public string? ValidateCards(IReadOnlyList<int> indices)
    {
        var distinct = indices.Distinct().ToList();
        if (distinct.Count != indices.Count) return "No puedes elegir la misma carta dos veces.";
        if (distinct.Any(i => i < 0 || i >= Candidates.Count)) return "Seleccion invalida.";
        if (distinct.Count < Min || distinct.Count > Max)
            return Min == Max ? $"Debes elegir exactamente {Min} carta(s)." : $"Debes elegir entre {Min} y {Max} carta(s).";
        return null;
    }
}
