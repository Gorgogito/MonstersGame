using MonstersGame.Core.Entities;

namespace MonstersGame.Core.Battle;

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
    bool DefenderDestroyed);
