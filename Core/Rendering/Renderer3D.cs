using System;
using Fourgate.Core.Rendering.Models;
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
    private const int BarrelSegments = 16;
    private const int MaxModelParts = 48;
    private const int MaxWallStainVertices = 6 * 1024;
    private const int SootTextureSize = 64;


    // Flat things float just above the floor so the floor does not hide them.
    private const float FlatHeight = 0.5f;
    private const int BillboardSegments = 8;

    // The sun stands overhead (as in Boxhead): every shadow lies right under its object, a soft
    // see-through blob, darker in the middle; blocks get a soft band hugging their base.
    private static readonly Color ShadowColor = new Color(0, 0, 0) * 0.32f;
    private static readonly Color ShadowCoreColor = new Color(0, 0, 0) * 0.22f;
    private const float ShadowSpread = 1.15f;
    private const float ShadowCore = 0.7f;
    private const float BoxShadowMargin = 7f;

    // A flying thing's shadow shrinks and fades the higher it is.
    private const float FlyingShadowFadeHeight = 400f;

    // Flying things cast a softer, see-through shadow.
    private static readonly Color FlyingShadowColor = new Color(0, 0, 0) * 0.4f;

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
    private readonly BasicEffect _stainEffect;
    private readonly VertexPositionTexture[] _stainQuad = new VertexPositionTexture[6];
    private int _stainsAt = -1;
    private readonly Matrix[] _partWorlds = new Matrix[MaxModelParts];
    private readonly Matrix[] _heldWorlds = new Matrix[MaxModelParts];
    private readonly Vector3[] _corners = new Vector3[8];
    private static readonly ModelPose StillPose = new(string.Empty, Vector2.Zero, 0f, 0f, 0f, 0f, Color.White);

    // Corners of each box face (bit 1: +x, 2: +y, 4: +z) and the face's normal before turning.
    private static readonly int[] FaceCorners = { 1, 3, 7, 5, 0, 4, 6, 2, 2, 6, 7, 3, 0, 1, 5, 4, 4, 5, 7, 6, 0, 2, 3, 1 };
    private static readonly Vector3[] FaceNormals =
    {
        Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ,
    };
    private readonly BasicEffect _wallStainEffect;
    private readonly Texture2D _sootTexture;
    private readonly VertexPositionColorTexture[] _wallStains = new VertexPositionColorTexture[MaxWallStainVertices];
    private int _wallStainCount;

    public Renderer3D(GraphicsDevice device, ShapeRenderer screen)
    {
        _device = device;
        _screen = screen;

        _solidEffect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = true };
        _solidEffect.EnableDefaultLighting();
        // The sun from almost straight above, and a weak fill from the camera's side so the faces
        // turned toward the player are not left dark.
        _solidEffect.AmbientLightColor = new Vector3(0.5f, 0.5f, 0.54f);
        _solidEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.12f, -1f, 0.18f));
        _solidEffect.DirectionalLight0.DiffuseColor = new Vector3(0.62f, 0.6f, 0.56f);
        _solidEffect.DirectionalLight1.Enabled = true;
        _solidEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0f, -0.5f, -1f));
        _solidEffect.DirectionalLight1.DiffuseColor = new Vector3(0.22f, 0.22f, 0.26f);
        _solidEffect.DirectionalLight1.SpecularColor = Vector3.Zero;
        _solidEffect.DirectionalLight2.Enabled = false;
        _solidEffect.SpecularColor = Vector3.Zero;

        _flatEffect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false };
        _stainEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = false, LightingEnabled = false };
        _wallStainEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };
        _sootTexture = CreateSootTexture(device);
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
        _stainsAt = -1;
        _wallStainCount = 0;
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

        var height = Depth3D.BodyHeight(visual.Radius);
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

    public void FillBox(Vector2 topLeft, Vector2 size, float height, Color color, bool castShadow = true)
    {
        if (!_inWorld)
        {
            _screen.FillRect(topLeft, size, color);
            return;
        }

        if (castShadow)
        {
            BoxShadow(topLeft, size, height);
        }

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

        // A ribbon turned to face the camera, so a beam looks round from any angle.
        var start = new Vector3(from.X, height, from.Y);
        var end = new Vector3(to.X, height, to.Y);
        var direction = Vector3.Normalize(end - start);
        var towardCamera = Vector3.Cross(_billboardRight, _billboardUp);
        var side = Vector3.Cross(direction, towardCamera);
        if (side.LengthSquared() < 0.0001f)
        {
            side = new Vector3(-direction.Z, 0f, direction.X);
        }

        side = Vector3.Normalize(side) * (thickness / 2f);
        Triangle(start - side, end - side, end + side, color);
        Triangle(start - side, end + side, start + side, color);
    }


    /// <summary>The models characters and guns are drawn with; null draws plain shapes.</summary>
    public ModelLibrary? Models { get; set; }

    public void DrawModel(in ModelPose pose, in VisualDescriptor fallback)
    {
        if (!_inWorld)
        {
            _screen.DrawModel(pose, fallback);
            return;
        }

        if (Models is null || !Models.TryGet(pose.ModelId, out var model) || model.Parts.Count > MaxModelParts)
        {
            DrawVisual(fallback, pose.Position, pose.Facing);
            return;
        }

        if (pose.ShadowRadius > 0f && pose.Elevation > 0f)
        {
            DrawShadow(pose.Position, pose.Elevation + model.Height, pose.ShadowRadius);
        }
        else if (pose.ShadowRadius > 0f && pose.Fall < 0.5f)
        {
            Shadow(pose.Position, pose.ShadowRadius * (1f - pose.Shrink), model.Height * (1f - pose.Shrink));
        }

        var root = ModelAnimator.Root(pose);
        ModelAnimator.Pose(model, pose, root, _partWorlds);
        var parts = model.Parts;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.IsVisible)
            {
                var color = PartColor(part, pose.Tint);
                if (pose.Glow > 0f)
                {
                    color = Color.Lerp(color, pose.GlowColor, Math.Clamp(pose.Glow, 0f, 1f));
                }

                OrientedBox(_partWorlds[i], part.Center, part.HalfSize, color);
            }

            if (pose.GunId is not null && HoldsGunHere(part.Mount, pose.Hold))
            {
                DrawHeldModel(pose.GunId, _partWorlds[i]);
            }
        }
    }

    // One gun goes in the right hand or at the chest; dual wielding puts one in each hand.
    private static bool HoldsGunHere(ModelMount mount, ModelHold hold)
    {
        return hold switch
        {
            ModelHold.TwoHands => mount == ModelMount.GunTwoHands,
            ModelHold.Dual => mount is ModelMount.Gun or ModelMount.GunLeft,
            ModelHold.Shoulder => mount == ModelMount.GunShoulder,
            _ => mount == ModelMount.Gun,
        };
    }

    // A held model (a gun): drawn in the hand's space, unanimated.
    private void DrawHeldModel(string id, Matrix hand)
    {
        if (Models is null || !Models.TryGet(id, out var held) || held.Parts.Count > MaxModelParts)
        {
            return;
        }

        ModelAnimator.Pose(held, StillPose, hand, _heldWorlds);
        for (var i = 0; i < held.Parts.Count; i++)
        {
            var part = held.Parts[i];
            if (part.IsVisible)
            {
                OrientedBox(_heldWorlds[i], part.Center, part.HalfSize, PartColor(part, part.Color));
            }
        }
    }

    private static Color PartColor(ModelPart part, Color tint)
    {
        var color = part.Tint ? tint : part.Color;
        if (part.Shade == 1f)
        {
            return new Color(color.R, color.G, color.B);
        }

        return new Color(Vector3.Clamp(color.ToVector3() * part.Shade, Vector3.Zero, Vector3.One));
    }

    // A box turned any way (a model part): its 8 corners placed by the part's matrix, each face
    // lit by its turned normal.
    private void OrientedBox(in Matrix world, Vector3 center, Vector3 half, Color color)
    {
        if (_solidCount + 36 > MaxSolidVertices)
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            var corner = center + new Vector3(
                (i & 1) != 0 ? half.X : -half.X, (i & 2) != 0 ? half.Y : -half.Y, (i & 4) != 0 ? half.Z : -half.Z);
            _corners[i] = Vector3.Transform(corner, world);
        }

        for (var face = 0; face < 6; face++)
        {
            var normal = Vector3.TransformNormal(FaceNormals[face], world);
            normal = normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : Vector3.Up;
            var a = _corners[FaceCorners[face * 4]];
            var b = _corners[FaceCorners[face * 4 + 1]];
            var c = _corners[FaceCorners[face * 4 + 2]];
            var d = _corners[FaceCorners[face * 4 + 3]];
            Solid(a, normal, color);
            Solid(b, normal, color);
            Solid(c, normal, color);
            Solid(a, normal, color);
            Solid(c, normal, color);
            Solid(d, normal, color);
        }
    }

    public void DrawLowBody(in VisualDescriptor visual, Vector2 position, float height)
    {
        if (!_inWorld)
        {
            _screen.DrawVisual(visual, position, 0f);
            return;
        }

        if (visual.Shape == ShapeKind.Square)
        {
            Box(position, new Vector2(visual.Radius), 0f, height, visual.Color);
            return;
        }

        Prism(position, visual.Radius, CircleSegments, 0f, height, visual.Color);
    }

    public void DrawBarrel(Vector2 position, float radius, float height, Color body, Color band)
    {
        if (!_inWorld)
        {
            _screen.DrawBarrel(position, radius, height, body, band);
            return;
        }

        // A drum: a slightly narrower body, dark rims at the foot and top, two bands round the
        // middle, and a dark lid with a bung on top.
        var rim = Color.Lerp(body, Color.Black, 0.45f);
        Shadow(position, radius, height);
        Prism(position, radius * 0.9f, BarrelSegments, 0f, height * 0.985f, body);
        Prism(position, radius, BarrelSegments, 0f, height * 0.07f, rim);
        Prism(position, radius * 0.96f, BarrelSegments, 0f, height * 0.08f, band, height * 0.3f);
        Prism(position, radius * 0.96f, BarrelSegments, 0f, height * 0.08f, band, height * 0.62f);
        Prism(position, radius, BarrelSegments, 0f, height * 0.06f, rim, height * 0.94f);
        FlatPolygon(position, radius * 0.8f, BarrelSegments, 0f, rim, height + 0.3f);
        FlatPolygon(position + new Vector2(radius * 0.4f, -radius * 0.25f), radius * 0.14f, 8, 0f, band, height + 0.6f);
    }

    public void DrawWallStain(Vector2 from, Vector2 to, float height, Color color)
    {
        if (!_inWorld || height <= 0f || _wallStainCount + 6 > _wallStains.Length)
        {
            return;
        }

        var b0 = new Vector3(from.X, 0f, from.Y);
        var b1 = new Vector3(to.X, 0f, to.Y);
        var t0 = new Vector3(from.X, height, from.Y);
        var t1 = new Vector3(to.X, height, to.Y);
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(b0, color, new Vector2(0f, 1f));
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(b1, color, new Vector2(1f, 1f));
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(t1, color, new Vector2(1f, 0f));
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(b0, color, new Vector2(0f, 1f));
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(t1, color, new Vector2(1f, 0f));
        _wallStains[_wallStainCount++] = new VertexPositionColorTexture(t0, color, new Vector2(0f, 0f));
    }

    /// <summary>The floor-stain image; nothing is drawn while it is null.</summary>
    public Texture2D? FloorStainTexture { get; set; }

    public void DrawFloorStains(Vector2 topLeft, Vector2 size)
    {
        if (!_inWorld)
        {
            _screen.DrawFloorStains(topLeft, size);
            return;
        }

        if (FloorStainTexture is null)
        {
            return;
        }

        // Drawn in its place among the flat things: after the floor, before what lies on it.
        _stainsAt = _flatCount;
        var a = new Vector3(topLeft.X, FlatHeight, topLeft.Y);
        var b = new Vector3(topLeft.X + size.X, FlatHeight, topLeft.Y);
        var c = new Vector3(topLeft.X + size.X, FlatHeight, topLeft.Y + size.Y);
        var d = new Vector3(topLeft.X, FlatHeight, topLeft.Y + size.Y);
        _stainQuad[0] = new VertexPositionTexture(a, new Vector2(0f, 0f));
        _stainQuad[1] = new VertexPositionTexture(b, new Vector2(1f, 0f));
        _stainQuad[2] = new VertexPositionTexture(c, new Vector2(1f, 1f));
        _stainQuad[3] = new VertexPositionTexture(a, new Vector2(0f, 0f));
        _stainQuad[4] = new VertexPositionTexture(c, new Vector2(1f, 1f));
        _stainQuad[5] = new VertexPositionTexture(d, new Vector2(0f, 1f));
    }

    public void DrawVisualElevated(in VisualDescriptor visual, Vector2 position, float rotation, float elevation)
    {
        if (!_inWorld)
        {
            _screen.DrawVisual(visual, position, rotation);
            return;
        }

        var height = Depth3D.BodyHeight(visual.Radius);
        Shadow(position, visual.Radius, elevation + height);
        switch (visual.Shape)
        {
            case ShapeKind.Square:
                Box(position, new Vector2(visual.Radius), 0f, elevation + height, visual.Color, elevation);
                break;

            case ShapeKind.Diamond:
                Prism(position, visual.Radius, 4, 0f, height, visual.Color, elevation);
                break;

            case ShapeKind.Triangle:
                Prism(position, visual.Radius, 3, rotation, height, visual.Color, elevation);
                break;

            case ShapeKind.Hexagon:
                Prism(position, visual.Radius, 6, rotation, height, visual.Color, elevation);
                break;

            default:
                Prism(position, visual.Radius, CircleSegments, 0f, height, visual.Color, elevation);
                break;
        }
    }

    public void DrawGlowWall(Vector2 from, Vector2 to, float height, Color color)
    {
        if (!_inWorld || height <= 0f)
        {
            return;
        }

        var b0 = new Vector3(from.X, FlatHeight, from.Y);
        var b1 = new Vector3(to.X, FlatHeight, to.Y);
        var t0 = new Vector3(from.X, height, from.Y);
        var t1 = new Vector3(to.X, height, to.Y);
        var top = Color.Transparent;
        Triangle(b0, color, b1, color, t1, top);
        Triangle(b0, color, t1, top, t0, top);
    }

    public void DrawShadow(Vector2 position, float height, float radius)
    {
        if (!_inWorld)
        {
            return;
        }

        // Right under it, smaller and fainter the higher it flies.
        var away = Math.Clamp(height / FlyingShadowFadeHeight, 0f, 0.6f);
        FlatPolygon(position, radius * (1f - away * 0.5f), FlatCircleSegments, 0f, FlyingShadowColor * (1f - away));
    }

    public void DrawBar(Vector2 from, Vector2 to, float thickness, float bottom, float top, Color color)
    {
        if (!_inWorld)
        {
            _screen.DrawLine(from, to, thickness, color);
            return;
        }

        var along = to - from;
        var length = along.Length();
        if (length < 0.001f)
        {
            return;
        }

        Box((from + to) / 2f, new Vector2(length / 2f, thickness / 2f), MathF.Atan2(along.Y, along.X), top, color, bottom);
    }

    public void FillRectRaised(Vector2 topLeft, Vector2 size, float height, Color color)
    {
        if (!_inWorld)
        {
            _screen.FillRect(topLeft, size, color);
            return;
        }

        FlatQuadAt(topLeft, topLeft + new Vector2(size.X, 0f), topLeft + size, topLeft + new Vector2(0f, size.Y), height, color);
    }

    public void DrawVisualRaised(in VisualDescriptor visual, Vector2 position, float rotation, float height)
    {
        if (!_inWorld)
        {
            _screen.DrawVisual(visual, position, rotation);
            return;
        }

        FlatShape(visual, position, rotation, height);
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

    public void DrawTextCentered(string text, Vector2 center, float size, Color color)
    {
        _screen.DrawTextCentered(text, center, size, color);
    }

    public void Dispose()
    {
        _sootTexture.Dispose();
        _wallStainEffect.Dispose();
        _solidEffect.Dispose();
        _silhouetteEffect.Dispose();
        _flatEffect.Dispose();
        _stainEffect.Dispose();
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
        DrawInChunks(_walls, 0, _wallCount, _solidEffect);
        _device.DepthStencilState = DepthStencilState.Default;
        DrawInChunks(_solids, 0, _solidCount, _solidEffect);

        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _flatEffect.View = viewMatrix;
        _flatEffect.Projection = projection;
        _flatEffect.World = Matrix.Identity;
        if (_stainsAt >= 0 && FloorStainTexture is not null)
        {
            DrawInChunks(_flats, 0, _stainsAt, _flatEffect);
            DrawStainQuad(viewMatrix, projection);
            DrawInChunks(_flats, _stainsAt, _flatCount, _flatEffect);
        }
        else
        {
            DrawInChunks(_flats, 0, _flatCount, _flatEffect);
        }

        if (_wallStainCount > 0)
        {
            _wallStainEffect.View = viewMatrix;
            _wallStainEffect.Projection = projection;
            _wallStainEffect.World = Matrix.Identity;
            _wallStainEffect.Texture = _sootTexture;
            _device.SamplerStates[0] = SamplerState.LinearClamp;
            foreach (var pass in _wallStainEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, _wallStains, 0, _wallStainCount / 3);
            }
        }

        // Characters hidden behind a wall or pillar show through it as see-through silhouettes.
        _device.DepthStencilState = BehindOccluders;
        _silhouetteEffect.View = viewMatrix;
        _silhouetteEffect.Projection = projection;
        _silhouetteEffect.World = Matrix.Identity;
        DrawInChunks(_solids, 0, _solidCount, _silhouetteEffect);

        _device.DepthStencilState = DepthStencilState.None;
        _device.Viewport = fullViewport;
    }

    // Soot: solid at the foot, thinning upward, soft at both sides, with a few darker streaks
    // running up so it does not look like a plain gradient. White, tinted per patch.
    private static Texture2D CreateSootTexture(GraphicsDevice device)
    {
        var texture = new Texture2D(device, SootTextureSize, SootTextureSize);
        var pixels = new Color[SootTextureSize * SootTextureSize];
        for (var y = 0; y < SootTextureSize; y++)
        {
            var up = 1f - y / (SootTextureSize - 1f);
            for (var x = 0; x < SootTextureSize; x++)
            {
                var across = x / (SootTextureSize - 1f);
                var side = Math.Clamp((1f - MathF.Abs(across * 2f - 1f)) / 0.5f, 0f, 1f);
                var streaks = 0.75f + 0.25f * MathF.Sin(across * 23f + MathF.Sin(across * 7f) * 2f);

                // Streaks reach higher than the soot between them.
                var reach = 0.55f + 0.45f * streaks;
                var rise = Math.Clamp(1f - up / reach, 0f, 1f);
                var alpha = side * side * (3f - 2f * side) * rise * rise * (0.8f + 0.2f * streaks);
                pixels[y * SootTextureSize + x] = Color.White * alpha;
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private void DrawStainQuad(Matrix view, Matrix projection)
    {
        _stainEffect.View = view;
        _stainEffect.Projection = projection;
        _stainEffect.World = Matrix.Identity;
        _stainEffect.Texture = FloorStainTexture;
        _device.SamplerStates[0] = SamplerState.LinearClamp;
        foreach (var pass in _stainEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _stainQuad, 0, 2);
        }
    }

    // Draws vertices [from, to) a chunk at a time.
    private void DrawInChunks<T>(T[] vertices, int from, int to, BasicEffect effect)
        where T : struct, IVertexType
    {
        for (var start = from; start < to; start += VerticesPerDraw)
        {
            var length = Math.Min(VerticesPerDraw, to - start);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, start, length / 3);
            }
        }
    }

    // --- solids ---

    private void Box(Vector2 center, Vector2 half, float rotation, float height, Color color, float bottom = 0f)
    {
        var a = center + Rotate(new Vector2(-half.X, -half.Y), rotation);
        var b = center + Rotate(new Vector2(half.X, -half.Y), rotation);
        var c = center + Rotate(new Vector2(half.X, half.Y), rotation);
        var d = center + Rotate(new Vector2(-half.X, half.Y), rotation);
        SideQuad(a, b, height, color, bottom);
        SideQuad(b, c, height, color, bottom);
        SideQuad(c, d, height, color, bottom);
        SideQuad(d, a, height, color, bottom);
        TopTriangle(a, b, c, height, color);
        TopTriangle(a, c, d, height, color);
    }

    // A regular prism standing on the floor; corner 0 points along the rotation.
    private void Prism(Vector2 center, float radius, int sides, float rotation, float height, Color color, float bottom = 0f)
    {
        var previous = center + Direction(rotation) * radius;
        for (var i = 1; i <= sides; i++)
        {
            var corner = center + Direction(rotation + MathHelper.TwoPi * i / sides) * radius;
            SideQuad(previous, corner, bottom + height, color, bottom);
            TopTriangle(center, previous, corner, bottom + height, color);
            previous = corner;
        }
    }

    private void SideQuad(Vector2 from, Vector2 to, float height, Color color, float bottom = 0f)
    {
        if ((_writingWalls ? _wallCount : _solidCount) + 6 > MaxSolidVertices)
        {
            return;
        }

        var outward = new Vector3(to.Y - from.Y, 0f, -(to.X - from.X));
        var normal = outward.LengthSquared() > 0f ? Vector3.Normalize(outward) : Vector3.Up;
        var b0 = new Vector3(from.X, bottom, from.Y);
        var b1 = new Vector3(to.X, bottom, to.Y);
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

    private void FlatShape(in VisualDescriptor visual, Vector2 position, float rotation, float height = FlatHeight)
    {
        switch (visual.Shape)
        {
            case ShapeKind.Square:
                FlatPolygon(position, visual.Radius * MathF.Sqrt(2f), 4, MathHelper.PiOver4, visual.Color, height);
                break;

            case ShapeKind.Diamond:
                FlatPolygon(position, visual.Radius, 4, 0f, visual.Color, height);
                break;

            case ShapeKind.Triangle:
                FlatPolygon(position, visual.Radius, 3, rotation, visual.Color, height);
                break;

            case ShapeKind.Hexagon:
                FlatPolygon(position, visual.Radius, 6, rotation, visual.Color, height);
                break;

            default:
                FlatPolygon(position, visual.Radius, FlatCircleSegments, 0f, visual.Color, height);
                break;
        }
    }

    private void FlatPolygon(Vector2 center, float radius, int sides, float rotation, Color color, float height = FlatHeight)
    {
        var previous = center + Direction(rotation) * radius;
        for (var i = 1; i <= sides; i++)
        {
            var corner = center + Direction(rotation + MathHelper.TwoPi * i / sides) * radius;
            FlatTriangleAt(center, previous, corner, height, color);
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
        Triangle(a, color, b, color, c, color);
    }

    private void Triangle(Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
    {
        if (_flatCount + 3 > MaxFlatVertices)
        {
            return;
        }

        _flats[_flatCount++] = new VertexPositionColor(a, colorA);
        _flats[_flatCount++] = new VertexPositionColor(b, colorB);
        _flats[_flatCount++] = new VertexPositionColor(c, colorC);
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
    // A soft blob right under a body: a wide faint rim and a darker core (the height no longer
    // matters with the sun overhead).
    private void Shadow(Vector2 position, float radius, float height)
    {
        FlatPolygon(position, radius * ShadowSpread, FlatCircleSegments, 0f, ShadowColor);
        FlatPolygon(position, radius * ShadowCore, FlatCircleSegments, 0f, ShadowCoreColor);
    }

    // A block's shadow: a soft band just around its base.
    private void BoxShadow(Vector2 topLeft, Vector2 size, float height)
    {
        var margin = new Vector2(BoxShadowMargin);
        var a = topLeft - margin;
        var c = topLeft + size + margin;
        FlatQuad(a, new Vector2(c.X, a.Y), c, new Vector2(a.X, c.Y), ShadowColor);
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
