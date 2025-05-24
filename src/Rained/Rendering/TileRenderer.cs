using System.Numerics;
using Rained.Assets;
using Raylib_cs;
using Rained.LevelData;
namespace Rained.Rendering;

class TileRenderer
{
    struct TileRender(int x, int y, int layer, Tile init)
    {
        public int X = x;
        public int Y = y;
        public int Layer = layer;
        public Tile TileInit = init;
    }

    private readonly LevelEditRender renderInfo;
    private readonly HashSet<CellPosition> dirtyHeads = [];
    private readonly List<TileRender> tileRenders = [];

    public TileRenderer(LevelEditRender renderInfo)
    {
        this.renderInfo = renderInfo;
        // ReloadLevel();
    }

    public void ReloadLevel()
    {
        dirtyHeads.Clear();
        tileRenders.Clear();

        for (int x = 0; x < RainEd.Instance.Level.Width; x++)
        {
            for (int y = 0; y < RainEd.Instance.Level.Height; y++)
            {
                dirtyHeads.Add(new CellPosition(x, y, 0));
                dirtyHeads.Add(new CellPosition(x, y, 1));
                dirtyHeads.Add(new CellPosition(x, y, 2));
            }
        }
    }

    public void Invalidate(int x, int y, int layer)
    {
        var pos = new CellPosition(x, y, layer);
        dirtyHeads.Add(pos);
    }

    private int GetTileRender(int x, int y, int layer)
    {
        for (int i = 0; i < tileRenders.Count; i++)
        {
            var tile = tileRenders[i];
            if (tile.X == x && tile.Y == y && tile.Layer == layer)
            {
                return i;
            }
        }
        
        return -1;
    }

    private void UpdateRenderList()
    {
        var level = RainEd.Instance.Level;

        foreach (var cellPos in dirtyHeads)
        {
            var i = GetTileRender(cellPos.X, cellPos.Y, cellPos.Layer);

            // if tile render already exists
            if (i >= 0)
            {
                var newHead = level.Layers[cellPos.Layer, cellPos.X, cellPos.Y].TileHead;
                
                // if the tile was removed
                if (newHead is null)
                {
                    tileRenders.RemoveAt(i);
                }

                else
                {
                    tileRenders[i] = new TileRender(cellPos.X, cellPos.Y, cellPos.Layer, newHead);
                }
            }

            // if tile render did not already exist
            else
            {
                var tileInit = level.Layers[cellPos.Layer, cellPos.X, cellPos.Y].TileHead;
                if (tileInit != null)
                {
                    tileRenders.Add(new TileRender(cellPos.X, cellPos.Y, cellPos.Layer, tileInit));
                }
            }
        }

        // sort tile renders by draw index
        if (dirtyHeads.Count > 0)
        {
            tileRenders.Sort(static (TileRender a, TileRender b) => {
                var w = RainEd.Instance.Level.Height;
                return (a.X * w + a.Y) - (b.X * w + b.Y);
            });
        }

        dirtyHeads.Clear();
    }

    private void RenderTilePreviewFromRoot(Tile init, TileRender tileRender, int layer, int alpha, bool useOverlay = false)
    {
        var level = RainEd.Instance.Level;
        
        var gfxProvider = RainEd.Instance.AssetGraphics;
        var previewTextureFound = gfxProvider.GetTilePreviewTexture(init, out var previewTexture, out var previewSrcRect);
        var col = previewTexture is null ? Color.White : init.Category.Color;
        var drawColor = new Color(col.R, col.G, col.B, alpha);

        // could not find the texture for the given tile
        if (previewTexture is null)
        {
            var srcRec = new Rectangle(-0f, -0f, init.Width * 2f, init.Height * 2f);
            var dstRec = new Rectangle(
                (tileRender.X - init.CenterX) * Level.TileSize,
                (tileRender.Y - init.CenterY) * Level.TileSize,
                init.Width * Level.TileSize,
                init.Height * Level.TileSize
            );
            Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
            return;
        }

        // render a part of the texture for each tile within the
        // tile bounds
        for (int x = 0; x < init.Width; x++)
        {
            int gx = tileRender.X - init.CenterX + x;
            for (int y = 0; y < init.Height; y++)
            {
                int gy = tileRender.Y - init.CenterY + y;
                if (!level.IsInBounds(gx, gy)) continue;
                if (renderInfo.OverlayAffectTiles && renderInfo.IsWithinOverlay(gx, gy, layer)) continue;

                for (int l = Math.Min(2, tileRender.Layer + (init.HasSecondLayer?1:0)); l >= tileRender.Layer; l--)
                {
                    if (l != layer) continue;
                    
                    bool isTileRoot = gx == tileRender.X && gy == tileRender.Y && l == tileRender.Layer;
                    var rqArr = l == tileRender.Layer ? init.Requirements : init.Requirements2;
                    if (!isTileRoot && rqArr[x,y] == -1) continue;
                    
                    var cell = useOverlay ? renderInfo.GetCellWithOverlay(gx, gy, l) : level.Layers[l, gx, gy];

                    // handle detached tile bodies.
                    // probably caused from comms move level tool,
                    // which does not correct tile pointers.
                    // draws a red checkerboard
                    if (!isTileRoot && cell.HasTile() && cell.TileHead is null &&
                        (!level.IsInBounds(cell.TileRootX, cell.TileRootY) ||
                        renderInfo.GetCell(cell.TileRootX, cell.TileRootY, cell.TileLayer, useOverlay).TileHead is null))
                    {
                        Raylib.DrawRectangleV(new Vector2(gx, gy) * Level.TileSize, Vector2.One * Level.TileSize, Color.Red);
                        Raylib.DrawRectangleV(new Vector2(gx + 0.5f, gy) * Level.TileSize, Vector2.One * Level.TileSize / 2f, Color.Black);
                        Raylib.DrawRectangleV(new Vector2(gx, gy + 0.5f) * Level.TileSize, Vector2.One * Level.TileSize / 2f, Color.Black);
                        continue;
                    }
                    
                    // render the tile if the tile body belongs to the tile head
                    if (
                        isTileRoot ||
                        (
                            cell.TileHead is null &&
                            cell.TileRootX == tileRender.X &&
                            cell.TileRootY == tileRender.Y &&
                            cell.TileLayer == tileRender.Layer
                        )
                    )
                    {
                        var srcRect = previewSrcRect!.Value;
                        Raylib.DrawTexturePro(
                            previewTexture,
                            new Rectangle(srcRect.X + x * 16f, srcRect.Y + y * 16f, 16f, 16f),
                            new Rectangle(gx * Level.TileSize, gy * Level.TileSize, Level.TileSize, Level.TileSize),
                            Vector2.Zero,
                            0f,
                            drawColor
                        );
                    }
                }
            }
        }
    }

