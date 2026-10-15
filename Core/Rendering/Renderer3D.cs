using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Experimental 3D look (Boxhead 2Play style) behind the same <see cref="IRenderer"/>: in a world
/// pass, shapes become lit solids (circles → cylinders, squares → boxes, triangles and hexagons →
/// prisms) and walls / pillars get height, while floor marks, markers, particles and lines stay
/// flat on the ground. Screen passes (HUD, menus, text) go to the 2D renderer unchanged.
/// Gameplay is untouched; only the picture changes.
/// </summary>
public sealed class Renderer3D : IRenderer, IDisposable
{
    private const int MaxSolidVertices = 120_000;
    private const int MaxFlatVertices = 300_000;
    private const int VerticesPerDraw = 60_000;
    private const int CircleSegments = 14;
    private const int FlatCircleSegments = 10;

    // A body is about this many times its radius tall.
    private const float BodyHeightPerRadius = 1.6f;

    // Flat things float just above the floor so the floor does not hide them.
    private const float FlatHeight = 0.5f;
    private const int BillboardSegments = 8;

    // Shadows fall away from the light (screen down-right), longer for taller things.
    private const float ShadowLengthPerHeight = 0.45f;
    private static readonly Vector2 ShadowDirection = Vector2.Normalize(new Vector2(0.55f, 0.4f));
    private static readonly Color ShadowColor = new(16, 17, 22);

    private static readonly RasterizerState NoCulling = new() { CullMode = CullMode.None };

