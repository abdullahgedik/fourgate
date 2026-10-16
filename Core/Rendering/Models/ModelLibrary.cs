using System.Collections.Generic;
using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering.Models;

/// <summary>Every model from <c>models.json</c>, looked up by id.</summary>
public sealed class ModelLibrary
{
    private readonly Dictionary<string, ModelDefinition> _models = new();

    public ModelLibrary(ModelsFile file)
    {
        for (var i = 0; i < file.Models.Count; i++)
        {
            var model = Build(file.Models[i]);
            _models[model.Id] = model;
        }
    }

    public int Count => _models.Count;

    public bool TryGet(string? id, out ModelDefinition model)
    {
        if (id is not null && _models.TryGetValue(id, out var found))
        {
            model = found;
            return true;
        }

        model = null!;
        return false;
    }

    public bool Contains(string id)
    {
        return _models.ContainsKey(id);
    }

    /// <summary>Fails with a clear message when the game data names a model that does not exist.</summary>
    public void CheckReferences(GameData data)
    {
        Require(data.Game.Player.Model, DataLoader.GameFileName, "player.model");
        Require(data.Game.Player.DefaultGunModel, DataLoader.GameFileName, "player.defaultGunModel");
        Require(data.Game.Debug.PracticeTargetModel, DataLoader.GameFileName, "debug.practiceTargetModel");
        for (var i = 0; i < data.Bosses.Count; i++)
        {
            Require(data.Bosses[i].Model, DataLoader.BossesFileName, $"bosses[{i}].model");
        }

        for (var i = 0; i < data.Weapons.Count; i++)
        {
            Require(data.Weapons[i].Model, DataLoader.WeaponsFileName, $"weapons[{i}].model");
            Require(data.Weapons[i].Projectile?.Model, DataLoader.WeaponsFileName, $"weapons[{i}].projectile.model");
        }

        for (var i = 0; i < data.Enemies.Count; i++)
        {
            Require(data.Enemies[i].Model, DataLoader.EnemiesFileName, $"enemies[{i}].model");
        }
    }

    private void Require(string? id, string file, string field)
    {
        DataLoadException.Require(
            id is null || _models.ContainsKey(id), file, field, $"'{id}' is not a model in {DataLoader.ModelsFileName}");
    }

    private static ModelDefinition Build(ModelConfig config)
    {
        var parts = new List<ModelPart>(config.Parts.Count);
        var indices = new Dictionary<string, int>();
        for (var i = 0; i < config.Parts.Count; i++)
        {
            var part = config.Parts[i];
            indices[part.Id] = i;
            var rest = part.Rest is null ? Vector3.Zero : Radians(Vector(part.Rest));
            var swing = MathHelper.ToRadians(part.Swing);
            var phase = MathHelper.ToRadians(part.SwingPhase);
            var holds = new[] { part.Holds?.OneHand, part.Holds?.TwoHands, part.Holds?.Dual, part.Holds?.Shoulder };
            var restByHold = new Vector3[holds.Length];
            var swingByHold = new float[holds.Length];
            var phaseByHold = new float[holds.Length];
            for (var h = 0; h < holds.Length; h++)
            {
                restByHold[h] = holds[h]?.Rest is { } holdRest ? Radians(Vector(holdRest)) : rest;
                swingByHold[h] = holds[h]?.Swing is { } holdSwing ? MathHelper.ToRadians(holdSwing) : swing;
                phaseByHold[h] = holds[h]?.SwingPhase is { } holdPhase ? MathHelper.ToRadians(holdPhase) : phase;
            }

            parts.Add(new ModelPart
            {
                Id = part.Id,
                Parent = part.Parent is null ? -1 : indices[part.Parent],
                Pivot = Vector(part.Pivot),
                HalfSize = part.Size is null ? Vector3.Zero : Vector(part.Size) / 2f,
                Center = part.Center is null ? Vector3.Zero : Vector(part.Center),
                Color = part.Color is null ? Color.White : new Color(part.Color[0], part.Color[1], part.Color[2]),
                Tint = part.Tint,
                Shade = part.Shade,
                Rest = rest,
                Swing = swing,
                SwingPhase = phase,
                Step = part.Step,
                RestByHold = restByHold,
                SwingByHold = swingByHold,
                SwingPhaseByHold = phaseByHold,
                Aim = part.Aim,
                Recoil = MathHelper.ToRadians(part.Recoil),
                Lunge = MathHelper.ToRadians(part.Lunge),
                Flinch = MathHelper.ToRadians(part.Flinch),
                Windup = MathHelper.ToRadians(part.Windup),
                Mount = part.Mount switch
                {
                    "gun" => ModelMount.Gun,
                    "gunLeft" => ModelMount.GunLeft,
                    "gunTwoHands" => ModelMount.GunTwoHands,
                    "gunShoulder" => ModelMount.GunShoulder,
                    _ => ModelMount.None,
                },
            });
        }

        return new ModelDefinition
        {
            Id = config.Id,
            Height = config.Height,
            Stride = config.Stride,
            FullStrideSpeed = config.FullStrideSpeed,
            Bob = config.Bob,
            Scale = config.Scale is null ? Vector3.One : Vector(config.Scale),
            Muzzle = config.Muzzle is null ? null : Vector(config.Muzzle),
            Exhaust = config.Exhaust is null ? null : Vector(config.Exhaust),
            Parts = parts,
        };
    }

    private static Vector3 Vector(IReadOnlyList<float> values)
    {
        return new Vector3(values[0], values[1], values[2]);
    }

    private static Vector3 Radians(Vector3 degrees)
    {
        return new Vector3(
            MathHelper.ToRadians(degrees.X), MathHelper.ToRadians(degrees.Y), MathHelper.ToRadians(degrees.Z));
    }
}