    private void DrawTileHeadMark(int x, int y, Color color, int alpha)
    {
        Raylib.DrawRectangle(
            x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize,
            new Color(color.R, color.G, color.B, (int)(alpha * 0.2f))  
        );

        Raylib.DrawLineV(
            new Vector2(x, y) * Level.TileSize,
            new Vector2(x+1, y+1) * Level.TileSize,
            color
        );

        Raylib.DrawLineV(
            new Vector2(x+1, y) * Level.TileSize,
            new Vector2(x, y+1) * Level.TileSize,
            color
        );
    }

    private void RenderTilePreviewSegment(int x, int y, int layer, int alpha, bool drawTileHeads, bool overlayRender)
    {
        if (overlayRender != renderInfo.IsWithinOverlay(x, y, layer)) return;
        ref var cell = ref renderInfo.GetCell(x, y, layer, overlayRender);
        if (!cell.HasTile()) return;

        Tile? tile;
        int tx;
        int ty;

        if (cell.TileHead is not null)
        {
            tile = cell.TileHead;
            tx = x;
            ty = y;
        }
        else
        {
            if (overlayRender)
            {
                tile = renderInfo.OverlayGeometry![cell.TileLayer, cell.TileRootX, cell.TileRootY].cell.TileHead;
                tx = cell.TileRootX + renderInfo.OverlayX;
                ty = cell.TileRootY + renderInfo.OverlayY;
            }
            else
            {
                tile = RainEd.Instance.Level.Layers[cell.TileLayer, cell.TileRootX, cell.TileRootY].TileHead;
                tx = cell.TileRootX;
                ty = cell.TileRootY;
            }
        }

        // detached tile body
        // probably caused from comms move level tool,
        // which does not correct tile pointers
        if (tile == null)
        {
            Raylib.DrawRectangleV(new Vector2(x, y) * Level.TileSize, Vector2.One * Level.TileSize, Color.Red);
            Raylib.DrawRectangleV(new Vector2(x + 0.5f, y) * Level.TileSize, Vector2.One * Level.TileSize / 2f, Color.Black);
            Raylib.DrawRectangleV(new Vector2(x, y + 0.5f) * Level.TileSize, Vector2.One * Level.TileSize / 2f, Color.Black);
            return;
        }

        var tileLeft = tx - tile.CenterX;
        var tileTop = ty - tile.CenterY;
        RainEd.Instance.AssetGraphics.GetTilePreviewTexture(tile, out var previewTexture, out var previewRect);
        var col = previewTexture is null ? Color.White : tile.Category.Color;

        var srcRect = previewTexture is not null
            ? new Rectangle(previewRect!.Value.X + (x - tileLeft) * 16, previewRect!.Value.Y + (y - tileTop) * 16, 16, 16)
            : new Rectangle((x - tileLeft) * 2, (y - tileTop) * 2, 2, 2); 

        Raylib.DrawTexturePro(
            previewTexture ?? RainEd.Instance.PlaceholderTexture,
            srcRect,
            new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize),
            Vector2.Zero,
            0f,
            new Color(col.R, col.G, col.B, alpha)
        );

