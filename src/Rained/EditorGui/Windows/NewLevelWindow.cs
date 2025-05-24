namespace Rained.EditorGui;

using System.Numerics;
using ImGuiNET;
using Rained.LevelData;
using Raylib_cs;

static class NewLevelWindow
{
    public const string WindowName = "New Level";
    public static bool IsWindowOpen = false;

    private static int levelWidth;
    private static int levelHeight;
    private static float levelScreenW;
    private static float levelScreenH;
    private static int levelBufL, levelBufR, levelBufT, levelBufB;
    private static int camBuffX, camBuffY;
    private static bool[] fillLayer = new bool[3];
    private static bool autoCameras;

    private static RlManaged.RenderTexture2D? previewFramebuffer = null;
    private static List<Vector2> cameraPositions = [];

    // window size isn't correctly centered on first appearance
    // cus it doesn't know the correct window size on first frame
    // so this is a hack to fix that.
    private static int _centerTick = 0;

    public static void OpenWindow()
    {
        levelWidth = 85;
        levelHeight = 58;
        levelBufL = 17;
        levelBufR = 17;
        levelBufT = 10;
        levelBufB = 10;
        fillLayer[0] = false;
        fillLayer[1] = false;
        fillLayer[2] = false;
        autoCameras = true;
        camBuffX = 6;
        camBuffY = 5;

        IsWindowOpen = true;

        // using the formula from the modding wiki
        levelScreenW = CalcScreenWidth();
        levelScreenH = CalcScreenHeight();
    }

