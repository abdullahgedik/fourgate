using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering.Models;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ModelTests
{
    private const float Step = 1f / 60f;

    private static readonly ModelLibrary Shipped = new(DataLoader.LoadModels(DataLoader.DefaultDirectory));

    [Fact]
    public void ShippedModels_Load_AndEveryModelTheGameNamesExists()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Shipped.CheckReferences(data);

        Assert.True(Shipped.Contains("survivor"));
        Assert.True(Shipped.Contains("zombie"));
        Assert.Equal("survivor", data.Game.Player.Model);
        Assert.Equal("zombie", data.GetEnemy("normal").Model);
        Assert.Equal("shotgun", data.GetWeapon("shotgun").Model);
    }

    [Fact]
    public void EveryZombieBossAndGun_HasItsOwnModel_AndTheLauncherSitsOnTheShoulder()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Assert.All(data.Enemies, enemy => Assert.True(Shipped.Contains(enemy.Model!)));
        Assert.All(data.Bosses, boss => Assert.True(Shipped.Contains(boss.Model!)));
        foreach (var gun in new[] { "pistol", "smg", "shotgun", "assault", "marksman", "sniper", "rocket", "flamethrower", "railgun" })
        {
            Assert.True(Shipped.Contains(data.GetWeapon(gun).Model!), gun);
        }

        Assert.Equal(WeaponHold.Shoulder, data.GetWeapon("rocket").Hold);
        Assert.Equal(WeaponHold.OneHand, data.GetWeapon("pistol").Hold);
        Assert.Equal(WeaponHold.TwoHands, data.GetWeapon("flamethrower").Hold);
        Assert.Equal("dummy", data.Game.Debug.PracticeTargetModel);
    }

    [Fact]
    public void Spitter_WindingUp_DrawsItsThrowingArmUpOverItsShoulder()
    {
        Shipped.TryGet("spitter", out var model);
        var hand = IndexOf(model, "handRight");

        var resting = Place(model, new ModelPose("spitter", Vector2.Zero, 0f, 0f, 0f, 0f, Color.White), hand);
        var drawnBack = Place(model, new ModelPose("spitter", Vector2.Zero, 0f, 0f, 0f, 0f, Color.White, Windup: 1f), hand);

        Assert.True(drawnBack.Y > resting.Y + 15f);
        Assert.True(drawnBack.X < resting.X);
    }

    [Fact]
    public void Part_HangingFromAPartListedLater_IsRejected()
    {
        const string json = """
            { "models": [ { "id": "m", "parts": [
              { "id": "arm", "parent": "body", "pivot": [ 0, 0, 0 ], "size": [ 1, 1, 1 ], "color": [ 1, 2, 3 ] },
              { "id": "body", "pivot": [ 0, 0, 0 ], "size": [ 1, 1, 1 ], "color": [ 1, 2, 3 ] }
            ] } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseModels(json, "models.json"));

        Assert.Equal("models[0].parts[0].parent", error.Field);
    }

    [Fact]
    public void Walking_SwingsTheLegsOppositeWays_AndStandingStillDoesNot()
    {
        Shipped.TryGet("survivor", out var model);

        // A quarter of a step cycle in, at full speed: one leg forward, the other back.
        var walking = Feet(model, speed: 400f, walked: model.Stride / 4f);
        Assert.True(MathF.Abs(walking.Left.X - walking.Right.X) > 4f);
        Assert.True(walking.Left.X * walking.Right.X < 0f);

        var standing = Feet(model, speed: 0f, walked: model.Stride / 4f);
        Assert.Equal(standing.Left.X, standing.Right.X, 2);
    }

    [Fact]
    public void Strafing_SwingsTheLegsSideways_AndBackpedallingReversesTheStride()
    {
        Shipped.TryGet("survivor", out var model);

        // Facing +X; moving toward the body's right (+Y on the floor, world z).
        var strafe = Feet(model, speed: 400f, walked: model.Stride / 4f, heading: MathHelper.PiOver2);
        Assert.True(MathF.Abs(strafe.Left.Z - strafe.Right.Z) > 4f);
        Assert.Equal(strafe.Left.X, strafe.Right.X, 1);

        var forward = Feet(model, speed: 400f, walked: model.Stride / 4f, heading: 0f);
        var backward = Feet(model, speed: 400f, walked: model.Stride / 4f, heading: MathHelper.Pi);
        Assert.Equal(forward.Left.X, -backward.Left.X, 1);
    }

    [Fact]
    public void OneHandedGuns_LeaveTheLeftArmFree_TwoHandedOnesBringItForward()
    {
        Shipped.TryGet("survivor", out var model);

        var free = Fist(model, ModelHold.OneHand);
        var supporting = Fist(model, ModelHold.TwoHands);

        // Hanging at the side, low; or reaching forward under the gun.
        Assert.True(free.Y < supporting.Y - 5f);
        Assert.True(supporting.X > free.X + 5f);
    }

    [Fact]
    public void Aiming_TurnsTheUpperBody_TowardTheAim()
    {
        Shipped.TryGet("survivor", out var model);

        // Facing +X; aiming a quarter turn round, toward +Y on the floor (world z).
        var ahead = Eye(model, aim: 0f);
        var turned = Eye(model, aim: MathHelper.PiOver2);

        Assert.True(ahead.X > 3f);
        Assert.True(turned.Z > 3f);
        Assert.True(turned.X < ahead.X);
    }

    [Fact]
    public void Falling_LaysTheBodyDownOnTheFloor()
    {
        Shipped.TryGet("zombie", out var model);
        var standing = Head(model, fall: 0f);
        var fallen = Head(model, fall: 1f);

        Assert.True(standing.Y > 30f);
        Assert.True(fallen.Y < 15f);
    }

    [Fact]
    public void Corpses_FallLieAndShrinkAway_ThenAreGone()
    {
        var corpses = new CorpseSystem();
        corpses.Add("zombie", new Vector2(100, 100), 0f, Color.Green, 20f, lieSeconds: 1f);
        Assert.Equal(1, corpses.Count);

        for (var i = 0; i < 60; i++)
        {
            corpses.Update(Step);
        }

        Assert.Equal(1, corpses.Count);
        for (var i = 0; i < 60; i++)
        {
            corpses.Update(Step);
        }

        Assert.Equal(0, corpses.Count);
    }

    [Fact]
    public void ZombieWithAModel_IsDrawnAsItsModel_AndLungesWhenItAttacks()
    {
        var zombie = TestData.Zombie();
        var config = new EnemyConfig
        {
            Id = zombie.Id, DisplayName = zombie.DisplayName, Hp = zombie.Hp, Speed = zombie.Speed, Radius = zombie.Radius,
            ContactDamage = 10, ContactCooldown = 1f, Xp = zombie.Xp, ScoreValue = zombie.ScoreValue, Cost = zombie.Cost,
            UnlockWave = zombie.UnlockWave, DropChance = zombie.DropChance, Model = "zombie",
        };
        var enemies = new EnemySystem(new NullAudioManager());
        var arena = new Arena(TestData.Arena());
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1500, 1500));
        var players = new List<Player> { player };
        enemies.Spawn(config, new Vector2(1538, 1500));

        enemies.Update(Step, players, arena);
        for (var i = 0; i < 6; i++)
        {
            enemies.Update(Step, players, arena);
        }

        var drawn = new RecordingRenderer();
        enemies.Draw(drawn);
        var pose = Assert.Single(drawn.Models);
        Assert.Equal("zombie", pose.ModelId);
        Assert.True(player.Hp < 100f);
        Assert.True(pose.Lunge > 0f);
    }

    [Fact]
    public void HitEnemy_WashesItsWholeModel_NotJustItsSkin()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var zombie = enemies.Spawn(TestData.Zombie(), new Vector2(1500, 1500));
        Assert.Equal(0f, zombie.ModelGlow(out _));

        zombie.Hit(5f, 0);
        var glow = zombie.ModelGlow(out var color);

        Assert.True(glow > 0.5f);
        Assert.True(color.R > 200 && color.G < 100);
        Assert.Equal(zombie.Config.Color[1], zombie.BodyColor.G);
    }

    [Fact]
    public void OneBigHit_TearsAZombieApart_ManySmallOnesDoNot()
    {
        var arena = new Arena(TestData.Arena());
        var nobody = new List<Player>();
        var enemies = new EnemySystem(new NullAudioManager());
        var blasted = enemies.Spawn(TestData.Zombie(), new Vector2(1000, 1000));
        var whittled = enemies.Spawn(TestData.Zombie(), new Vector2(1500, 1500));

        blasted.Hit(blasted.MaxHp, 0);
        for (var i = 0; i < 8; i++)
        {
            whittled.Hit(whittled.MaxHp / 8f, 0);
        }

        enemies.Update(Step, nobody, arena);

        Assert.Equal(2, enemies.Deaths.Count);
        Assert.Contains(enemies.Deaths, d => d.Gibbed && d.Position == new Vector2(1000, 1000));
        Assert.Contains(enemies.Deaths, d => !d.Gibbed && d.Position == new Vector2(1500, 1500));
    }

    [Fact]
    public void Shells_FlyOut_LieOnTheFloorAMoment_ThenGo()
    {
        var particles = new ParticleSystem(new Fourgate.Core.Rng.GameRandom(2));
        particles.Shell(new Vector2(500, 500), 30f, Vector2.UnitY, Color.Gold);

        for (var i = 0; i < 60; i++)
        {
            particles.Update(Step);
        }

        var drawn = new RecordingRenderer();
        particles.Draw(drawn);
        var shell = Assert.Single(drawn.Particles);
        Assert.Equal(0f, shell.BaseHeight + shell.Lift, 2);
        Assert.True(shell.Ground.Y > 500f);

        for (var i = 0; i < 120; i++)
        {
            particles.Update(Step);
        }

        Assert.Equal(0, particles.Count);
    }

    [Fact]
    public void EveryCharacterModel_FillsAFairShareOfItsHitbox()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        // Shots hitting air next to a thin body (or fireballs hitting a player beside them) feel
        // wrong, so each body must be at least this wide for its hitbox.
        void Check(string? id, float radius)
        {
            Assert.True(Shipped.TryGet(id, out var model), id);
            Assert.True(BodyHalfWidth(model) >= radius * 0.45f, $"{id}: body {BodyHalfWidth(model):0.0} for radius {radius}");
        }

        Check(data.Game.Player.Model, data.Game.Player.Radius);
        foreach (var enemy in data.Enemies)
        {
            Check(enemy.Model, enemy.Radius);
        }

        foreach (var boss in data.Bosses)
        {
            Check(boss.Model, boss.Radius);
        }
    }

    [Fact]
    public void YardDressing_PaintsOnlyTheSoftMarks_AndDrawsTheSharpDetailAsGeometry()
    {
        var arena = new Arena(TestData.Arena());
        var floor = new Fourgate.Core.Rendering.FloorStains(new Vector2(arena.Left, arena.Top), arena.Size);
        var decor = new Fourgate.UI.ArenaDecor(arena, 90f, 120f, 60f);

        decor.Paint(floor, new Fourgate.Core.Rng.GameRandom(3));

        // Old blood and scorched thresholds go into the floor image; flagstones, cracks, runes
        // and bones are drawn crisp each frame instead.
        Assert.InRange(floor.TotalAdded, 1, 60);
        Assert.Equal(floor.TotalAdded, floor.PendingCount);
    }

    [Fact]
    public void EveryGunsMuzzle_SitsAtTheFrontEndOfItsBarrel()
    {
        foreach (var gun in new[] { "pistol", "smg", "shotgun", "rifle", "assault", "marksman", "sniper", "flamer", "railgun" })
        {
            Assert.True(Shipped.TryGet(gun, out var model), gun);
            Assert.True(model.Muzzle.HasValue, gun);
            var front = FrontOf(model, out var frontHeight);
            Assert.True(MathF.Abs(model.Muzzle!.Value.Z - front) <= 1.5f, $"{gun}: muzzle {model.Muzzle.Value.Z} vs barrel end {front}");
            Assert.True(MathF.Abs(model.Muzzle.Value.Y - frontHeight) <= 2f, $"{gun}: muzzle height {model.Muzzle.Value.Y} vs {frontHeight}");
        }

        // The launcher flares at both ends of its tube.
        Assert.True(Shipped.TryGet("launcher", out var launcher));
        Assert.True(launcher.Muzzle!.Value.Z > 15f);
        Assert.True(launcher.Exhaust!.Value.Z < -10f);
    }

    // The furthest forward point of a gun's parts, and the height of the part that reaches it.
    private static float FrontOf(ModelDefinition model, out float height)
    {
        var pose = new ModelPose(model.Id, Vector2.Zero, 0f, 0f, 0f, 0f, Color.White);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, Matrix.Identity, worlds);
        var front = float.MinValue;
        height = 0f;
        for (var i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (!part.IsVisible)
            {
                continue;
            }

            var tip = Vector3.Transform(part.Center + new Vector3(0f, 0f, part.HalfSize.Z), worlds[i]);
            if (tip.Z > front)
            {
                front = tip.Z;
                height = tip.Y;
            }
        }

        return front;
    }

    [Fact]
    public void ShotsLeaveFromTheBarrel_ThePistolsOffToTheRight_TheLaunchersOnTheShoulder()
    {
        Assert.True(MuzzleFinder.TryFind(Shipped, "survivor", "pistol", ModelHold.OneHand, out var forward, out var right, out _));
        Assert.True(forward > 20f);
        Assert.True(right > 5f);

        Assert.True(MuzzleFinder.TryFind(Shipped, "survivor", "launcher", ModelHold.Shoulder, out _, out var shoulder, out _));
        Assert.True(shoulder > 4f);

        Assert.True(MuzzleFinder.TryFind(Shipped, "survivor", "pistol", ModelHold.Dual, out _, out _, out var spread));
        Assert.True(spread > 8f);
    }

    [Fact]
    public void Muzzle_IsPulledBack_BeforeAWallOrAZombieInTheWay()
    {
        var arena = new Arena(TestData.Arena());
        var shots = new ShotSystem(arena);
        var enemies = new EnemySystem(new NullAudioManager());
        shots.AddSource(enemies);
        enemies.Spawn(TestData.Zombie(), new Vector2(1550, 1500));
        enemies.Update(Step, new List<Player>(), arena);

        // Clear way: the muzzle stays where it is.
        Assert.Equal(new Vector2(1500, 1400), shots.ClearMuzzle(new Vector2(1500, 1500), new Vector2(1500, 1400)));

        // A zombie hugging the player: the shot starts before its body.
        var blocked = shots.ClearMuzzle(new Vector2(1500, 1500), new Vector2(1600, 1500));
        Assert.True(blocked.X < 1550f - TestData.Zombie().Radius);

        // Against the wall: the shot starts inside it.
        var walled = shots.ClearMuzzle(new Vector2(arena.Left + 25f, 1500), new Vector2(arena.Left - 20f, 1500));
        Assert.True(walled.X > arena.Left);
    }

    private static float BodyHalfWidth(ModelDefinition model)
    {
        var pose = new ModelPose(model.Id, Vector2.Zero, 0f, 0f, 0f, 0f, Color.White);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        var half = 0f;
        for (var i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (part.Id is not ("torso" or "hips" or "body" or "robe"))
            {
                continue;
            }

            for (var c = 0; c < 8; c++)
            {
                var corner = part.Center + new Vector3(
                    (c & 1) != 0 ? part.HalfSize.X : -part.HalfSize.X,
                    (c & 2) != 0 ? part.HalfSize.Y : -part.HalfSize.Y,
                    (c & 4) != 0 ? part.HalfSize.Z : -part.HalfSize.Z);
                var world = Vector3.Transform(corner, worlds[i]);
                half = MathF.Max(half, MathF.Max(MathF.Abs(world.X), MathF.Abs(world.Z)));
            }
        }

        return half;
    }

    private static (Vector3 Left, Vector3 Right) Feet(ModelDefinition model, float speed, float walked, float? heading = null)
    {
        var pose = new ModelPose("survivor", Vector2.Zero, 0f, 0f, walked, speed, Color.White, MoveHeading: heading);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        var left = IndexOf(model, "legLeft");
        var right = IndexOf(model, "legRight");
        var foot = new Vector3(0f, -15f, 0f);
        return (Vector3.Transform(foot, worlds[left]), Vector3.Transform(foot, worlds[right]));
    }

    private static Vector3 Place(ModelDefinition model, ModelPose pose, int part)
    {
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        return Vector3.Transform(Vector3.Zero, worlds[part]);
    }

    private static Vector3 Fist(ModelDefinition model, ModelHold hold)
    {
        var pose = new ModelPose("survivor", Vector2.Zero, 0f, 0f, 0f, 0f, Color.White, Hold: hold);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        return Vector3.Transform(Vector3.Zero, worlds[IndexOf(model, "fistLeft")]);
    }

    private static Vector3 Eye(ModelDefinition model, float aim)
    {
        var pose = new ModelPose("survivor", Vector2.Zero, 0f, aim, 0f, 0f, Color.White);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        return Vector3.Transform(Vector3.Zero, worlds[IndexOf(model, "eyeRight")]);
    }

    private static Vector3 Head(ModelDefinition model, float fall)
    {
        var pose = new ModelPose("zombie", Vector2.Zero, 0f, 0f, 0f, 0f, Color.White, Fall: fall);
        var worlds = new Matrix[model.Parts.Count];
        ModelAnimator.Pose(model, pose, ModelAnimator.Root(pose), worlds);
        return Vector3.Transform(new Vector3(0f, 5f, 0f), worlds[IndexOf(model, "head")]);
    }

    private static int IndexOf(ModelDefinition model, string id)
    {
        for (var i = 0; i < model.Parts.Count; i++)
        {
            if (model.Parts[i].Id == id)
            {
                return i;
            }
        }

        throw new InvalidOperationException(id);
    }
}
