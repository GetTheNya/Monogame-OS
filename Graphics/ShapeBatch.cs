using System;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Graphics;

public class ShapeBatch : IFontStashRenderer2 {
    private const int _initialSprites = 2048;
    private const int _initialTriangles = _initialSprites * 2;
    private const int _initialVertices = _initialSprites * 4;
    private const int _initialIndices = _initialSprites * 6;

    private const int MaxBatchSize = short.MaxValue / 6;

    private GraphicsDevice _graphicsDevice;
    private VertexShape[] _vertices;
    private int[] _indices;

    private ShapeBatchItem[] _batchItemList;
    private int _batchItemCount;

    private Matrix _view;
    private Matrix _projection;
    private Effect _effect;

    private EffectParameter _screenSizeParam;
    private EffectParameter _blurredTextureParam;
    private EffectParameter _blurUVOffsetParam;

    public Texture2D BlurredBackground { get; set; }
    
    /// <summary>
    /// Offset for correct blur UV sampling when rendering to a local RenderTarget.
    /// Set to the screen position of the RT's origin (e.g., window content area position).
    /// </summary>
    public Vector2 BlurUVOffset { get; set; } = Vector2.Zero;
    
    /// <summary>
    /// Override for ScreenSize parameter when rendering to a local RenderTarget.
    /// Set to the actual screen size (not RT viewport size) for correct blur UV calculation.
    /// If null, uses current viewport size.
    /// </summary>
    public Vector2? ScreenSizeOverride { get; set; } = null;

    private float _pixelSize = 1f;
    private float _aaSize = 2f;
    private float _aaOffset = 1f;

    private int _fromIndex = 0;

    private bool _isBatchRunning;

    public ShapeBatch(GraphicsDevice graphicsDevice, ContentManager content) {
        _graphicsDevice = graphicsDevice;

        _effect = content.Load<Effect>("apos-shapes");

        _screenSizeParam = _effect.Parameters["ScreenSize"];
        _blurredTextureParam = _effect.Parameters["BlurredBackgroundTexture"];
        _blurUVOffsetParam = _effect.Parameters["BlurUVOffset"];

        _batchItemList = new ShapeBatchItem[_initialSprites];
        _vertices = new VertexShape[_initialVertices];
        _indices = new int[_initialIndices];

        for (int i = 0; i < _initialSprites; i++) {
            _batchItemList[i] = new ShapeBatchItem();
        }

        _batchItemCount = 0;

        GenerateIndexArray();

        _isBatchRunning = false;
    }

    public void Begin(Matrix? view = null, Matrix? projection = null) {
        if (_isBatchRunning) {
            throw new InvalidOperationException("Begin cannot be called again until End has been successfully called.");
        }

        _isBatchRunning = true;

        if (view != null) {
            _view = view.Value;
        } else {
            _view = Matrix.Identity;
        }

        if (projection != null) {
            _projection = projection.Value;
        } else {
            Viewport viewport = _graphicsDevice.Viewport;
            _projection = Matrix.CreateOrthographicOffCenter(viewport.X, viewport.Width, viewport.Height, viewport.Y, 0, 1);
        }

        _pixelSize = ScreenToWorldScale();
        _aaOffset = _pixelSize * _aaSize;
    }