    public static void ShowWindow()
    {
        if (!ImGui.IsPopupOpen(WindowName) && IsWindowOpen)
        {
            ImGui.OpenPopup(WindowName);
            _centerTick = 4;
        }

        // center popup modal
        // window size isn't correctly centered on first appearance
        // cus it doesn't know the correct window size on first frame
        // so this is a hack to fix that.
        ImGuiExt.CenterNextWindow(_centerTick == 0 ? ImGuiCond.Appearing : ImGuiCond.Always);
        if (_centerTick > 0) _centerTick--;
        
        if (ImGui.BeginPopupModal(WindowName, ref IsWindowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            // options column
            var childFlags = ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY;
            int previewHeight;

            var cursorStart = ImGui.GetCursorPos();
            ImGui.BeginChild("Options", new Vector2(0f, 0f), childFlags);
            {
                ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 8.0f);

                ImGui.SeparatorText("Level Size");
                {
                    // tile size
                    if (ImGui.InputInt("Width", ref levelWidth))
                        levelScreenW = CalcScreenWidth();
                    
                    levelWidth = Math.Max(levelWidth, 1); // minimum value is 1

                    if (ImGui.InputInt("Height", ref levelHeight))
                        levelScreenH = CalcScreenHeight();
                    
                    levelHeight = Math.Max(levelHeight, 1); // minimum value is 1

                    // screen size, using the formula from the modding wiki
                    if (!RainEd.Instance.Preferences.HideScreenSize)
                    {
                        if (ImGui.InputFloat("Screen Width", ref levelScreenW, 0.5f, 0.125f))
                        {
                            levelWidth = CalcLevelWidth();
                        }
                        levelScreenW = Math.Max(levelScreenW, 0);

                        if (ImGui.InputFloat("Screen Height", ref levelScreenH, 0.5f, 0.125f))
                        {
                            levelHeight = CalcLevelHeight();
                        }
                        levelScreenH = Math.Max(levelScreenH, 0); // minimum value is 1
                    }
                }

                ImGui.SeparatorText("Border Tiles");
                {
                    ImGui.InputInt("Border Tiles Left", ref levelBufL);
                    ImGui.InputInt("Border Tiles Top", ref levelBufT);
                    ImGui.InputInt("Border Tiles Right", ref levelBufR);
                    ImGui.InputInt("Border Tiles Bottom", ref levelBufB);

                    levelBufL = Math.Max(levelBufL, 0);
                    levelBufR = Math.Max(levelBufR, 0);
                    levelBufT = Math.Max(levelBufT, 0);
                    levelBufB = Math.Max(levelBufB, 0);
                }

                ImGui.SeparatorText("Fill Layers");
                {
                    ImGuiExt.ButtonFlags("Layers", ["1", "2", "3"], fillLayer);
                    //ImGui.Checkbox("Fill Layer 1", ref fillLayer[0]);
                    //ImGui.Checkbox("Fill Layer 2", ref fillLayer[1]);
                    //ImGui.Checkbox("Fill Layer 3", ref fillLayer[2]);
                }

                ImGui.SeparatorText("Options");
                {
                    ImGui.Checkbox("Auto-place Cameras", ref autoCameras);
                    
                    if (ImGui.InputInt("Camera Overlap W", ref camBuffX)) {
                        levelScreenW = CalcScreenWidth();
                        camBuffX = Math.Max(camBuffX, 0);
                    }

                    if (ImGui.InputInt("Camera Overlap H", ref camBuffY)) {
                        levelScreenH = CalcScreenHeight();
                        camBuffY = Math.Max(camBuffY, 0);
                    }
                }

                ImGui.PopItemWidth();
            }
            ImGui.EndChild();
            var cursorEnd = ImGui.GetCursorPos();
            previewHeight = (int)(cursorEnd.Y - cursorStart.Y);

            // preview column
            ImGui.SameLine();
            ImGui.BeginGroup();

            if (previewFramebuffer is null ||
                previewFramebuffer.Texture.Height != previewHeight
            )
            {
                previewFramebuffer?.Dispose();
                previewFramebuffer = RlManaged.RenderTexture2D.Load((int)(ImGui.GetFontSize() * 40f), previewHeight);
            }

            UpdatePreview();
            ImGuiExt.ImageRenderTexture(previewFramebuffer);

            ImGui.EndGroup();

            ImGui.Separator();

            if (StandardPopupButtons.Show(PopupButtonList.OKCancel, out int btnPressed))
            {
                if (btnPressed == 0)
                {
                    RainEd.Instance.OpenLevel(CreateLevel());
                }

                IsWindowOpen = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private static float CalcScreenWidth() {
        return (levelWidth - camBuffX - 34f) / (51f - camBuffX);
    }

    private static float CalcScreenHeight() {
        return (levelHeight - camBuffY - 20f) / (38f - camBuffY);
    }


    private static int CalcLevelWidth() {
        return (int)Math.Max(Math.Floor(51f * levelScreenW - levelScreenW * camBuffX + camBuffX + 34f), 0);
    }

    private static int CalcLevelHeight() {
        return (int)Math.Max(Math.Floor(38f * levelScreenH - levelScreenH * camBuffY + camBuffY + 20f), 0);
    }

    private static List<Vector2> CalcCameraPositions()
        => CalcCameraPositions([]);

    private static List<Vector2> CalcCameraPositions(List<Vector2> cameraPositions)
    {
        if (!autoCameras)
        {
            cameraPositions.Clear();
            cameraPositions.Add(new Vector2(1f, 1f));
            return cameraPositions;
        }

        var screenW = (int)MathF.Round(Math.Max(1f, CalcScreenWidth()));
        var screenH = (int)MathF.Round(Math.Max(1f, CalcScreenHeight()));

        cameraPositions.Clear();

        for (int row = 0; row < screenH; row++)
        {
            for (int col = 0; col < screenW; col++)
            {
                var camPos = (Camera.StandardSize - new Vector2(camBuffX, camBuffY)) * new Vector2(col, row) + new Vector2(levelBufL - 10, levelBufT - 1);
                cameraPositions.Add(camPos);
            }
        }

        return cameraPositions;
    }

    private static void UpdatePreview()
    {
        if (previewFramebuffer is null) return;

        const int TileSize = 2;
        
        Raylib.BeginTextureMode(previewFramebuffer);
        Raylib.ClearBackground(Color.Blank);

        Span<Color> layerColors = stackalloc Color[3];
        {
            var layerCol1 = RainEd.Instance.Preferences.LayerColor1;
            var layerCol2 = RainEd.Instance.Preferences.LayerColor2;
            var layerCol3 = RainEd.Instance.Preferences.LayerColor3;
            layerColors =
            [
                new Color(layerCol1.R, layerCol1.G, layerCol1.B, (byte)255),
                new Color(layerCol2.R, layerCol2.G, layerCol2.B, (byte)100),
                new Color(layerCol3.R, layerCol3.G, layerCol3.B, (byte)70),
            ];
        }

        Rlgl.PushMatrix();
        Rlgl.Translatef(
            (previewFramebuffer.Texture.Width - levelWidth * TileSize) / 2f,
            (previewFramebuffer.Texture.Height - levelHeight * TileSize) / 2f,
            0f
        );

        // draw background color
        Raylib.DrawRectangle(
            0, 0, levelWidth * TileSize, levelHeight * TileSize,
            RainEd.Instance.Preferences.BackgroundColor.ToRaylibColor(255)
        );

        // draw layers
        for (int i = 0; i < 3; i++)
        {
            if (fillLayer[i])
                Raylib.DrawRectangle(0, 0, levelWidth * TileSize, levelHeight * TileSize, layerColors[i]);
        }

        // level border
        Raylib.DrawRectangleLines(
            levelBufL * TileSize, levelBufT * TileSize,
            (levelWidth - levelBufR - levelBufL) * TileSize, (levelHeight - levelBufB - levelBufT) * TileSize,
            Color.White
        );

        // draw cameras
        var camOffset = (Camera.Size - Camera.StandardSize) / 2f;
        
        // outer border
        foreach (var camPos in CalcCameraPositions(cameraPositions))
        {
            Raylib.DrawRectangleLines(
                (int)(camPos.X * TileSize), (int)(camPos.Y * TileSize),
                (int)(Camera.Size.X * TileSize), (int)(Camera.Size.Y * TileSize),
                new Color(0, 255, 0, 255)
            );
        }

        // inner border
        foreach (var camPos in CalcCameraPositions(cameraPositions))
        {
            Raylib.DrawRectangleLines(
                (int)((camPos.X + camOffset.X) * TileSize), (int)((camPos.Y + camOffset.Y) * TileSize),
                (int)(Camera.StandardSize.X * TileSize), (int)(Camera.StandardSize.Y * TileSize),
                new Color(255, 0, 0, 255)
            );
        }

        Rlgl.PopMatrix();
        Raylib.EndTextureMode();
    }

    private static Level CreateLevel()
    {
        var level = new Level(levelWidth, levelHeight)
        {
            BufferTilesLeft = levelBufL,
            BufferTilesTop = levelBufT,
            BufferTilesRight = levelBufR,
            BufferTilesBot = levelBufB
        };

        // fill options
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                if (fillLayer[0])
                    level.Layers[0,x,y].Geo = GeoType.Solid;

                if (fillLayer[1])
                    level.Layers[1,x,y].Geo = GeoType.Solid;

                if (fillLayer[2])
                    level.Layers[2,x,y].Geo = GeoType.Solid;
            }
        }

        // camera placement
        foreach (var camPos in CalcCameraPositions())
        {
            level.Cameras.Add(new Camera(camPos));
        }

        return level;
    }
}