    // Walls and pillars mark the pixels they cover in the stencil buffer...
    private static readonly DepthStencilState MarkOccluders = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = true,
        StencilEnable = true,
        StencilFunction = CompareFunction.Always,
        StencilPass = StencilOperation.Replace,
        ReferenceStencil = 1,
    };

    // ...so characters can be drawn again only where one of those is in front of them.
    private static readonly DepthStencilState BehindOccluders = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = false,
        DepthBufferFunction = CompareFunction.Greater,
        StencilEnable = true,
        StencilFunction = CompareFunction.Equal,
        ReferenceStencil = 1,
    };

    // How strongly a hidden character shows through a wall or pillar.
    private const float SilhouetteAlpha = 0.55f;

    private readonly GraphicsDevice _device;
    private readonly ShapeRenderer _screen;
    private readonly BasicEffect _solidEffect;
    private readonly BasicEffect _flatEffect;
    private readonly VertexPositionNormalColor[] _solids = new VertexPositionNormalColor[MaxSolidVertices];
    private readonly VertexPositionNormalColor[] _walls = new VertexPositionNormalColor[MaxSolidVertices];
    private readonly BasicEffect _silhouetteEffect;
    private int _wallCount;
    private bool _writingWalls;
    private readonly VertexPositionColor[] _flats = new VertexPositionColor[MaxFlatVertices];
    private int _solidCount;
    private int _flatCount;
    private bool _inWorld;
    private Vector3 _billboardRight = Vector3.Right;
    private Vector3 _billboardUp = Vector3.Up;
    private Camera2D? _camera;

    public Renderer3D(GraphicsDevice device, ShapeRenderer screen)
    {
        _device = device;
        _screen = screen;

        _solidEffect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = true };
        _solidEffect.EnableDefaultLighting();
        _solidEffect.AmbientLightColor = new Vector3(0.45f, 0.45f, 0.5f);
        _solidEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.45f, -1f, 0.33f));
        _solidEffect.DirectionalLight0.DiffuseColor = new Vector3(0.75f, 0.72f, 0.68f);
        _solidEffect.DirectionalLight1.Enabled = false;
        _solidEffect.DirectionalLight2.Enabled = false;
        _solidEffect.SpecularColor = Vector3.Zero;

        _flatEffect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false };
        _silhouetteEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true, LightingEnabled = false, Alpha = SilhouetteAlpha,
        };
    }

    public void BeginWorld(Camera2D camera)
    {
        camera.Is3D = true;
        _camera = camera;

        // Camera-facing directions for particles and other floating sprites.
        var view = camera.View3D;
        _billboardRight = new Vector3(view.M11, view.M21, view.M31);
        _billboardUp = new Vector3(view.M12, view.M22, view.M32);
        _inWorld = true;
        _solidCount = 0;
        _wallCount = 0;
        _flatCount = 0;
    }

    public void BeginScreen()
    {
        _inWorld = false;
        _screen.BeginScreen();
    }

    public void End()
    {
        if (_inWorld)
        {
            Flush();
            _inWorld = false;
            return;
        }

        _screen.End();
    }

    public void DrawVisual(in VisualDescriptor visual, Vector2 position, float rotation)
    {
        if (!_inWorld)
        {
            _screen.DrawVisual(visual, position, rotation);
            return;
        }

        // See-through things (flashes, glows) stay flat on the ground.
        if (visual.Color.A < 250)
        {
            FlatShape(visual, position, rotation);
            return;
        }

        var height = visual.Radius * BodyHeightPerRadius;
        Shadow(position, visual.Radius, height);
        switch (visual.Shape)
        {
            case ShapeKind.Square:
                Box(position, new Vector2(visual.Radius), 0f, height, visual.Color);
                break;

            case ShapeKind.Diamond:
                Prism(position, visual.Radius, 4, 0f, height, visual.Color);
                break;

            case ShapeKind.Triangle:
                Prism(position, visual.Radius, 3, rotation, height, visual.Color);
                break;

            case ShapeKind.Hexagon:
                Prism(position, visual.Radius, 6, rotation, height, visual.Color);
                break;

            default:
                Prism(position, visual.Radius, CircleSegments, 0f, height, visual.Color);
                break;
        }
    }

    public void FillRect(Vector2 topLeft, Vector2 size, Color color)
    {
        if (!_inWorld)
        {
            _screen.FillRect(topLeft, size, color);
            return;
        }

        FlatQuad(topLeft, topLeft + new Vector2(size.X, 0f), topLeft + size, topLeft + new Vector2(0f, size.Y), color);
    }

    public void FillBox(Vector2 topLeft, Vector2 size, float height, Color color)
    {
        if (!_inWorld)
        {
            _screen.FillRect(topLeft, size, color);
            return;
        }

        BoxShadow(topLeft, size, height);
        _writingWalls = true;
        Box(topLeft + size / 2f, size / 2f, 0f, height, color);
        _writingWalls = false;
    }

    public void FillCircleRaised(Vector2 position, float height, float radius, Color color)
    {
        if (!_inWorld)
        {
            _screen.FillCircle(position, radius, color);
            return;
        }

        Billboard(new Vector3(position.X, height, position.Y), radius, color);
    }

    public void DrawLineRaised(Vector2 from, Vector2 to, float height, float thickness, Color color)
    {
        if (!_inWorld)
        {
            _screen.DrawLine(from, to, thickness, color);
            return;
        }

        var along = to - from;
        if (along.LengthSquared() < 0.0001f)
        {
            return;
        }

        var side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (thickness / 2f);
        FlatQuadAt(from - side, to - side, to + side, from + side, height, color);
    }

    public void DrawParticle(Vector2 ground, float baseHeight, float lift, float radius, Color color)
    {
        if (!_inWorld)
        {
            _screen.DrawParticle(ground, baseHeight, lift, radius, color);
            return;
        }

        Billboard(new Vector3(ground.X, baseHeight + lift + radius * 0.5f, ground.Y), radius, color);
    }

    public void FillCircle(Vector2 center, float radius, Color color)
    {
        if (!_inWorld)
        {
            _screen.FillCircle(center, radius, color);
            return;
        }

        FlatPolygon(center, radius, FlatCircleSegments, 0f, color);
    }

    public void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        if (!_inWorld)
        {
            _screen.DrawLine(from, to, thickness, color);
            return;
        }

        var along = to - from;
        if (along.LengthSquared() < 0.0001f)
        {
            return;
        }

        var side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (thickness / 2f);
        FlatQuad(from - side, to - side, to + side, from + side, color);
    }

    public void DrawText(string text, Vector2 position, float size, Color color, TextAlign align = TextAlign.Left)
    {
        _screen.DrawText(text, position, size, color, align);
    }

    public void Dispose()
    {
        _solidEffect.Dispose();
        _silhouetteEffect.Dispose();
        _flatEffect.Dispose();
    }

    // Solids first (depth written), then flat things in the order they were drawn: they test
    // against the solids but not against each other, so the 2D draw order still holds for them.
    private void Flush()
    {
        if (_camera is null)
        {
            return;
        }

        var fullViewport = _device.Viewport;
        var view = _camera.ViewRect;
        _device.Viewport = new Viewport(view.X, view.Y, view.Width, view.Height);
        _device.RasterizerState = NoCulling;

        var viewMatrix = _camera.View3D;
        var projection = _camera.Projection3D;

        _device.BlendState = BlendState.Opaque;
        _device.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);
        _solidEffect.View = viewMatrix;
        _solidEffect.Projection = projection;
        _solidEffect.World = Matrix.Identity;
        _device.DepthStencilState = MarkOccluders;
        DrawInChunks(_walls, _wallCount, _solidEffect);
        _device.DepthStencilState = DepthStencilState.Default;
        DrawInChunks(_solids, _solidCount, _solidEffect);

        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _flatEffect.View = viewMatrix;
        _flatEffect.Projection = projection;
        _flatEffect.World = Matrix.Identity;
        DrawInChunks(_flats, _flatCount, _flatEffect);

        // Characters hidden behind a wall or pillar show through it as see-through silhouettes.
        _device.DepthStencilState = BehindOccluders;
        _silhouetteEffect.View = viewMatrix;
        _silhouetteEffect.Projection = projection;
        _silhouetteEffect.World = Matrix.Identity;
        DrawInChunks(_solids, _solidCount, _silhouetteEffect);

        _device.DepthStencilState = DepthStencilState.None;
        _device.Viewport = fullViewport;
    }

    private void DrawInChunks<T>(T[] vertices, int count, BasicEffect effect)
        where T : struct, IVertexType
    {
        for (var start = 0; start < count; start += VerticesPerDraw)
        {
            var length = Math.Min(VerticesPerDraw, count - start);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, start, length / 3);
            }
        }
    }

    // --- solids ---

    private void Box(Vector2 center, Vector2 half, float rotation, float height, Color color)
    {
        var a = center + Rotate(new Vector2(-half.X, -half.Y), rotation);
        var b = center + Rotate(new Vector2(half.X, -half.Y), rotation);
        var c = center + Rotate(new Vector2(half.X, half.Y), rotation);
        var d = center + Rotate(new Vector2(-half.X, half.Y), rotation);
        SideQuad(a, b, height, color);
        SideQuad(b, c, height, color);
        SideQuad(c, d, height, color);
        SideQuad(d, a, height, color);
        TopTriangle(a, b, c, height, color);
        TopTriangle(a, c, d, height, color);
    }

    // A regular prism standing on the floor; corner 0 points along the rotation.
    private void Prism(Vector2 center, float radius, int sides, float rotation, float height, Color color)
    {
        var previous = center + Direction(rotation) * radius;
        for (var i = 1; i <= sides; i++)
        {
            var corner = center + Direction(rotation + MathHelper.TwoPi * i / sides) * radius;
            SideQuad(previous, corner, height, color);
            TopTriangle(center, previous, corner, height, color);
            previous = corner;
        }
    }

    private void SideQuad(Vector2 from, Vector2 to, float height, Color color)
    {
        if ((_writingWalls ? _wallCount : _solidCount) + 6 > MaxSolidVertices)
        {
            return;
        }

        var outward = new Vector3(to.Y - from.Y, 0f, -(to.X - from.X));
        var normal = outward.LengthSquared() > 0f ? Vector3.Normalize(outward) : Vector3.Up;
        var b0 = new Vector3(from.X, 0f, from.Y);
        var b1 = new Vector3(to.X, 0f, to.Y);
        var t0 = new Vector3(from.X, height, from.Y);
        var t1 = new Vector3(to.X, height, to.Y);
        Solid(b0, normal, color);
        Solid(b1, normal, color);
        Solid(t1, normal, color);
        Solid(b0, normal, color);
        Solid(t1, normal, color);
        Solid(t0, normal, color);
    }

    private void TopTriangle(Vector2 a, Vector2 b, Vector2 c, float height, Color color)
    {
        if ((_writingWalls ? _wallCount : _solidCount) + 3 > MaxSolidVertices)
        {
            return;
        }

        Solid(new Vector3(a.X, height, a.Y), Vector3.Up, color);
        Solid(new Vector3(b.X, height, b.Y), Vector3.Up, color);
        Solid(new Vector3(c.X, height, c.Y), Vector3.Up, color);
    }

    private void Solid(Vector3 position, Vector3 normal, Color color)
    {
        var vertex = new VertexPositionNormalColor(position, normal, color);
        if (_writingWalls)
        {
            _walls[_wallCount++] = vertex;
        }
        else
        {
            _solids[_solidCount++] = vertex;
        }
    }

    // --- flat things ---

    private void FlatShape(in VisualDescriptor visual, Vector2 position, float rotation)
    {
        switch (visual.Shape)
        {
            case ShapeKind.Square:
                FlatPolygon(position, visual.Radius * MathF.Sqrt(2f), 4, MathHelper.PiOver4, visual.Color);
                break;

            case ShapeKind.Diamond:
                FlatPolygon(position, visual.Radius, 4, 0f, visual.Color);
                break;

            case ShapeKind.Triangle:
                FlatPolygon(position, visual.Radius, 3, rotation, visual.Color);
                break;

            case ShapeKind.Hexagon:
                FlatPolygon(position, visual.Radius, 6, rotation, visual.Color);
                break;

            default:
                FlatPolygon(position, visual.Radius, FlatCircleSegments, 0f, visual.Color);
                break;
        }
    }

    private void FlatPolygon(Vector2 center, float radius, int sides, float rotation, Color color)
    {
        var previous = center + Direction(rotation) * radius;
        for (var i = 1; i <= sides; i++)
        {
            var corner = center + Direction(rotation + MathHelper.TwoPi * i / sides) * radius;
            FlatTriangle(center, previous, corner, color);
            previous = corner;
        }
    }

    private void FlatQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        FlatQuadAt(a, b, c, d, FlatHeight, color);
    }

    private void FlatQuadAt(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float height, Color color)
    {
        FlatTriangleAt(a, b, c, height, color);
        FlatTriangleAt(a, c, d, height, color);
    }

    private void FlatTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        FlatTriangleAt(a, b, c, FlatHeight, color);
    }

    private void FlatTriangleAt(Vector2 a, Vector2 b, Vector2 c, float height, Color color)
    {
        Triangle(new Vector3(a.X, height, a.Y), new Vector3(b.X, height, b.Y), new Vector3(c.X, height, c.Y), color);
    }

    private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        if (_flatCount + 3 > MaxFlatVertices)
        {
            return;
        }

        _flats[_flatCount++] = new VertexPositionColor(a, color);
        _flats[_flatCount++] = new VertexPositionColor(b, color);
        _flats[_flatCount++] = new VertexPositionColor(c, color);
    }

    // A disc in the camera's plane, so it looks round from any angle.
    private void Billboard(Vector3 center, float radius, Color color)
    {
        var previous = center + _billboardRight * radius;
        for (var i = 1; i <= BillboardSegments; i++)
        {
            var angle = MathHelper.TwoPi * i / BillboardSegments;
            var corner = center + (_billboardRight * MathF.Cos(angle) + _billboardUp * MathF.Sin(angle)) * radius;
            Triangle(center, previous, corner, color);
            previous = corner;
        }
    }

    // A round body's shadow: its footprint stretched away from the light.
    private void Shadow(Vector2 position, float radius, float height)
    {
        var offset = ShadowDirection * (height * ShadowLengthPerHeight);
        FlatPolygon(position + offset, radius, FlatCircleSegments, 0f, ShadowColor);
        var side = new Vector2(-ShadowDirection.Y, ShadowDirection.X) * radius;
        FlatQuad(position - side, position + side, position + offset + side, position + offset - side, ShadowColor);
    }

    // A block's shadow: its footprint and the same footprint moved away from the light, joined up.
    private void BoxShadow(Vector2 topLeft, Vector2 size, float height)
    {
        var offset = ShadowDirection * (height * ShadowLengthPerHeight);
        var a = topLeft;
        var b = topLeft + new Vector2(size.X, 0f);
        var c = topLeft + size;
        var d = topLeft + new Vector2(0f, size.Y);
        FlatQuad(a + offset, b + offset, c + offset, d + offset, ShadowColor);
        FlatQuad(a, b, b + offset, a + offset, ShadowColor);
        FlatQuad(b, c, c + offset, b + offset, ShadowColor);
        FlatQuad(c, d, d + offset, c + offset, ShadowColor);
        FlatQuad(d, a, a + offset, d + offset, ShadowColor);
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        if (angle == 0f)
        {
            return vector;
        }

        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }
}
