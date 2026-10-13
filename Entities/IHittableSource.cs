using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>A group of hittable things that can be searched by area.</summary>
public interface IHittableSource
{
    /// <summary>
    /// Appends everything that may overlap the given circle to <paramref name="results"/>.
    /// May include near misses; the caller does the exact test.
    /// </summary>
    void Collect(Vector2 center, float radius, List<IHittable> results);
}
