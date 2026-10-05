using Microsoft.Xna.Framework;

namespace MonstersGame.Graphics;

/// <summary>
/// Perfil visual reutilizable: color y duracion de un destello de Zona, que
/// forma de rafaga de particulas dispara (<see cref="Particles"/>, Fase 3 del
/// plan de mejoras visuales) y, opcionalmente, que efecto de sonido acompaña
/// el disparo (<see cref="SfxKey"/>, Fase 7 -- vacio = ninguno, para no
/// duplicar los sonidos que ya vienen del mecanismo viejo de diffing en
/// <c>DuelScreen</c>). Varias claves de <see cref="VisualProfileCatalog"/>
/// pueden compartir el mismo perfil.
/// </summary>
public sealed record VisualProfile(Color FlashColor, float Duration, ParticleBurstKind Particles = ParticleBurstKind.None, string SfxKey = "");
