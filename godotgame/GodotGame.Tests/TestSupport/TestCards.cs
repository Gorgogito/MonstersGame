using GodotGame.Core.Entities;

namespace GodotGame.Tests.TestSupport;

/// <summary>
/// Catalogo de cartas de monstruo controlado y estable, independiente del
/// contenido real de <c>Data/Cards/cards.json</c>, para que los tests no se
/// rompan si el catalogo del juego cambia.
/// </summary>
internal static class TestCards
{
    public static readonly MonsterCard Level4Strong =
        new(1001, "Guerrero de Prueba", attack: 1500, defense: 1200, level: 4, "Warrior", MonsterAttribute.Earth);

    public static readonly MonsterCard Level4Weak =
        new(1002, "Recluta de Prueba", attack: 1000, defense: 1000, level: 4, "Warrior", MonsterAttribute.Earth);

    public static readonly MonsterCard Level6OneTribute =
        new(1003, "Bestia de Prueba", attack: 2200, defense: 1800, level: 6, "Beast", MonsterAttribute.Fire);

    public static readonly MonsterCard Level8TwoTributes =
        new(1004, "Dragon de Prueba", attack: 3000, defense: 2500, level: 8, "Dragon", MonsterAttribute.Light);

    public static readonly MonsterCard ZeroAttack =
        new(1005, "Larva de Prueba", attack: 0, defense: 0, level: 1, "Insect", MonsterAttribute.Earth);

    public static readonly MonsterCard HighDefense =
        new(1006, "Muralla de Prueba", attack: 300, defense: 2500, level: 4, "Rock", MonsterAttribute.Earth);

    public static readonly MonsterCard Level2Fodder =
        new(1007, "Sirviente de Prueba", attack: 400, defense: 400, level: 2, "Fiend", MonsterAttribute.Dark);

    public static readonly MonsterCard FusionMaterialA =
        new(2001, "Material Dragon de Prueba", attack: 1600, defense: 1200, level: 4, "Dragon", MonsterAttribute.Wind);

    public static readonly MonsterCard FusionMaterialB =
        new(2002, "Material Trueno de Prueba", attack: 1200, defense: 800, level: 4, "Thunder", MonsterAttribute.Light);

    public static readonly MonsterCard FusionResult =
        new(2003, "Fusion de Prueba", attack: 2800, defense: 2000, level: 7, "Dragon", MonsterAttribute.Wind,
            category: MonsterCategory.Fusion);

    public static readonly FusionRecipe ValidFusionRecipe =
        new(FusionMaterialA.Id, FusionMaterialB.Id, FusionResult.Id);

    public static readonly SpellCard NormalSpell =
        new(3001, "Magia Normal de Prueba", SpellSubType.Normal, description: "Efecto de un solo uso.");

    public static readonly SpellCard ContinuousSpell =
        new(3002, "Magia Continua de Prueba", SpellSubType.Continuous);

    public static readonly SpellCard EquipSpell =
        new(3003, "Magia de Equipo de Prueba", SpellSubType.Equip, equipAttackModifier: 500, equipDefenseModifier: 300);

    public static readonly SpellCard FieldSpellA =
        new(3004, "Campo de Prueba A", SpellSubType.Field);

    public static readonly SpellCard FieldSpellB =
        new(3005, "Campo de Prueba B", SpellSubType.Field);

    public static readonly SpellCard QuickPlaySpell =
        new(3006, "Magia de Juego Rapido de Prueba", SpellSubType.QuickPlay);

    public static readonly SpellCard RitualSpell =
        new(3007, "Magia de Ritual de Prueba", SpellSubType.Ritual);

    /// <summary>Efecto registrado "draw_1": roba 1 carta, sin objetivo.</summary>
    public static readonly SpellCard DrawEffectSpell =
        new(3008, "Impulso de Prueba", SpellSubType.Normal, effectId: "draw_1");

    /// <summary>Efecto registrado "special_summon_from_own_graveyard": requiere objetivo.</summary>
    public static readonly SpellCard ReviveEffectSpell =
        new(3009, "Resurreccion de Prueba", SpellSubType.Normal, effectId: "special_summon_from_own_graveyard");

    /// <summary>Magia de Ritual vinculada a <see cref="RitualMonster"/> (Nivel 6 requerido).</summary>
    public static readonly SpellCard RitualSummonSpell =
        new(3010, "Rito de Prueba", SpellSubType.Ritual, ritualMonsterId: 5001, requiredRitualLevel: 6);

    public static readonly TrapCard NormalTrap =
        new(4001, "Trampa Normal de Prueba", TrapSubType.Normal);

    public static readonly TrapCard ContinuousTrap =
        new(4002, "Trampa Continua de Prueba", TrapSubType.Continuous);

    public static readonly TrapCard CounterTrap =
        new(4003, "Trampa de Contraefecto de Prueba", TrapSubType.Counter);

    /// <summary>Efecto registrado "destroy_target_monster": requiere objetivo.</summary>
    public static readonly TrapCard DestroyEffectTrap =
        new(4004, "Trampa Destructora de Prueba", TrapSubType.Normal, effectId: "destroy_target_monster");

    /// <summary>Efecto registrado "negate_activation": niega el eslabon al que responde.</summary>
    public static readonly TrapCard NegateEffectTrap =
        new(4005, "Contraefecto de Prueba", TrapSubType.Counter, effectId: "negate_activation");

    /// <summary>Monstruo de Ritual (solo Invocable via <see cref="RitualSummonSpell"/>).</summary>
    public static readonly MonsterCard RitualMonster =
        new(5001, "Monstruo de Ritual de Prueba", attack: 2500, defense: 2000, level: 6, "Fairy", MonsterAttribute.Light,
            category: MonsterCategory.Ritual);

    /// <summary>Monstruo de Efecto con Efecto de Volteo "draw_1".</summary>
    public static readonly MonsterCard FlipEffectMonster =
        new(5002, "Centinela Volteado de Prueba", attack: 800, defense: 600, level: 3, "Reptile", MonsterAttribute.Water,
            category: MonsterCategory.Effect, effectId: "draw_1");

    public static IEnumerable<Card> All => new Card[]
    {
        Level4Strong, Level4Weak, Level6OneTribute, Level8TwoTributes,
        ZeroAttack, HighDefense, Level2Fodder, FusionMaterialA, FusionMaterialB, FusionResult,
        NormalSpell, ContinuousSpell, EquipSpell, FieldSpellA, FieldSpellB, QuickPlaySpell, RitualSpell,
        DrawEffectSpell, ReviveEffectSpell, RitualSummonSpell,
        NormalTrap, ContinuousTrap, CounterTrap, DestroyEffectTrap, NegateEffectTrap,
        RitualMonster, FlipEffectMonster
    };
}
