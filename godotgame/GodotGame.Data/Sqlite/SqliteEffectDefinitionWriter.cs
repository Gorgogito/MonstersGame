using System.Text.Json;
using Microsoft.Data.Sqlite;
using GodotGame.Data.Loaders;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Escribe un <c>EffectDefinition</c> compuesto (Trigger + Objetivo + pasos de
/// Accion) desde el editor de cartas. A diferencia de <see cref="SqliteRequirementWriter"/>,
/// aqui SI se borra la definicion anterior con el mismo Id antes de re-crearla
/// (<c>ON DELETE CASCADE</c> se lleva sus condiciones/objetivo/pasos): el Id
/// es estable por carta (<c>card_{Id}_effect</c>, ver <see cref="Dtos.CardDto.ComposeCustomEffect"/>),
/// asi que "guardar de nuevo" siempre significa "reemplazar la version
/// anterior de esta misma carta", no crear una fila nueva cada vez.
/// </summary>
public static class SqliteEffectDefinitionWriter
{
    public static void WriteEffectDefinition(
        SqliteConnection connection,
        string id,
        string name,
        string trigger,
        bool requiresTarget,
        string targetKind,
        FilterDto? targetFilter,
        IReadOnlyList<EffectActionStepDto> actionSteps,
        string visualProfileKey = "")
    {
        using (var delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM EffectDefinitions WHERE Id = $Id";
            delete.Parameters.AddWithValue("$Id", id);
            delete.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO EffectDefinitions (Id, Name, Trigger, VisualProfileKey, IsActive) VALUES ($Id, $Name, $Trigger, $VisualProfileKey, 1)";
            insert.Parameters.AddWithValue("$Id", id);
            insert.Parameters.AddWithValue("$Name", name);
            insert.Parameters.AddWithValue("$Trigger", trigger);
            insert.Parameters.AddWithValue("$VisualProfileKey", visualProfileKey ?? "");
            insert.ExecuteNonQuery();
        }

        if (requiresTarget)
        {
            int? filterId = SqliteRequirementWriter.WriteFilter(connection, targetFilter);
            using var insertTargetSpec = connection.CreateCommand();
            insertTargetSpec.CommandText = "INSERT INTO EffectTargetSpecs (EffectDefinitionId, TargetKind, FilterId, Required) VALUES ($Id, $TargetKind, $FilterId, 1)";
            insertTargetSpec.Parameters.AddWithValue("$Id", id);
            insertTargetSpec.Parameters.AddWithValue("$TargetKind", targetKind);
            insertTargetSpec.Parameters.AddWithValue("$FilterId", (object?)filterId ?? DBNull.Value);
            insertTargetSpec.ExecuteNonQuery();
        }

        for (int stepOrder = 0; stepOrder < actionSteps.Count; stepOrder++)
        {
            var step = actionSteps[stepOrder];
            using var insertStep = connection.CreateCommand();
            insertStep.CommandText = "INSERT INTO EffectActionSteps (EffectDefinitionId, StepOrder, ActionKind, ParamsJson) VALUES ($Id, $StepOrder, $ActionKind, $ParamsJson)";
            insertStep.Parameters.AddWithValue("$Id", id);
            insertStep.Parameters.AddWithValue("$StepOrder", stepOrder);
            insertStep.Parameters.AddWithValue("$ActionKind", step.ActionKind);
            insertStep.Parameters.AddWithValue("$ParamsJson", ParamsTextToJson(step.ParamsText));
            insertStep.ExecuteNonQuery();
        }
    }

    /// <summary>Convierte el formato simple del editor (<c>Clave=Valor;Clave2=Valor2</c>) al JSON que espera el loader.</summary>
    public static string ParamsTextToJson(string paramsText)
    {
        var values = new Dictionary<string, string>();
        foreach (var pair in paramsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            values[pair[..eq].Trim()] = pair[(eq + 1)..].Trim();
        }
        return values.Count == 0 ? "{}" : JsonSerializer.Serialize(values);
    }

    /// <summary>Convierte de vuelta el JSON guardado al formato simple del editor, para poder reeditarlo.</summary>
    public static string ParamsJsonToText(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson) || paramsJson == "{}") return "";
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(paramsJson);
        return values == null ? "" : string.Join(";", values.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
