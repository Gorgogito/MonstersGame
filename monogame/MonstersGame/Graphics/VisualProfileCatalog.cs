using Microsoft.Xna.Framework;
using MonstersGame.Core.Battle;
using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;

namespace MonstersGame.Graphics;

/// <summary>
/// Catalogo en codigo de <see cref="VisualProfile"/>, indexado por una clave
/// de texto estable -- mismo patron que <c>EffectActionCatalog</c> en Core:
/// el motor no conoce estas claves, solo <see cref="ResolveKey"/> (aqui, en
/// la capa de presentacion) las deriva de un <see cref="DuelEvent"/>. Agregar
/// un perfil realmente nuevo (un color/duracion/rafaga distintos) es codigo;
/// asociar un evento (o una carta concreta) a un perfil existente es dato.
/// </summary>
public static class VisualProfileCatalog
{
    public static readonly VisualProfile Default = new(Color.White, 0.4f);

    private static readonly Dictionary<string, VisualProfile> Profiles = new()
    {
        ["summon.normal"] = new VisualProfile(Color.White, 0.4f, ParticleBurstKind.SummonSparkle),
        ["summon.flip"] = new VisualProfile(new Color(255, 220, 120), 0.4f, ParticleBurstKind.SummonSparkle),
        ["summon.special"] = new VisualProfile(new Color(150, 220, 255), 0.45f, ParticleBurstKind.SummonBeam),
        ["summon.fusion"] = new VisualProfile(new Color(200, 120, 255), 0.6f, ParticleBurstKind.FusionSwirl, SfxKey: "fusion"),
        ["summon.ritual"] = new VisualProfile(new Color(255, 215, 80), 0.6f, ParticleBurstKind.RitualPillar, SfxKey: "ritual"),
        ["destroy.effect"] = new VisualProfile(new Color(255, 90, 90), 0.4f, ParticleBurstKind.DestroyDissolve),
        ["destroy.cost"] = new VisualProfile(new Color(180, 180, 180), 0.35f, ParticleBurstKind.DestroyDissolve),
        ["destroy.battle"] = new VisualProfile(new Color(255, 140, 60), 0.5f, ParticleBurstKind.DestroyShatter, SfxKey: "destroy_battle"),
        ["spelltrap.activate"] = new VisualProfile(new Color(120, 200, 255), 0.4f, ParticleBurstKind.SpellActivate),
        ["field.change"] = new VisualProfile(new Color(120, 255, 150), 0.5f),
        ["statmodifier.equip"] = new VisualProfile(new Color(120, 255, 180), 0.45f),
    };

    public static VisualProfile Resolve(string key) =>
        Profiles.TryGetValue(key, out var profile) ? profile : Default;

    /// <summary>Deriva la clave de perfil visual generica de un evento del motor (una por tipo/variante de evento, sin mirar la carta concreta).</summary>
    public static string KeyFor(DuelEvent evt) => evt switch
    {
        MonsterSummonedEvent { Kind: SummonKind.Flip } => "summon.flip",
        MonsterSummonedEvent { Kind: SummonKind.Special } => "summon.special",
        MonsterSummonedEvent => "summon.normal",
        MonsterDestroyedEvent { Cause: DestructionCause.Cost } => "destroy.cost",
        MonsterDestroyedEvent { Cause: DestructionCause.Battle } => "destroy.battle",
        MonsterDestroyedEvent => "destroy.effect",
        FusionPerformedEvent => "summon.fusion",
        RitualPerformedEvent => "summon.ritual",
        SpellTrapActivatedEvent => "spelltrap.activate",
        FieldChangedEvent => "field.change",
        StatModifierAppliedEvent => "statmodifier.equip",
        _ => "default"
    };

    /// <summary>
    /// Igual que <see cref="KeyFor"/>, pero primero busca si la carta concreta
    /// involucrada eligio su propio perfil (<c>EffectDefinition.VisualProfileKey</c>
    /// para un efecto compuesto activado/volteado, <c>FieldType.VisualEffectsKey</c>
    /// para una Carta de Campo activa) -- solo si esta configurado, si no cae
    /// al generico por tipo de evento. Necesita <paramref name="state"/> para
    /// resolver el Monstruo de Campo/EffectId real en el momento del evento.
    /// </summary>
    public static string ResolveKey(DuelEvent evt, DuelState state)
    {
        string? overrideKey = evt switch
        {
            FieldChangedEvent e => (state.GetPlayer(e.Side).FieldZone?.Card as SpellCard)?.FieldType?.VisualEffectsKey,
            SpellTrapActivatedEvent e => EffectDefinitionResolver.GetDefinition(EffectIdOf(e.Card))?.VisualProfileKey,
            MonsterSummonedEvent { Kind: SummonKind.Flip } e => EffectDefinitionResolver.GetDefinition(e.Card.EffectId)?.VisualProfileKey,
            _ => null
        };

        return string.IsNullOrEmpty(overrideKey) ? KeyFor(evt) : overrideKey;
    }

    private static string EffectIdOf(Card card) => card switch
    {
        SpellCard s => s.EffectId,
        TrapCard t => t.EffectId,
        _ => ""
    };
}
