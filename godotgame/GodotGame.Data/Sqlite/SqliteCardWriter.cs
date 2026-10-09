using Microsoft.Data.Sqlite;
using GodotGame.Core.Requirements;
using GodotGame.Data.Loaders;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Lee y escribe el catalogo de cartas en la tabla <c>Cards</c> de una base
/// SQLite -- y, a partir de la Fase 5, tambien orquesta el guardado de los
/// filtros/requisitos/efecto compuesto asociados (Fusion, Ritual, Equip,
/// Field, efecto data-driven), delegando en <see cref="SqliteRequirementWriter"/>
/// y <see cref="SqliteEffectDefinitionWriter"/>. Mismo contrato publico que
/// <see cref="Loaders.JsonCardWriter"/> para que <c>GodotGame.CardEditor</c>
/// pueda usar cualquiera de los dos sin cambiar su propio codigo (aunque el
/// backend JSON no soporta estas estructuras nuevas: solo el catalogo base).
/// </summary>
public sealed class SqliteCardWriter
{
    private readonly string _dbPath;

    public SqliteCardWriter(string dbPath) => _dbPath = dbPath;

    public List<CardDto> LoadAllDtos()
    {
        var dtos = new List<CardDto>();
        if (!File.Exists(_dbPath)) return dtos;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        var requirementLoader = new SqliteRequirementLoader(connection);
        var monsterEffects = SqliteMonsterEffectStore.LoadAll(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category,
                   RitualMonsterId, RequiredRitualLevel, RequirementSetId,
                   EquipTargetFilterId, EquipAttackModifier, EquipDefenseModifier, EquipDuration, EquipDurationTurns,
                   FieldTypeId, GuardianStar1, GuardianStar2
            FROM Cards ORDER BY Id
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var dto = SqliteCardMapping.ReadDto(reader);
            dto.GuardianStar1 = reader.GetString(22);
            dto.GuardianStar2 = reader.GetString(23);
            dto.MonsterEffects = monsterEffects.GetValueOrDefault(dto.Id) ?? new List<MonsterEffectDto>();

            if (!reader.IsDBNull(15))
            {
                var set = requirementLoader.LoadRequirementSet(reader.GetInt32(15));
                if (set.LevelSum != null)
                    dto.RitualFilter = RequirementDtoConversion.ToFilterDto(set.LevelSum.Filter);
            }
            if (!reader.IsDBNull(16))
                dto.EquipTargetFilter = RequirementDtoConversion.ToFilterDto(requirementLoader.LoadFilter(reader.GetInt32(16)));
            dto.EquipAttackModifier = reader.GetInt32(17);
            dto.EquipDefenseModifier = reader.GetInt32(18);
            dto.EquipDuration = reader.GetString(19);
            dto.EquipDurationTurns = reader.GetInt32(20);
            dto.FieldTypeId = reader.IsDBNull(21) ? null : reader.GetString(21);

            LoadFusionMaterialsIfPresent(connection, requirementLoader, dto);
            LoadComposedEffectIfPresent(connection, requirementLoader, dto);

            dtos.Add(dto);
        }

        return dtos;
    }