    public void DrawCircle(Vector2 center, float radius, Color c1, Color c2, float thickness = 1f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        float radius1 = radius + _aaOffset; // Account for AA.

        var topLeft = center + new Vector2(-radius1);
        var topRight = center + new Vector2(radius1, -radius1);
        var bottomRight = center + new Vector2(radius1);
        var bottomLeft = center + new Vector2(-radius1, radius1);

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-radius1, -radius1), 1f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(radius1, -radius1), 1f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(radius1, radius1), 1f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-radius1, radius1), 1f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillCircle(Vector2 center, float radius, Color c) {
        DrawCircle(center, radius, c, c, 0f);
    }

    public void BorderCircle(Vector2 center, float radius, Color c, float thickness = 1f) {
        DrawCircle(center, radius, Color.Transparent, c, thickness);
    }

    public void DrawRectangle(Vector2 xy, Vector2 size, Color c1, Color c2, float thickness = 1f, float rounded = 0f, float rotation = 0f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        rounded = MathF.Min(MathF.Min(rounded, size.X / 2f), size.Y / 2f);

        xy -= new Vector2(_aaOffset); // Account for AA.
        Vector2 size1 = size + new Vector2(_aaOffset * 2f); // Account for AA.
        Vector2 half = size / 2f;
        Vector2 half1 = half + new Vector2(_aaOffset); // Account for AA.

        half -= new Vector2(rounded);

        var topLeft = xy;
        var topRight = xy + new Vector2(size1.X, 0);
        var bottomRight = xy + size1;
        var bottomLeft = xy + new Vector2(0, size1.Y);

        if (rotation != 0f) {
            Vector2 center = xy + half1;
            topLeft = Rotate(topLeft, center, rotation);
            topRight = Rotate(topRight, center, rotation);
            bottomRight = Rotate(bottomRight, center, rotation);
            bottomLeft = Rotate(bottomLeft, center, rotation);
        }

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-half1.X, -half1.Y), 2f, c1, c2, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(half1.X, -half1.Y), 2f, c1, c2, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(half1.X, half1.Y), 2f, c1, c2, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-half1.X, half1.Y), 2f, c1, c2, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillRectangle(Vector2 xy, Vector2 size, Color c, float rounded = 0f, float rotation = 0f) {
        DrawRectangle(xy, size, c, c, 0f, rounded, rotation);
    }

    public void BorderRectangle(Vector2 xy, Vector2 size, Color c, float thickness = 1f, float rounded = 0f, float rotation = 0f) {
        DrawRectangle(xy, size, Color.Transparent, c, thickness, rounded, rotation);
    }

    public void DrawLine(Vector2 a, Vector2 b, float radius, Color c1, Color c2, float thickness = 1f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        var radius1 = radius + _aaOffset; // Account for AA.

        var c = Slide(a, b, radius1);
        var d = Slide(b, a, radius1);

        var topLeft = CounterClockwise(d, c, radius1);
        var topRight = Clockwise(d, c, radius1);
        var bottomRight = CounterClockwise(c, d, radius1);
        var bottomLeft = Clockwise(c, d, radius1);

        var width1 = radius + radius1;
        var height = Vector2.Distance(a, b) + radius;
        var height1 = Vector2.Distance(topLeft, bottomLeft) - _aaOffset; // Account for AA.

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-_aaOffset, -_aaOffset), 3f, c1, c2, thickness, radius, _pixelSize, height, aaSize: _aaSize);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(width1, -_aaOffset), 3f, c1, c2, thickness, radius, _pixelSize, height, aaSize: _aaSize);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(width1, height1), 3f, c1, c2, thickness, radius, _pixelSize, height, aaSize: _aaSize);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-_aaOffset, height1), 3f, c1, c2, thickness, radius, _pixelSize, height, aaSize: _aaSize);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillLine(Vector2 a, Vector2 b, float radius, Color c) {
        DrawLine(a, b, radius, c, c, 0f);
    }

    public void BorderLine(Vector2 a, Vector2 b, float radius, Color c, float thickness = 1f) {
        DrawLine(a, b, radius, Color.Transparent, c, thickness);
    }

    public void DrawHexagon(Vector2 center, float radius, Color c1, Color c2, float thickness = 1f, float rounded = 0, float rotation = 0f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        rounded = MathF.Min(rounded, radius);

        float radius1 = radius + _aaOffset; // Account for AA.
        float width1 = 2f * radius / MathF.Sqrt(3f) + _aaOffset; // Account for AA.

        radius -= rounded;

        Vector2 size = new Vector2(width1, radius1);

        var topLeft = center - size;
        var topRight = center + new Vector2(size.X, -size.Y);
        var bottomRight = center + size;
        var bottomLeft = center + new Vector2(-size.X, size.Y);

        if (rotation != 0f) {
            topLeft = Rotate(topLeft, center, rotation);
            topRight = Rotate(topRight, center, rotation);
            bottomRight = Rotate(bottomRight, center, rotation);
            bottomLeft = Rotate(bottomLeft, center, rotation);
        }

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-size.X, -size.Y), 4f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(size.X, -size.Y), 4f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(size.X, size.Y), 4f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-size.X, size.Y), 4f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: rounded);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillHexagon(Vector2 center, float radius, Color c, float rounded = 0f, float rotation = 0f) {
        DrawHexagon(center, radius, c, c, 0f, rounded, rotation);
    }

    public void BorderHexagon(Vector2 center, float radius, Color c, float thickness = 1f, float rounded = 0f, float rotation = 0f) {
        DrawHexagon(center, radius, Color.Transparent, c, thickness, rounded, rotation);
    }

    public void DrawEquilateralTriangle(Vector2 center, float radius, Color c1, Color c2, float thickness = 1f, float rounded = 0f, float rotation = 0f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        rounded = MathF.Min(rounded, radius);

        float height = radius * 3f;

        float halfWidth = height / MathF.Sqrt(3f);
        float incircle = height / 3f;
        float circumcircle = 2f * height / 3f;

        float halfWidth1 = halfWidth + _aaOffset; // Account for AA.
        float incircle1 = incircle + _aaOffset; // Account for AA.
        float circumcircle1 = circumcircle + _aaOffset; // Account for AA.

        halfWidth -= rounded * MathF.Sqrt(3f);

        var topLeft = center - new Vector2(halfWidth1, incircle1);
        var topRight = center + new Vector2(halfWidth1, -incircle1);
        var bottomRight = center + new Vector2(halfWidth1, circumcircle1);
        var bottomLeft = center + new Vector2(-halfWidth1, circumcircle1);

        if (rotation != 0f) {
            topLeft = Rotate(topLeft, center, rotation);
            topRight = Rotate(topRight, center, rotation);
            bottomRight = Rotate(bottomRight, center, rotation);
            bottomLeft = Rotate(bottomLeft, center, rotation);
        }

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-halfWidth1, -incircle1), 5f, c1, c2, thickness, halfWidth, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(halfWidth1, -incircle1), 5f, c1, c2, thickness, halfWidth, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(halfWidth1, circumcircle1), 5f, c1, c2, thickness, halfWidth, _pixelSize, aaSize: _aaSize, rounded: rounded);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-halfWidth1, circumcircle1), 5f, c1, c2, thickness, halfWidth, _pixelSize, aaSize: _aaSize, rounded: rounded);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillEquilateralTriangle(Vector2 center, float radius, Color c, float rounded = 0f, float rotation = 0f) {
        DrawEquilateralTriangle(center, radius, c, c, 0f, rounded, rotation);
    }

    public void BorderEquilateralTriangle(Vector2 center, float radius, Color c, float thickness = 1f, float rounded = 0f, float rotation = 0f) {
        DrawEquilateralTriangle(center, radius, Color.Transparent, c, thickness, rounded, rotation);
    }

    public void DrawEllipse(Vector2 center, float radius1, float radius2, Color c1, Color c2, float thickness = 1f, float rotation = 0f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        float radius3 = radius1 + _aaOffset; // Account for AA.
        float radius4 = radius2 + _aaOffset; // Account for AA.

        var topLeft = center + new Vector2(-radius3, -radius4);
        var topRight = center + new Vector2(radius3, -radius4);
        var bottomRight = center + new Vector2(radius3, radius4);
        var bottomLeft = center + new Vector2(-radius3, radius4);

        if (rotation != 0f) {
            topLeft = Rotate(topLeft, center, rotation);
            topRight = Rotate(topRight, center, rotation);
            bottomRight = Rotate(bottomRight, center, rotation);
            bottomLeft = Rotate(bottomLeft, center, rotation);
        }

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-radius3, -radius4), 6f, c1, c2, thickness, radius1, _pixelSize, radius2, aaSize: _aaSize);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(radius3, -radius4), 6f, c1, c2, thickness, radius1, _pixelSize, radius2, aaSize: _aaSize);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(radius3, radius4), 6f, c1, c2, thickness, radius1, _pixelSize, radius2, aaSize: _aaSize);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-radius3, radius4), 6f, c1, c2, thickness, radius1, _pixelSize, radius2, aaSize: _aaSize);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillEllipse(Vector2 center, float width, float height, Color c, float rotation = 0f) {
        DrawEllipse(center, width, height, c, c, 0f, rotation);
    }

    public void BorderEllipse(Vector2 center, float width, float height, Color c, float thickness = 1f, float rotation = 0f) {
        DrawEllipse(center, width, height, Color.Transparent, c, thickness, rotation);
    }

    public void DrawRainbowCircle(Vector2 center, float radius, Color borderColor, float rotation = 0f, float thickness = 1f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        float radius1 = radius + _aaOffset; // Account for AA.

        var topLeft = center + new Vector2(-radius1);
        var topRight = center + new Vector2(radius1, -radius1);
        var bottomRight = center + new Vector2(radius1);
        var bottomLeft = center + new Vector2(-radius1, radius1);

        var white = Color.White;

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-radius1, -radius1), 7f, white, borderColor, thickness, radius, _pixelSize, aaSize: _aaSize, rotation: rotation);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(radius1, -radius1), 7f, white, borderColor, thickness, radius, _pixelSize, aaSize: _aaSize, rotation: rotation);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(radius1, radius1), 7f, white, borderColor, thickness, radius, _pixelSize, aaSize: _aaSize, rotation: rotation);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-radius1, radius1), 7f, white, borderColor, thickness, radius, _pixelSize, aaSize: _aaSize, rotation: rotation);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void DrawArc(Vector2 center, float radius, float startAngle, float endAngle, Color c1, Color c2, float thickness = 1f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        float radius1 = radius + _aaOffset; // Account for AA.

        var topLeft = center + new Vector2(-radius1);
        var topRight = center + new Vector2(radius1, -radius1);
        var bottomRight = center + new Vector2(radius1);
        var bottomLeft = center + new Vector2(-radius1, radius1);

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-radius1, -radius1), 8f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: startAngle, rotation: endAngle);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(radius1, -radius1), 8f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: startAngle, rotation: endAngle);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(radius1, radius1), 8f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: startAngle, rotation: endAngle);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-radius1, radius1), 8f, c1, c2, thickness, radius, _pixelSize, aaSize: _aaSize, rounded: startAngle, rotation: endAngle);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void FillArc(Vector2 center, float radius, float startAngle, float endAngle, Color c) {
        DrawArc(center, radius, startAngle, endAngle, c, c, 0f);
    }

    public void BorderArc(Vector2 center, float radius, float startAngle, float endAngle, Color c, float thickness = 1f) {
        DrawArc(center, radius, startAngle, endAngle, Color.Transparent, c, thickness);
    }

    public void DrawTexture(Texture2D texture, Vector2 xy, Color c, Vector2 origin, float scale = 1f, float rotation = 0f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        Vector2 size = texture.Bounds.Size.ToVector2() * scale;

        var topLeft = xy - origin * scale;
        var topRight = xy + new Vector2(size.X, 0) - origin * scale;
        var bottomRight = xy + size - origin * scale;
        var bottomLeft = xy + new Vector2(0, size.Y) - origin * scale;

        if (rotation != 0f) {
            topLeft = Rotate(topLeft, xy, rotation);
            topRight = Rotate(topRight, xy, rotation);
            bottomRight = Rotate(bottomRight, xy, rotation);
            bottomLeft = Rotate(bottomLeft, xy, rotation);
        }

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(0, 0), c);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(1, 0), c);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(1, 1), c);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(0, 1), c);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL, texture);
    }

    public void DrawTexture(Texture2D texture, Vector2 xy, Color c, float scale = 1f, float rotation = 0f) {
        DrawTexture(texture, xy, c, new Vector2(0, 0), scale, rotation);
    }

    public void DrawBlurredRectangle(Vector2 xy, Vector2 size, Color tintColor, Color borderColor, float thickness = 1f, float rounded = 0f, float blurOpacity = 1f) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        rounded = MathF.Min(MathF.Min(rounded, size.X / 2f), size.Y / 2f);

        xy -= new Vector2(_aaOffset);
        Vector2 size1 = size + new Vector2(_aaOffset * 2f);
        Vector2 half = size / 2f;
        Vector2 half1 = half + new Vector2(_aaOffset);
        half -= new Vector2(rounded);

        var topLeft = xy;
        var topRight = xy + new Vector2(size1.X, 0);
        var bottomRight = xy + size1;
        var bottomLeft = xy + new Vector2(0, size1.Y);

        var TL = new VertexShape(new Vector3(topLeft, 0), new Vector2(-half1.X, -half1.Y), 9f, tintColor, borderColor, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded, blurOpacity: blurOpacity);
        var TR = new VertexShape(new Vector3(topRight, 0), new Vector2(half1.X, -half1.Y), 9f, tintColor, borderColor, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded, blurOpacity: blurOpacity);
        var BR = new VertexShape(new Vector3(bottomRight, 0), new Vector2(half1.X, half1.Y), 9f, tintColor, borderColor, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded, blurOpacity: blurOpacity);
        var BL = new VertexShape(new Vector3(bottomLeft, 0), new Vector2(-half1.X, half1.Y), 9f, tintColor, borderColor, thickness, half.X, _pixelSize, half.Y, aaSize: _aaSize, rounded: rounded, blurOpacity: blurOpacity);

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL);
    }

    public void End() {
        if (!_isBatchRunning) {
            throw new InvalidOperationException("Begin must be called before calling End.");
        }

        _isBatchRunning = false;

        EnsureSizeOrResize(ref _vertices, _batchItemCount * 4);
        if (EnsureSizeOrResize(ref _indices, _batchItemCount * 6)) {
            GenerateIndexArray();
        }

        DrawBatch();

        // TODO: Restore old states like rasterizer, depth stencil, blend state?
    }

    private void DrawBatch() {
        if (_batchItemCount == 0) return;

        _effect.Parameters["view_projection"].SetValue(_view * _projection);

        if (_screenSizeParam != null) {
            // Use override if set (for RT rendering), otherwise use viewport size
            if (ScreenSizeOverride.HasValue) {
                _screenSizeParam.SetValue(ScreenSizeOverride.Value);
            } else {
                float w = _graphicsDevice.Viewport.Width;
                float h = _graphicsDevice.Viewport.Height;
                _screenSizeParam.SetValue(new Vector2(w, h));
            }
        }

        if (_blurredTextureParam != null && BlurredBackground != null) {
            _blurredTextureParam.SetValue(BlurredBackground);
        }
        
        // Set blur UV offset for correct sampling when rendering to local RT
        if (_blurUVOffsetParam != null) {
            _blurUVOffsetParam.SetValue(BlurUVOffset);
        }

        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.BlendState = BlendState.AlphaBlend;

        int batchIndex = 0;
        int batchCount = _batchItemCount;

        while (batchCount > 0) {
            var startIndex = 0;
            var index = 0;
            Texture2D tex = null;

            int numBatchesToProcess = batchCount;
            if (numBatchesToProcess > MaxBatchSize) {
                numBatchesToProcess = MaxBatchSize;
            }

            var verticesArrPos = 0;

            for (int i = 0; i < numBatchesToProcess; i++, batchIndex++, index += 4, verticesArrPos += 4) {
                ShapeBatchItem item = _batchItemList[batchIndex];

                var shouldFlush = !ReferenceEquals(item.Texture2D, tex);

                if (shouldFlush) {
                    Flush(startIndex, index, tex);

                    tex = item.Texture2D;
                    startIndex = index = 0;
                    verticesArrPos = 0;
                    _graphicsDevice.Textures[0] = tex;
                }

                _vertices[verticesArrPos + 0] = item.vertexTL;
                _vertices[verticesArrPos + 1] = item.vertexTR;
                _vertices[verticesArrPos + 2] = item.vertexBR;
                _vertices[verticesArrPos + 3] = item.vertexBL;

                item.Texture2D = null;
            }

            Flush(startIndex, index, tex);

            batchCount -= numBatchesToProcess;
        }

        _batchItemCount = 0;
    }

    private void Flush(int start, int end, Texture texture) {
        if (start == end) return;

        var vertexCount = end - start;

        foreach (var pass in _effect.CurrentTechnique.Passes) {
            pass.Apply();

            _graphicsDevice.Textures[0] = texture;

            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, vertexCount, _indices, 0, (vertexCount / 4) * 2, VertexShape.VertexDeclaration);
        }
    }

    private float ScreenToWorldScale() {
        return Vector2.Distance(ScreenToWorld(0f, 0f), ScreenToWorld(1f, 0f));
    }

    private Vector2 ScreenToWorld(float x, float y) {
        return ScreenToWorld(new Vector2(x, y));
    }

    private Vector2 ScreenToWorld(Vector2 xy) {
        return Vector2.Transform(xy, Matrix.Invert(_view));
    }

    private Vector2 Slide(Vector2 a, Vector2 b, float distance) {
        var c = Vector2.Normalize(b - a) * distance;
        return b + c;
    }

    private Vector2 Clockwise(Vector2 a, Vector2 b, float distance) {
        var c = Vector2.Normalize(b - a) * distance;
        return new Vector2(c.Y, -c.X) + a;
    }

    private Vector2 CounterClockwise(Vector2 a, Vector2 b, float distance) {
        var c = Vector2.Normalize(b - a) * distance;
        return new Vector2(-c.Y, c.X) + a;
    }

    private Vector2 Rotate(Vector2 a, Vector2 origin, float rotation) {
        return new Vector2(origin.X + (a.X - origin.X) * MathF.Cos(rotation) - (a.Y - origin.Y) * MathF.Sin(rotation), origin.Y + (a.X - origin.X) * MathF.Sin(rotation) + (a.Y - origin.Y) * MathF.Cos(rotation));
    }

    private bool EnsureBatchItemsSizeOrDouble(ref ShapeBatchItem[] array, int neededCapacity) {
        if (array.Length < neededCapacity) {
            var oldSize = _batchItemList.Length;
            var newSize = oldSize * 2;
            Array.Resize(ref array, newSize);
            for (int i = oldSize; i < newSize; i++) {
                _batchItemList[i] = new ShapeBatchItem();
            }

            return true;
        }

        return false;
    }

    private bool EnsureSizeOrResize<T>(ref T[] array, int neededCapacity) {
        if (array.Length < neededCapacity) {
            int newSize = Math.Max(array.Length * 2, neededCapacity);
            Array.Resize(ref array, newSize);
            return true;
        }

        return false;
    }

    private void GenerateIndexArray() {
        int i = Floor(_fromIndex, 6, 6);
        int j = Floor(_fromIndex, 6, 4);
        for (; i < _indices.Length; i += 6, j += 4) {
            _indices[i + 0] = j + 0;
            _indices[i + 1] = j + 1;
            _indices[i + 2] = j + 3;
            _indices[i + 3] = j + 1;
            _indices[i + 4] = j + 2;
            _indices[i + 5] = j + 3;
        }

        _fromIndex = _indices.Length;
    }

    private int Floor(int value, int div, int mul) {
        return (int)MathF.Floor((float)value / div) * mul;
    }

    public void DrawQuad(Texture2D texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight,
        ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight) {
        EnsureBatchItemsSizeOrDouble(ref _batchItemList, _batchItemCount + 1);

        var TL = topLeft.ToVertexShape();
        var TR = topRight.ToVertexShape();
        var BR = bottomRight.ToVertexShape();
        var BL = bottomLeft.ToVertexShape();

        _batchItemList[_batchItemCount++].Set(TL, TR, BR, BL, texture);
    }

    public GraphicsDevice GraphicsDevice => _graphicsDevice;
}
