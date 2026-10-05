using Microsoft.Data.Sqlite;

namespace MonstersGame.Data.Sqlite.Migrations;

/// <summary>
/// Inserta una fila <c>EffectDefinitions</c> 1:1 por cada uno de los 4
/// <c>EffectId</c> hoy resueltos en codigo (<see cref="Core.Effects.EffectDefinitionResolver"/>
/// ya trae estos mismos 4 por defecto sin necesitar la base de datos; esta
/// migracion los hace tambien administrables desde la base para cuando el
/// editor pueda leerlos/editarlos, sin duplicar logica). Usa el mismo Id como
/// texto: ninguna fila de <c>Cards.EffectId</c> necesita tocarse.
/// </summary>
internal sealed class Migration005_BackfillLegacyEffectDefinitions : ISchemaMigration
{
    public int TargetVersion => 5;

    public void Apply(SqliteConnection connection)
    {
        InsertDefinition(connection, "draw_1", "Robar 1 carta");
        InsertActionStep(connection, "draw_1", 0, "draw_card", """{"Count":"1"}""");

        InsertDefinition(connection, "destroy_target_monster", "Destruir monstruo objetivo");
        InsertTargetSpec(connection, "destroy_target_monster", "MonsterZone");
        InsertActionStep(connection, "destroy_target_monster", 0, "destroy_target_monster", "{}");

        InsertDefinition(connection, "special_summon_from_own_graveyard", "Invocar de Modo Especial desde el Cementerio");
        InsertTargetSpec(connection, "special_summon_from_own_graveyard", "OwnGraveyard");
        InsertActionStep(connection, "special_summon_from_own_graveyard", 0, "special_summon_from_own_graveyard", "{}");

        InsertDefinition(connection, "negate_activation", "Negar activacion");
        InsertActionStep(connection, "negate_activation", 0, "negate_activation", "{}");
    }

    private static void InsertDefinition(SqliteConnection connection, string id, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO EffectDefinitions (Id, Name, Trigger) VALUES ($Id, $Name, 'Any')";
        command.Parameters.AddWithValue("$Id", id);
        command.Parameters.AddWithValue("$Name", name);
        command.ExecuteNonQuery();
    }

    private static void InsertTargetSpec(SqliteConnection connection, string effectDefinitionId, string targetKind)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO EffectTargetSpecs (EffectDefinitionId, TargetKind, FilterId, Required) VALUES ($Id, $TargetKind, NULL, 1)";
        command.Parameters.AddWithValue("$Id", effectDefinitionId);
        command.Parameters.AddWithValue("$TargetKind", targetKind);
        command.ExecuteNonQuery();
    }

    private static void InsertActionStep(SqliteConnection connection, string effectDefinitionId, int stepOrder, string actionKind, string paramsJson)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO EffectActionSteps (EffectDefinitionId, StepOrder, ActionKind, ParamsJson) VALUES ($Id, $StepOrder, $ActionKind, $ParamsJson)";
        command.Parameters.AddWithValue("$Id", effectDefinitionId);
        command.Parameters.AddWithValue("$StepOrder", stepOrder);
        command.Parameters.AddWithValue("$ActionKind", actionKind);
        command.Parameters.AddWithValue("$ParamsJson", paramsJson);
        command.ExecuteNonQuery();
    }
}
