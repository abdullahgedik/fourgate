using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>A lit, coloured 3D vertex (position, normal, colour) for the 3D renderer's solids.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct VertexPositionNormalColor : IVertexType
{
    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Color Color;

    public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