    /// <summary>
    /// Inserta o reemplaza (por Id) la carta en la base. Antes de escribir la
    /// fila de <c>Cards</c>, escribe (siempre como filas nuevas, ver
    /// <see cref="SqliteRequirementWriter"/>) el filtro/requisito que
    /// corresponda segun Kind/Category/SubType, y si <see cref="CardDto.ComposeCustomEffect"/>
    /// esta activo, el <c>EffectDefinition</c> compuesto.
    /// </summary>
    public void SaveCard(CardDto dto)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);

        bool isSpell = dto.Kind.Equals("Spell", StringComparison.OrdinalIgnoreCase);
        bool isMonster = dto.Kind.Equals("Monster", StringComparison.OrdinalIgnoreCase);

        int? requirementSetId = null;
        if (isSpell && dto.SubType.Equals("Ritual", StringComparison.OrdinalIgnoreCase) && dto.RequiredRitualLevel > 0)
            requirementSetId = SqliteRequirementWriter.WriteLevelSumRequirementSet(connection, dto.RitualFilter, dto.RequiredRitualLevel);

        int? equipTargetFilterId = null;
        if (isSpell && dto.SubType.Equals("Equip", StringComparison.OrdinalIgnoreCase))
            equipTargetFilterId = SqliteRequirementWriter.WriteFilter(connection, dto.EquipTargetFilter);

        string? fieldTypeId = isSpell && dto.SubType.Equals("Field", StringComparison.OrdinalIgnoreCase) ? dto.FieldTypeId : null;

        if (dto.ComposeCustomEffect)
        {
            dto.EffectId = ComposedEffectId(dto.Id);
            SqliteEffectDefinitionWriter.WriteEffectDefinition(
                connection, dto.EffectId, dto.Name, dto.EffectTrigger,
                dto.EffectRequiresTarget, dto.EffectTargetKind, dto.EffectTargetFilter, dto.EffectActionSteps,
                dto.EffectVisualProfileKey);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel,
                                    RequirementSetId, EquipTargetFilterId, EquipAttackModifier, EquipDefenseModifier, EquipDuration, EquipDurationTurns, FieldTypeId,
                                    GuardianStar1, GuardianStar2)
                VALUES ($Id, $Kind, $Name, $Attack, $Defense, $Level, $Type, $Attribute, $Image, $Description, $EffectId, $SubType, $Category, $RitualMonsterId, $RequiredRitualLevel,
                        $RequirementSetId, $EquipTargetFilterId, $EquipAttackModifier, $EquipDefenseModifier, $EquipDuration, $EquipDurationTurns, $FieldTypeId,
                        $GuardianStar1, $GuardianStar2)
                ON CONFLICT(Id) DO UPDATE SET
                    Kind = excluded.Kind, Name = excluded.Name, Attack = excluded.Attack, Defense = excluded.Defense,
                    Level = excluded.Level, Type = excluded.Type, Attribute = excluded.Attribute, Image = excluded.Image,
                    Description = excluded.Description, EffectId = excluded.EffectId, SubType = excluded.SubType,
                    Category = excluded.Category, RitualMonsterId = excluded.RitualMonsterId, RequiredRitualLevel = excluded.RequiredRitualLevel,
                    RequirementSetId = excluded.RequirementSetId, EquipTargetFilterId = excluded.EquipTargetFilterId,
                    EquipAttackModifier = excluded.EquipAttackModifier, EquipDefenseModifier = excluded.EquipDefenseModifier,
                    EquipDuration = excluded.EquipDuration, EquipDurationTurns = excluded.EquipDurationTurns,
                    FieldTypeId = excluded.FieldTypeId,
                    GuardianStar1 = excluded.GuardianStar1, GuardianStar2 = excluded.GuardianStar2
                """;
            SqliteCardMapping.BindParameters(command, dto);
            command.Parameters.AddWithValue("$RequirementSetId", (object?)requirementSetId ?? DBNull.Value);
            command.Parameters.AddWithValue("$EquipTargetFilterId", (object?)equipTargetFilterId ?? DBNull.Value);
            command.Parameters.AddWithValue("$EquipAttackModifier", dto.EquipAttackModifier);
            command.Parameters.AddWithValue("$EquipDefenseModifier", dto.EquipDefenseModifier);
            command.Parameters.AddWithValue("$EquipDuration", dto.EquipDuration);
            command.Parameters.AddWithValue("$EquipDurationTurns", dto.EquipDurationTurns);
            command.Parameters.AddWithValue("$FieldTypeId", (object?)fieldTypeId ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        // La receta de Fusion vive en la tabla Fusions (Result = Id de esta
        // carta), no en Cards: se reemplaza aparte, siempre despues de que la
        // carta ya tiene fila (por si Result referenciara su propio Id via FK
        // en el futuro).
        if (isMonster && dto.Category.Equals("Fusion", StringComparison.OrdinalIgnoreCase))
            SaveFusionRecipe(connection, dto);

        SqliteMonsterEffectStore.Save(connection, dto.Id, dto.MonsterEffects);
    }

    private static void SaveFusionRecipe(SqliteConnection connection, CardDto dto)
    {
        using (var deleteOld = connection.CreateCommand())
        {
            deleteOld.CommandText = "DELETE FROM Fusions WHERE Result = $ResultId AND RequirementSetId IS NOT NULL";
            deleteOld.Parameters.AddWithValue("$ResultId", dto.Id);
            deleteOld.ExecuteNonQuery();
        }

        if (dto.FusionMaterials.Count == 0) return;

        int materialSetId = SqliteRequirementWriter.WriteMaterialSlotsRequirementSet(connection, dto.FusionMaterials)!.Value;
        using var insertFusion = connection.CreateCommand();
        insertFusion.CommandText = "INSERT INTO Fusions (MaterialA, MaterialB, Result, RequirementSetId) VALUES (NULL, NULL, $ResultId, $SetId)";
        insertFusion.Parameters.AddWithValue("$ResultId", dto.Id);
        insertFusion.Parameters.AddWithValue("$SetId", materialSetId);
        insertFusion.ExecuteNonQuery();
    }

    private static void LoadFusionMaterialsIfPresent(SqliteConnection connection, SqliteRequirementLoader requirementLoader, CardDto dto)
    {
        if (!dto.Kind.Equals("Monster", StringComparison.OrdinalIgnoreCase) || !dto.Category.Equals("Fusion", StringComparison.OrdinalIgnoreCase))
            return;

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RequirementSetId FROM Fusions WHERE Result = $ResultId AND RequirementSetId IS NOT NULL";
        command.Parameters.AddWithValue("$ResultId", dto.Id);
        var result = command.ExecuteScalar();
        if (result == null) return;

        var set = requirementLoader.LoadRequirementSet(Convert.ToInt32((long)result));
        dto.FusionMaterials = set.Slots.Select(RequirementDtoConversion.ToFusionSlotDto).ToList();
    }

    private static void LoadComposedEffectIfPresent(SqliteConnection connection, SqliteRequirementLoader requirementLoader, CardDto dto)
    {
        if (string.IsNullOrEmpty(dto.EffectId) || dto.EffectId != ComposedEffectId(dto.Id)) return;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Trigger, VisualProfileKey FROM EffectDefinitions WHERE Id = $Id";
            command.Parameters.AddWithValue("$Id", dto.EffectId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return;
            dto.ComposeCustomEffect = true;
            dto.EffectTrigger = reader.GetString(0);
            dto.EffectVisualProfileKey = reader.IsDBNull(1) ? "" : reader.GetString(1);
        }

        using (var targetCommand = connection.CreateCommand())
        {
            targetCommand.CommandText = "SELECT TargetKind, FilterId FROM EffectTargetSpecs WHERE EffectDefinitionId = $Id";
            targetCommand.Parameters.AddWithValue("$Id", dto.EffectId);
            using var targetReader = targetCommand.ExecuteReader();
            if (targetReader.Read())
            {
                dto.EffectRequiresTarget = true;
                dto.EffectTargetKind = targetReader.GetString(0);
                if (!targetReader.IsDBNull(1))
                    dto.EffectTargetFilter = RequirementDtoConversion.ToFilterDto(requirementLoader.LoadFilter(targetReader.GetInt32(1)));
            }
        }

        using (var stepsCommand = connection.CreateCommand())
        {
            stepsCommand.CommandText = "SELECT ActionKind, ParamsJson FROM EffectActionSteps WHERE EffectDefinitionId = $Id ORDER BY StepOrder";
            stepsCommand.Parameters.AddWithValue("$Id", dto.EffectId);
            using var stepsReader = stepsCommand.ExecuteReader();
            while (stepsReader.Read())
                dto.EffectActionSteps.Add(new EffectActionStepDto
                {
                    ActionKind = stepsReader.GetString(0),
                    ParamsText = SqliteEffectDefinitionWriter.ParamsJsonToText(stepsReader.GetString(1))
                });
        }
    }

    /// <summary>Id estable del efecto compuesto de una carta: reeditar y volver a guardar reemplaza la misma definicion, no crea una nueva.</summary>
    private static string ComposedEffectId(int cardId) => $"card_{cardId}_effect";

    public void DeleteCard(int id)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cards WHERE Id = $Id";
        command.Parameters.AddWithValue("$Id", id);
        command.ExecuteNonQuery();
    }
}
