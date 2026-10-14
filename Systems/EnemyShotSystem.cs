using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Projectiles;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns enemy fireballs: moves them and resolves what they hit.</summary>
public sealed class EnemyShotSystem
{
    private const int InitialFireballs = 64;
    private const float GlowScale = 1.5f;

    private static readonly Color CoreColor = new(255, 210, 90);
    private static readonly Color GlowColor = new(255, 90, 30);

    private readonly Pool<Fireball> _fireballs = new(InitialFireballs);

    public int FireballCount => _fireballs.Count;

    /// <summary>Throws one fireball along a unit direction.</summary>
    public void Fire(Vector2 origin, Vector2 direction, EnemyAttackConfig attack)
    {
        Fire(origin, direction, attack.ProjectileSpeed, attack.ProjectileRadius, attack.Damage, attack.ProjectileRange);
    }

    /// <summary>Throws one fireball along a unit direction with the given flight values.</summary>
    public void Fire(Vector2 origin, Vector2 direction, float speed, float radius, float damage, float range)
    {
        _fireballs.Rent().Launch(origin, direction * speed, radius, damage, range);
    }

    /// <summary>
    /// Moves every fireball. One goes out on a wall, a pillar, a barrel (which counts as an
    /// enemy hit on it) or a player; a dashing player is not hit and it flies on.
    /// </summary>
    public void Update(float deltaSeconds, IReadOnlyList<Player> players, Arena arena)
    {
        for (var i = _fireballs.Count - 1; i >= 0; i--)
        {
            var fireball = _fireballs[i];
            fireball.Move(deltaSeconds);
            if (fireball.DistanceLeft <= 0f
                || arena.IsSolid(fireball.Position)
                || (arena.Obstacles?.StrikeTouching(fireball.Position, fireball.Radius, 0f) ?? false)
                || HitsPlayer(fireball, players))
            {
                _fireballs.ReturnAt(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _fireballs.Count; i++)
        {
            var fireball = _fireballs[i];
            renderer.FillCircle(fireball.Position, fireball.Radius * GlowScale, GlowColor * 0.5f);
            renderer.FillCircle(fireball.Position, fireball.Radius, CoreColor);
        }
    }

    private static bool HitsPlayer(Fireball fireball, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            var reach = player.Radius + fireball.Radius;
            if (Vector2.DistanceSquared(player.Position, fireball.Position) <= reach * reach
                && player.TakeDamage(fireball.Damage))
            {
                return true;
            }
        }

        return false;
    }
}