        // highlight tile head
        if (cell.TileHead is not null && drawTileHeads)
        {
            DrawTileHeadMark(x, y, col, alpha);
        }
    }

    /// <summary>
    /// Render tiles using their preview graphics
    /// </summary>
    public void PreviewRender(int layer, int alpha)
    {
        var level = RainEd.Instance.Level;
        var drawTileHeads = RainEd.Instance.Preferences.ViewTileHeads;
        int viewL = (int) Math.Floor(renderInfo.ViewTopLeft.X);
        int viewT = (int) Math.Floor(renderInfo.ViewTopLeft.Y);
        int viewR = (int) Math.Ceiling(renderInfo.ViewBottomRight.X);
        int viewB = (int) Math.Ceiling(renderInfo.ViewBottomRight.Y);

        // optimized method - assumes all tile bodies are within the bounds
        // of the tile head. this is the expected condition, but if the
        // user wishes, they can use the slower rendering method, which is
        // covered in the else branch.
        if (RainEd.Instance.Preferences.OptimizedTilePreviews)
        {
            UpdateRenderList();

            // draw the tile renders
            foreach (var tileRender in tileRenders)
            {
                var init = tileRender.TileInit;
                if (tileRender.Layer != layer && init.HasSecondLayer && tileRender.Layer+1 != layer) continue;

                var rectPos = new Vector2(tileRender.X - init.CenterX - init.BfTiles, tileRender.Y - init.CenterY - init.BfTiles);
                var rectSize = new Vector2(init.Width + init.BfTiles * 2, init.Height + init.BfTiles * 2);

                // if levelRec is within screen bounds?
                if (
                    rectPos.X < viewR &&
                    rectPos.Y < viewB &&
                    rectPos.X + rectSize.X > viewL &&
                    rectPos.Y + rectSize.Y > viewT
                )
                {
                    RenderTilePreviewFromRoot(init, tileRender, layer, alpha);
                }
            }

            // highlight tile heads
            if (drawTileHeads)
            {
                foreach (var tileRender in tileRenders)
                {
                    if (tileRender.Layer != layer) continue;
                    var x = tileRender.X;
                    var y = tileRender.Y;
                    var col = tileRender.TileInit.Category.Color;

                    DrawTileHeadMark(x, y, col, alpha);
                    /*Raylib.DrawRectangle(
                        x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize,
                        new Color(col.R, col.G, col.B, (int)(alpha * 0.2f))  
                    );

                    Raylib.DrawLineV(
                        new Vector2(x, y) * Level.TileSize,
                        new Vector2(x+1, y+1) * Level.TileSize,
                        col
                    );

                    Raylib.DrawLineV(
                        new Vector2(x+1, y) * Level.TileSize,
                        new Vector2(x, y+1) * Level.TileSize,
                        col
                    );*/
                }
            }
        }

        // thorough tile preview rendering method
        else
        {
            for (int x = Math.Max(0, viewL); x < Math.Min(level.Width, viewR); x++)
            {
                for (int y = Math.Max(0, viewT); y < Math.Min(level.Height, viewB); y++)
                {
                    RenderTilePreviewSegment(x, y, layer, alpha, drawTileHeads, false);
                }
            }
        }

        // draw tiles in overlay
        if (renderInfo.IsOverlayActive && renderInfo.OverlayAffectTiles)
        {
            var overlayL = Math.Max(0, renderInfo.OverlayX);
            var overlayT = Math.Max(0, renderInfo.OverlayY);
            var overlayR = Math.Min(level.Width, renderInfo.OverlayX + renderInfo.OverlayWidth);
            var overlayB = Math.Min(level.Height, renderInfo.OverlayY + renderInfo.OverlayHeight);

            for (int x = overlayL; x < overlayR; x++)
            {
                for (int y = overlayT; y < overlayB; y++)
                {
                    RenderTilePreviewSegment(x, y, layer, alpha, drawTileHeads, true);
                }
            }
        }
    }

    /// <summary>
    /// Render tiles using their render graphics
    /// </summary>
    public void Render(int layer, int alpha)
    {
        var level = RainEd.Instance.Level;

        UpdateRenderList();

        // draw the tile renders
        RlManaged.Shader shader;
        RlManaged.Shader? curShader = null;

        // palette rendering mode
        bool renderPalette;

        if (renderInfo.UsePalette)
        {
            renderPalette = true;
            shader = Shaders.PaletteShader;
            renderInfo.Palette.UpdateTexture();
        }

        // normal rendering mode
        else
        {
            renderPalette = false;
            shader = Shaders.TileShader;
        }

        var gfxProvider = RainEd.Instance.AssetGraphics;

        int viewL = (int) Math.Floor(renderInfo.ViewTopLeft.X);
        int viewT = (int) Math.Floor(renderInfo.ViewTopLeft.Y);
        int viewR = (int) Math.Ceiling(renderInfo.ViewBottomRight.X);
        int viewB = (int) Math.Ceiling(renderInfo.ViewBottomRight.Y);
        
        
        #region

        // Draw material previews

        Vector2[][] askDirs = [[new Vector2(-1, 0), new Vector2(0, 1)], [new Vector2(0, 1), new Vector2(1, 0)], [new Vector2(-1, 0), new Vector2(0, -1)], [new Vector2(0, -1), new Vector2(1, 0)]];

        ref LevelCell getGeo(int x, int y) {
            return ref level.Layers[layer, Math.Clamp(x, 0, level.Width - 1), Math.Clamp(y, 0, level.Height - 1)];
        }

        bool matchesMat(int x, int y, int matIn) {
            return getGeo(x, y).Material.Equals(matIn) && !getGeo(x, y).Geo.Equals(GeoType.Air) && !getGeo(x, y).Geo.Equals(GeoType.Platform) && !getGeo(x, y).HasTile();
        }

        Rectangle GetGraphicSublayerMat(int y, int x, int sublayer) {
            return new Rectangle(
                20 * x + 5,
                20 * (y + (sublayer * 4)) + 5,
                20, 20
            );
        }

        Rectangle getBlockRect(int x, int y, int corner, int sublayer) {
            int matIn = getGeo(x, y).Material;

            bool matches(int x, int y) {
                return matchesMat(x, y, matIn);
            }

            bool up = matches(x, y - 1);
            bool down = matches(x, y + 1);
            bool left = matches(x - 1, y);
            bool right = matches(x + 1, y);

            bool upleft = matches(x - 1, y - 1);
            bool downleft = matches(x - 1, y + 1);
            bool upright = matches(x + 1, y - 1);
            bool downright = matches(x + 1, y + 1);

            switch (corner) {
                case 0:

                    if (!up && !left) {
                        return GetGraphicSublayerMat(0, 0, sublayer);
                    }

                    if (up && !left) {
                        return GetGraphicSublayerMat(0, 1, sublayer);
                    }

                    if (!up && left) {
                        return GetGraphicSublayerMat(0, 2, sublayer);
                    }

                    if (up && left && !upleft) {
                        return GetGraphicSublayerMat(0, 3, sublayer);
                    }

                    if (up && left && upleft) {
                        return GetGraphicSublayerMat(0, 4, sublayer);
                    }

                    break;
                case 1:

                    if (!up && !right) {
                        return GetGraphicSublayerMat(1, 0, sublayer);
                    }

                    if (up && !right) {
                        return GetGraphicSublayerMat(1, 1, sublayer);
                    }

                    if (!up && right) {
                        return GetGraphicSublayerMat(0, 2, sublayer);
                    }

                    if (up && right && !upright) {
                        return GetGraphicSublayerMat(1, 3, sublayer);
                    }

                    if (up && right && upright) {
                        return GetGraphicSublayerMat(0, 4, sublayer);
                    }

                    break;
                case 2:

                    if (!down && !right) {
                        return GetGraphicSublayerMat(2, 0, sublayer);
                    }

                    if (down && !right) {
                        return GetGraphicSublayerMat(1, 1, sublayer);
                    }

                    if (!down && right) {
                        return GetGraphicSublayerMat(2, 2, sublayer);
                    }

                    if (down && right && !downright) {
                        return GetGraphicSublayerMat(2, 3, sublayer);
                    }

                    if (down && right && downright) {
                        return GetGraphicSublayerMat(0, 4, sublayer);
                    }

                    break;
                case 3:

                    if (!down && !left) {
                        return GetGraphicSublayerMat(3, 0, sublayer);
                    }

                    if (down && !left) {
                        return GetGraphicSublayerMat(0, 1, sublayer);
                    }

                    if (!down && left) {
                        return GetGraphicSublayerMat(2, 2, sublayer);
                    }

                    if (down && left && !downleft) {
                        return GetGraphicSublayerMat(3, 3, sublayer);
                    }

                    if (down && left && downleft) {
                        return GetGraphicSublayerMat(0, 4, sublayer);
                    }

                    break;
            }

            return GetGraphicSublayerMat(0, 0, sublayer);
        }

        for (int x = Math.Max(0, viewL); x < Math.Min(level.Width, viewR); x++) {
            for (int y = Math.Max(0, viewT); y < Math.Min(level.Height, viewB); y++) {
                ref var cell = ref getGeo(x, y);

                if (cell.HasTile()) {
                    var tile = level.GetTile(cell);
                    if (tile is null) {
                        if (curShader != null) {
                            curShader = null;
                            Raylib.EndShaderMode();
                            RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                        }

                        var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                        Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize), Vector2.Zero, 0f, Color.White);
                    }
                }

                if (!cell.HasTile() && cell.Material != 0 && cell.Geo != GeoType.Air) {
                    MaterialInfo mat = RainEd.Instance.MaterialDatabase.GetMaterial(cell.Material);
                    if (mat.Init != null) {

                        if (cell.Geo.Equals(GeoType.Solid)) {
                            if (mat.Init.ContainsKey("block")) {

                                Lingo.PropertyList block = (Lingo.PropertyList)mat.Init["block"];

                                var rectPos = new Vector2(x, y);
                                var rectSize = new Vector2(1, 1);

                                var matName = mat.Name;

                                if (block.ContainsKey("isDefaultMat")) {
                                    matName = "tileSet" + block["fakeInternalName"].ToString();
                                }

                                var tex = gfxProvider.GetMaterialTexture(matName);
                                var dstRec = new Rectangle(rectPos * Level.TileSize, rectSize * Level.TileSize);

                                if (tex is null) {
                                    if (curShader != null) {
                                        curShader = null;
                                        Raylib.EndShaderMode();
                                        RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                                    }

                                    Raylib.EndShaderMode();
                                    var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                                    Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
                                }
                                else {
                                    if (curShader != shader) {
                                        curShader = shader;
                                        RainEd.RenderContext.Flags = Glib.RenderFlags.DepthTest;

                                        if (shader == Shaders.PaletteShader)
                                            renderInfo.Palette.BeginPaletteShaderMode();
                                        else
                                            Raylib.BeginShaderMode(shader);
                                    }

                                    Lingo.LinearList repeatL = (Lingo.LinearList)block["repeatL"];

                                    for (int l = repeatL.Count - 1; l >= 0; l--) {
                                        Glib.Color col = Glib.Color.FromRGBA(255, 255, 255, alpha);

                                        if (renderPalette) {
                                            var paletteIndex = mat.blockLayerDepths[l] / 30f;
                                            col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                                        }
                                        else {
                                            // fade to white as the layer is further away
                                            // from the front
                                            float a = (float)mat.blockLayerDepths[l] / 10.0f;
                                            col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                            col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                            col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                                        }

                                        int realL = l;
                                        var addRect = new Vector2(0, 0);

                                        if (block.ContainsKey("isDefaultMat")) {
                                            realL = 0;
                                            addRect = new Vector2(l == 0 ? 0 : 120, 0);
                                        }

                                        var srcRec = getBlockRect(x, y, 0, realL) + addRect;
                                        var cDstRec = new Rectangle(rectPos * Level.TileSize + new Vector2(-5f, -5f), rectSize * Level.TileSize);
                                        LevelEditRender.DrawTextureSublayer(tex, srcRec, cDstRec, layer * 10 + mat.blockLayerDepths[l], col);

                                        srcRec = getBlockRect(x, y, 1, realL) + addRect;
                                        cDstRec = new Rectangle(rectPos * Level.TileSize + new Vector2(5f, -5f), rectSize * Level.TileSize);
                                        LevelEditRender.DrawTextureSublayer(tex, srcRec, cDstRec, layer * 10 + mat.blockLayerDepths[l], col);

                                        srcRec = getBlockRect(x, y, 2, realL) + addRect;
                                        cDstRec = new Rectangle(rectPos * Level.TileSize + new Vector2(5f, 5f), rectSize * Level.TileSize);
                                        LevelEditRender.DrawTextureSublayer(tex, srcRec, cDstRec, layer * 10 + mat.blockLayerDepths[l], col);

                                        srcRec = getBlockRect(x, y, 3, realL) + addRect;
                                        cDstRec = new Rectangle(rectPos * Level.TileSize + new Vector2(-5f, 5f), rectSize * Level.TileSize);
                                        LevelEditRender.DrawTextureSublayer(tex, srcRec, cDstRec, layer * 10 + mat.blockLayerDepths[l], col);

                                    }
                                }
                            }
                        }
                        else if (cell.Geo.Equals(GeoType.SlopeLeftDown) || cell.Geo.Equals(GeoType.SlopeLeftUp) || cell.Geo.Equals(GeoType.SlopeRightUp) || cell.Geo.Equals(GeoType.SlopeRightDown)) {
                            if (mat.Init.ContainsKey("slope")) {

                                Lingo.PropertyList slope = (Lingo.PropertyList)mat.Init["slope"];

                                var rectPos = new Vector2(x, y);
                                var rectSize = new Vector2(1, 1);

                                var matName = mat.Name + "Slopes";

                                if (slope.ContainsKey("isDefaultMat")) {
                                    matName = "tileSet" + slope["fakeInternalName"].ToString();
                                }

                                var tex = gfxProvider.GetMaterialTexture(matName);
                                var dstRec = new Rectangle(rectPos * Level.TileSize - new Vector2(5f, 5f), rectSize * Level.TileSize + new Vector2(10f, 10f));

                                if (tex is null) {
                                    if (curShader != null) {
                                        curShader = null;
                                        Raylib.EndShaderMode();
                                        RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                                    }

                                    Raylib.EndShaderMode();
                                    var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                                    Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
                                }
                                else {
                                    if (curShader != shader) {
                                        curShader = shader;
                                        RainEd.RenderContext.Flags = Glib.RenderFlags.DepthTest;

                                        if (shader == Shaders.PaletteShader)
                                            renderInfo.Palette.BeginPaletteShaderMode();
                                        else
                                            Raylib.BeginShaderMode(shader);
                                    }

                                    Lingo.LinearList repeatL = (Lingo.LinearList)slope["repeatL"];

                                    Vector2[] dir = askDirs[((int)cell.Geo) - 2];

                                    for (int l = repeatL.Count - 1; l >= 0; l--) {
                                        Glib.Color col = Glib.Color.FromRGBA(255, 255, 255, alpha);

                                        if (renderPalette) {
                                            var paletteIndex = mat.slopeLayerDepths[l] / 30f;
                                            col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                                        }
                                        else {
                                            // fade to white as the layer is further away
                                            // from the front
                                            float a = (float)mat.slopeLayerDepths[l] / 10.0f;
                                            col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                            col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                            col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                                        }

                                        for (int ad = 0; ad < 2; ad++) {

                                            Vector2 adir = new Vector2(x, y) + dir[ad];
                                            Vector2 inPos = new Vector2(5, 5) + new Vector2(60 * ad, 30 * (((int)cell.Geo) - 2));

                                            if (matchesMat(((int)adir.X), ((int)adir.Y), cell.Material)) {
                                                inPos += new Vector2(30, 0);
                                            }

                                            int realL = l;
                                            var addRect = new Vector2(0, 0);

                                            if (slope.ContainsKey("isDefaultMat")) {
                                                realL = 0;
                                                addRect = new Vector2(l == 0 ? 0 : 120, 80);
                                            }

                                            Rectangle srcRect = new Rectangle(inPos + new Vector2(0, 130 * realL) + addRect, new Vector2(30, 30));

                                            LevelEditRender.DrawTextureSublayer(tex, srcRect, dstRec, layer * 10 + mat.slopeLayerDepths[l], col);
                                        }
                                    }
                                }
                            }
                        }

                        if (mat.Init.ContainsKey("texture")) {
                            Lingo.PropertyList texture = (Lingo.PropertyList)mat.Init["texture"];

                            var rectPos = new Vector2(x, y);
                            var rectSize = new Vector2(1, 1);

                            var matName = mat.Name;
                            var offset = new Vector2(0, 1);

                            if (texture.ContainsKey("isDefaultMat")) {
                                matName = texture["fakeInternalName"].ToString();
                                offset = new Vector2(0, 0);
                            }

                            var tex = gfxProvider.GetMaterialTexture(matName + "Texture");
                            var dstRec = new Rectangle(rectPos * Level.TileSize, rectSize * Level.TileSize);

                            if (tex is null) {
                                if (curShader != null) {
                                    curShader = null;
                                    Raylib.EndShaderMode();
                                    RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                                }

                                Raylib.EndShaderMode();
                                var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                                Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
                            }
                            else {
                                if (curShader != shader) {
                                    curShader = shader;
                                    RainEd.RenderContext.Flags = Glib.RenderFlags.DepthTest;

                                    if (shader == Shaders.PaletteShader)
                                        renderInfo.Palette.BeginPaletteShaderMode();
                                    else
                                        Raylib.BeginShaderMode(shader);
                                }

                                Lingo.LinearList repeatL = (Lingo.LinearList)texture["repeatL"];
                                Vector2 size = (Vector2)texture["sz"];

                                for (int l = repeatL.Count - 1; l >= 0; l--) {
                                    Glib.Color col = Glib.Color.FromRGBA(255, 255, 255, alpha);

                                    if (renderPalette) {
                                        var paletteIndex = mat.textureLayerDepths[l] / 30f;
                                        col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                                    }
                                    else {
                                        // fade to white as the layer is further away
                                        // from the front
                                        float a = (float)mat.textureLayerDepths[l] / 10.0f;
                                        col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                        col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                        col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                                    }

                                    var srcRect = new Rectangle(new Vector2(x % size.X, (y % size.Y) + l * size.Y) * 20 + offset, rectSize * Level.TileSize);

                                    if (cell.Geo.Equals(GeoType.SlopeLeftDown) || cell.Geo.Equals(GeoType.SlopeLeftUp) || cell.Geo.Equals(GeoType.SlopeRightUp) || cell.Geo.Equals(GeoType.SlopeRightDown)) {
                                        LevelEditRender.DrawTextureTriangleSublayer(tex, srcRect, dstRec, layer * 10 + mat.textureLayerDepths[l], col, ((int)cell.Geo) - 2);
                                    }
                                    else if (cell.Geo.Equals(GeoType.Solid)) {
                                        LevelEditRender.DrawTextureSublayer(tex, srcRect, dstRec, layer * 10 + mat.textureLayerDepths[l], col);
                                    }

                                }
                            }
                        }

                        if (mat.Init.ContainsKey("pipelike")) {

                            Lingo.PropertyList pipelike = (Lingo.PropertyList)mat.Init["pipelike"];

                            var rectPos = new Vector2(x, y);

                            var matName = mat.Name + "Pipes";

                            var bfOffset = new Vector2(10, 10);

                            if (pipelike.ContainsKey("isDefaultMat")) {
                                matName = pipelike["fakeInternalName"].ToString();
                                bfOffset = (Vector2)pipelike["bfOffset"];
                            }

                            var isDense = false;

                            if (pipelike.ContainsKey("denseMat")) {
                                isDense = Lingo.LingoNumber.AsInt(pipelike["denseMat"]) == 1;
                            }

                            var tex = gfxProvider.GetMaterialTexture(matName);
                            var dstRec = new Rectangle(rectPos * Level.TileSize - bfOffset, new Vector2(40, 40));

                            if (tex is null) {
                                if (curShader != null) {
                                    curShader = null;
                                    Raylib.EndShaderMode();
                                    RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                                }

                                Raylib.EndShaderMode();
                                var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                                Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
                            }
                            else {
                                if (curShader != shader) {
                                    curShader = shader;
                                    RainEd.RenderContext.Flags = Glib.RenderFlags.DepthTest;

                                    if (shader == Shaders.PaletteShader)
                                        renderInfo.Palette.BeginPaletteShaderMode();
                                    else
                                        Raylib.BeginShaderMode(shader);
                                }

                                int[] depths = [2, 3, 6, 7];
                                if (pipelike.ContainsKey("depths")) {
                                    depths = ((Lingo.LinearList)pipelike["depths"]).Select(ln => (int)ln).ToArray();
                                }


                                Vector2 gtPos = new(0, 0);

                                switch (((int)cell.Geo)) {
                                    case 1:
                                        string nbrs = "";
                                        foreach (var dir in new[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) }) {
                                            nbrs += matchesMat(x + (int)dir.X, y + (int)dir.Y, cell.Material) ? "1" : "0";
                                        }

                                        switch (nbrs) {
                                            case "0101":
                                                gtPos = new Vector2(2, 2);
                                                break;
                                            case "1010":
                                                gtPos = new Vector2(4, 2);
                                                break;
                                            case "1111":
                                                gtPos = new Vector2(6, 2);
                                                break;
                                            case "0111":
                                                gtPos = new Vector2(8, 2);
                                                break;
                                            case "1101":
                                                gtPos = new Vector2(10, 2);
                                                break;
                                            case "1110":
                                                gtPos = new Vector2(12, 2);
                                                break;
                                            case "1011":
                                                gtPos = new Vector2(14, 2);
                                                break;
                                            case "0011":
                                                gtPos = new Vector2(16, 2);
                                                break;
                                            case "1001":
                                                gtPos = new Vector2(18, 2);
                                                break;
                                            case "1100":
                                                gtPos = new Vector2(20, 2);
                                                break;
                                            case "0110":
                                                gtPos = new Vector2(22, 2);
                                                break;
                                            case "1000":
                                                gtPos = new Vector2(24, 2);
                                                break;
                                            case "0010":
                                                gtPos = new Vector2(26, 2);
                                                break;
                                            case "0100":
                                                gtPos = new Vector2(28, 2);
                                                break;
                                            case "0001":
                                                gtPos = new Vector2(30, 2);
                                                break;
                                            case "0000":
                                                gtPos = new Vector2(40, 2);
                                                break;
                                        }
                                        break;

                                    case 3:
                                        gtPos = new Vector2(32, 2);
                                        break;
                                    case 2:
                                        gtPos = new Vector2(34, 2);
                                        break;
                                    case 4:
                                        gtPos = new Vector2(36, 2);
                                        break;
                                    case 5:
                                        gtPos = new Vector2(38, 2);
                                        break;
                                    case 6:
                                        gtPos = new Vector2(42, 2);
                                        break;
                                }

                                if (isDense) {
                                    switch (((int)cell.Geo)) {
                                        case 1:
                                            string nbrs = "";
                                            foreach (var dir in new[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) }) {
                                                nbrs += matchesMat(x + (int)dir.X, y + (int)dir.Y, cell.Material) ? "1" : "0";
                                            }

                                            switch (nbrs) {
                                                case "0000":
                                                    gtPos = new Vector2(2, 2);
                                                    break;
                                                case "1111":
                                                    gtPos = new Vector2(4, 2);
                                                    break;
                                                case "0101":
                                                    gtPos = new Vector2(6, 2);
                                                    break;
                                                case "1010":
                                                    gtPos = new Vector2(8, 2);
                                                    break;
                                                case "0001":
                                                    gtPos = new Vector2(10, 2);
                                                    break;
                                                case "1000":
                                                    gtPos = new Vector2(12, 2);
                                                    break;
                                                case "0100":
                                                    gtPos = new Vector2(14, 2);
                                                    break;
                                                case "0010":
                                                    gtPos = new Vector2(16, 2);
                                                    break;
                                                case "1001":
                                                    gtPos = new Vector2(18, 2);
                                                    break;
                                                case "1100":
                                                    gtPos = new Vector2(20, 2);
                                                    break;
                                                case "0110":
                                                    gtPos = new Vector2(22, 2);
                                                    break;
                                                case "0011":
                                                    gtPos = new Vector2(24, 2);
                                                    break;
                                                case "1011":
                                                    gtPos = new Vector2(26, 2);
                                                    break;
                                                case "1101":
                                                    gtPos = new Vector2(28, 2);
                                                    break;
                                                case "1110":
                                                    gtPos = new Vector2(30, 2);
                                                    break;
                                                case "0111":
                                                    gtPos = new Vector2(32, 2);
                                                    break;
                                            }
                                            break;

                                        case 3:
                                            gtPos = new Vector2(38, 2);
                                            break;
                                        case 2:
                                            gtPos = new Vector2(40, 2);
                                            break;
                                        case 4:
                                            gtPos = new Vector2(34, 2);
                                            break;
                                        case 5:
                                            gtPos = new Vector2(36, 2);
                                            break;
                                        case 6:
                                            gtPos = new Vector2(42, 2);
                                            break;
                                    }
                                }


                                Vector2 va = new((gtPos.X - 1) * 20 - 10, (gtPos.Y - 1) * 20 - 9);

                                Rectangle r = new(va, dstRec.Size);

                                for (int i = 0; i < depths.Length; i++) {
                                    Glib.Color col = Glib.Color.FromRGBA(255, 255, 255, alpha);

                                    if (renderPalette) {
                                        var paletteIndex = depths[i] / 30f;
                                        col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                                    }
                                    else {
                                        // fade to white as the layer is further away
                                        // from the front
                                        float a = (float)depths[i] / 10.0f;
                                        col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                        col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                        col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                                    }

                                    LevelEditRender.DrawTextureSublayer(tex, r, dstRec, layer * 10 + depths[i], col);
                                }

                            }
                        }
                    }
                }
            }
        }

        #endregion

        foreach (var tileRender in tileRenders)
        {
            if (tileRender.Layer != layer) continue;

            var init = tileRender.TileInit;

            var rectPos = new Vector2(tileRender.X - init.CenterX - init.BfTiles, tileRender.Y - init.CenterY - init.BfTiles);
            var rectSize = new Vector2(init.Width + init.BfTiles * 2, init.Height + init.BfTiles * 2);

            // if levelRec is within screen bounds?
            if (
                rectPos.X < viewR &&
                rectPos.Y < viewB &&
                rectPos.X + rectSize.X > viewL &&
                rectPos.Y + rectSize.Y > viewT
            )
            {
                var tex = gfxProvider.GetTileTexture(init.Name);
                var dstRec = new Rectangle(rectPos * Level.TileSize, rectSize * Level.TileSize);

                var catCol = init.Category.Color;
                var drawColor = new Color(catCol.R, catCol.G, catCol.B, alpha);

                // if the tile texture was not found, draw a
                // placeholder graphic
                if (tex is null)
                {
                    if (curShader != null)
                    {
                        curShader = null;
                        Raylib.EndShaderMode();
                        RainEd.RenderContext.Flags = Glib.RenderFlags.None;
                    }

                    var srcRec = new Rectangle(0f, 0f, 2f, 2f);
                    Raylib.DrawTexturePro(RainEd.Instance.PlaceholderTexture, srcRec, dstRec, Vector2.Zero, 0f, Color.White);
                }
                else
                {
                    if (curShader != shader)
                    {
                        curShader = shader;
                        RainEd.RenderContext.Flags = Glib.RenderFlags.DepthTest;

                        if (shader == Shaders.PaletteShader)
                            renderInfo.Palette.BeginPaletteShaderMode();
                        else
                            Raylib.BeginShaderMode(shader);
                    }

                    // draw front face of box tile
                    if (init.Type == TileType.Box)
                    {
                        var height = init.Height * 20;
                        var srcRec = new Rectangle(
                            0f,
                            init.ImageYOffset + height * init.Width,
                            (init.Width + init.BfTiles * 2) * 20, (init.Height + init.BfTiles * 2) * 20
                        );

                        // if rendering palette, R channel represents sublayer
                        // A channel is alpha, as usual
                        var col = renderPalette ? new Color(0, 0, 0, (int)drawColor.A) : drawColor;
                        LevelEditRender.DrawTextureSublayer(
                            tex, srcRec, dstRec, layer*10 + init.LayerDepths[0],
                            Glib.Color.FromRGBA(col.R, col.G, col.B, col.A)
                        );
                    }

                    // draw the tile sublayers from back to front
                    else
                    {
                        for (int l = init.LayerCount-1; l >= 0; l--)
                        {
                            var srcRec = GetGraphicSublayer(init, l, 0);

                            // if rendering palette, R channel represents sublayer
                            // A channel is alpha, as usual
                            Glib.Color col = Glib.Color.FromRGBA(drawColor.R, drawColor.G, drawColor.B, drawColor.A);

                            if (renderPalette)
                            {
                                var paletteIndex = init.LayerDepths[l] / 30f;
                                col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                            }
                            else
                            {
                                // fade to white as the layer is further away
                                // from the front
                                float a = (float)init.LayerDepths[l] / init.LayerDepth;
                                col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                            }

                            LevelEditRender.DrawTextureSublayer(tex, srcRec, dstRec, layer*10 + init.LayerDepths[l], col);
                        }

                    }
                    
                    
                    #region
                    if (init.Tags.Contains("Big Wheel")) {
                        var tetex = gfxProvider.GetTileTexture("Big Wheel Graf");
                        for (int l = 0; l < 10; l++) {
                            if (l == 0 || l == 1 || l == 2 || l == 7 || l == 8 || l == 9) {

                                // if rendering palette, R channel represents sublayer
                                // A channel is alpha, as usual
                                Glib.Color col = Glib.Color.FromRGBA(drawColor.R, drawColor.G, drawColor.B, drawColor.A);

                                if (renderPalette) {
                                    var paletteIndex = l / 30f;
                                    col = new Glib.Color(Math.Clamp(paletteIndex, 0f, 1f), 0f, 0f, col.A);
                                }
                                else {
                                    // fade to white as the layer is further away
                                    // from the front
                                    float a = (float)l / init.LayerDepth;
                                    col.R = col.R * (1f - a) + (col.R * 0.5f) * a;
                                    col.G = col.G * (1f - a) + (col.G * 0.5f) * a;
                                    col.B = col.B * (1f - a) + (col.B * 0.5f) * a;
                                }

                                LevelEditRender.DrawTextureSublayer(tetex, new Rectangle(0, 0, 180, 180), new Rectangle(rectPos * Level.TileSize - new Vector2(52, 50), new Vector2(181, 180)), layer * 10 + l, col);
                            }
                        }
                    }
                    #endregion
                    
                }
            }
        }

        if (curShader != null)
        {
            Raylib.EndShaderMode();
            RainEd.RenderContext.Flags = Glib.RenderFlags.None;
        }

        // highlight tile heads
        if (RainEd.Instance.Preferences.ViewTileHeads)
        {
            foreach (var tileRender in tileRenders)
            {
                if (tileRender.Layer != layer) continue;
                var x = tileRender.X;
                var y = tileRender.Y;
                var col = tileRender.TileInit.Category.Color;

                Raylib.DrawRectangle(
                    x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize,
                    new Color(col.R, col.G, col.B, (int)(alpha * 0.2f))  
                );

                Raylib.DrawLineV(
                    new Vector2(x, y) * Level.TileSize,
                    new Vector2(x+1, y+1) * Level.TileSize,
                    col
                );

                Raylib.DrawLineV(
                    new Vector2(x+1, y) * Level.TileSize,
                    new Vector2(x, y+1) * Level.TileSize,
                    col
                );
            }

        }
    }

    public static Rectangle GetGraphicSublayer(Tile tile, int sublayer, int variation)
    {
        var width = (tile.Width + tile.BfTiles * 2) * 20;
        var height = (tile.Height + tile.BfTiles * 2) * 20;

        return new Rectangle(
            width * variation,
            height * sublayer + tile.ImageYOffset,
            width, height
        );
    }

    public static void DrawGeometryOutline(int tileInt, int x, int y, float lineWidth, float tileSize, Color color)
    {
        if (tileInt == 0)
        {
            // air is represented by a cross (OMG ASCEND WITH GORB???)
            // an empty cell (-1) would mean any tile is accepted
            Raylib.DrawLineV(
                startPos: new Vector2(x * tileSize + 5, y * tileSize + 5),
                endPos: new Vector2((x+1) * tileSize - 5, (y+1) * tileSize - 5),
                color
            );

            Raylib.DrawLineV(
                startPos: new Vector2((x+1) * tileSize - 5, y * tileSize + 5),
                endPos: new Vector2(x * tileSize + 5, (y+1) * tileSize - 5),
                color
            );
        }
        else if (tileInt > 0)
        {
            var cellType = (GeoType) tileInt;
            switch (cellType)
            {
                case GeoType.Solid:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        color
                    );
                    break;
                
                case GeoType.Platform:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, 10),
                        color
                    );
                    break;
                
                case GeoType.Glass:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        color
                    );
                    break;

                case GeoType.ShortcutEntrance:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        Color.Red
                    );
                    break;

                case GeoType.SlopeLeftDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x+1, y) * tileSize,
                        new Vector2(x, y) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeLeftUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x, y+1) * tileSize,
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x+1, y) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y) * tileSize,
                        new Vector2(x, y) * tileSize,
                        new Vector2(x, y+1) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x, y) * tileSize,
                        new Vector2(x, y+1) * tileSize,
                        color
                    );
                    break;
            }
        }
    }

    public static void DrawTileSpecs(Tile selectedTile, int tileOriginX, int tileOriginY,
        float tileSize = Level.TileSize,
        byte alpha = 255
    )
    {
        var lineWidth = 1f / RainEd.Instance.LevelView.ViewZoom;
        var prefs = RainEd.Instance.Preferences;

        if (selectedTile.HasSecondLayer)
        {
            var col = prefs.TileSpec2.ToRaylibColor();
            col.A = (byte)(col.A * (alpha / 255f));

            for (int x = 0; x < selectedTile.Width; x++)
            {
                for (int y = 0; y < selectedTile.Height; y++)
                {
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(tileOriginX * tileSize + 2, tileOriginY * tileSize + 2, 0);

                    sbyte tileInt = selectedTile.Requirements2[x,y];
                    DrawGeometryOutline(tileInt, x, y, lineWidth, tileSize, col);
                    Rlgl.PopMatrix();
                }
            }
        }

        // first layer
        {
            var col = prefs.TileSpec1.ToRaylibColor();
            col.A = (byte)(col.A * (alpha / 255f));

            for (int x = 0; x < selectedTile.Width; x++)
            {
                for (int y = 0; y < selectedTile.Height; y++)
                {
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(tileOriginX * tileSize, tileOriginY * tileSize, 0);

                    sbyte tileInt = selectedTile.Requirements[x,y];
                    DrawGeometryOutline(tileInt, x, y, lineWidth, tileSize, col);
                    Rlgl.PopMatrix();
                }
            }
        }
    }
}