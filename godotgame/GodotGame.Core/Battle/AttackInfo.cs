using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Snapshot inmutable del ultimo ataque resuelto con exito: quien ataco, a
/// quien (si habia objetivo) y que se destruyo. <see cref="DuelEngine.DeclareAttack"/>
/// lo captura con los datos exactos de la Batalla (antes de que las
/// destrucciones vacien las Zonas) para que la presentacion (el juego
/// MonoGame) pueda animar el enfrentamiento sin inferirlo por diferencia de
/// estado ni duplicar ninguna regla de combate.
/// </summary>
public readonly record struct AttackInfo(
    PlayerSide AttackerSide,
    int AttackerZone,
    int DefenderZone,                  // -1 = ataque directo, sin monstruo objetivo
    MonsterCard AttackerCard,
    MonsterCard? DefenderCard,         // null si fue ataque directo
    BattlePosition? DefenderPosition,  // posicion del objetivo al momento del combate (ya volteado si estaba boca abajo); null si fue ataque directo
    bool AttackerDestroyed,
    bool DefenderDestroyed,
    int AttackerValue,                 // ATK efectivo del atacante en el momento del combate (con Equipo/Campo)
    int DefenderValue,                 // ATK o DEF efectivo del defensor (segun su posicion); 0 si fue ataque directo
    bool DefenderWasFaceDown,          // el objetivo estaba boca abajo y se volteo por este ataque
    int DamageToAttacker,
    int DamageToDefender,
    int Serial,                        // contador creciente por duelo: dos ataques con datos identicos (ej. el mismo ataque directo en dos turnos) siguen siendo distintos
    GuardianStar AttackerStar,
    GuardianStar? DefenderStar,        // null si fue ataque directo
    int AttackerStarBonus,             // +500 si la estrella del atacante vencio a la del defensor (ya incluido en AttackerValue)
    int DefenderStarBonus);            // idem para el defensor (ya incluido en DefenderValue)
