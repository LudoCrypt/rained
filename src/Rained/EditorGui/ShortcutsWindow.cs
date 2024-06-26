using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
namespace RainEd;

static partial class ShortcutsWindow
{
    public static bool IsWindowOpen = false;
    
    private readonly static string[] NavTabs = new string[] { "General", "Environment Edit", "Geometry Edit", "Tile Edit", "Camera Edit", "Light Edit", "Effects Edit", "Prop Edit" };
    private static int selectedNavTab = 0;
    private static int lastEditMode = -1;

    private readonly static (string, string)[][] TabData = new (string, string)[][]
    {
        // General
        [
            ("Scroll Wheel", "Zoom"),
            ("Middle Mouse", "Pan"),
            ("Alt+Left Mouse", "Pan"),
            ("[Undo]", "Undo"),
            ("[Redo]", "Redo"),
            ("[Render]", "Render"),
            ("1", "Edit environment"),
            ("2", "Edit geometry"),
            ("3", "Edit tiles"),
            ("4", "Edit cameras"),
            ("5", "Edit light"),
            ("6", "Edit effects"),
            ("7", "Edit props"),
        ],

        // Environment
        [
            ("Left Mouse", "Set water level")
        ],

        // Geometry
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "Browse tool selector"),
            ("Left Mouse", "Place/remove"),
            ("Right Mouse", "Remove"),
            ("Shift+Left Mouse", "Rect fill"),
            ("[SwitchLayer]", "Cycle layers"),
            ("[ToggleLayer1]", "Toggle layer 1"),
            ("[ToggleLayer2]", "Toggle layer 2"),
            ("[ToggleLayer3]", "Toggle layer 3"),
        ],

        // Tile
        [
            ("[SwitchLayer]", "Switch layer"),
            ("[SwitchTab]", "Switch selector tab"),
            ("[NavUp]/[NavDown]", "Browse selected category"),
            ("[NavLeft]/[NavRight]", "Browse tile categories"),
            ("Shift+Mouse Wheel", "Change material brush size"),
            ("[Eyedropper]", "Sample tile from level"),
            ("[SetMaterial]", "Set selected to default material"),
            ("Left Mouse", "Place tile/material"),
            ("Right Mouse", "Remove tile/material"),
            ("[TileIgnoreDifferent]+Left Mouse", "Ignore differing materials"),
            ("[TileForcePlacement]+Left Mouse", "Force tile placement"),
            ("[TileForceGeometry]+Left Mouse", "Force tile geometry"),
            ("[TileForceGeometry]+Right Mouse", "Remove tile and geometry"),
        ],

        // Camera
        [
            ("Double-click", "Create camera"),
            ("[NewObject]", "Create camera"),
            ("Left Mouse", "Select camera"),
            ("Right Mouse", "Reset camera corner"),
            ("Backspace/Delete", "Delete selected camera"),
            ("[Duplicate]", "Duplicate selected camera"),
            ("[CameraSnapX]/[NavUp]/[NavDown]", "Snap X to other cameras"),
            ("[CameraSnapY]/[NavLeft]/[NavRight]", "Snap Y to other cameras"),
        ],

        // Light
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "Browse brush catalog"),
            ("[ZoomLightIn]", "Move light inward"),
            ("[ZoomLightOut]", "Move light outward"),
            ("[RotateLightCW]", "Rotate light clockwise"),
            ("[RotateLightCCW]", "Rotate light counter-clockwise"),
            ("Q+Mouse Move", "Scale brush"),
            ("E+Mouse Move", "Rotate brush"),
            ("[ResetBrushTransform]", "Reset brush transform"),
            ("Left Mouse", "Paint shadow"),
            ("Right Mouse", "Paint light"),
        ],

        // Effects
        [
            ("Left Mouse", "Paint effect"),
            ("Shift+Left Mouse", "Paint effect stronger"),
            ("Right Mouse", "Erase effect"),
            ("Shift+Mouse Wheel", "Change brush size")
        ],

        // Props
        [
            ("[SwitchLayer]", "Switch layer"),
            ("[SwitchTab]", "Switch selector tab"),
            ("[NavUp]/[NavDown]", "Browse selected category"),
            ("[NavLeft]/[NavRight]", "Browse prop categories"),
            ("[Eyedropper]", "Sample prop under mouse"),
            ("Double-click", "Create prop"),
            ("[NewObject]", "Create prop"),
            ("Left Mouse", "Select prop"),
            ("Shift+Left Mouse", "Add prop to selection"),
            ("Right Mouse", "Find prop(s) under the mouse"),
            ("Backspace/Delete", "Delete selected prop(s)"),
            ("[ToggleVertexMode]", "Toggle vertex mode"),
            ("[Duplicate]", "Duplicate selected prop(s)"),
        ]
    };

    public static void ShowWindow()
    {
        var editMode = RainEd.Instance.Window.EditMode;

        if (lastEditMode == -1)
        {
            lastEditMode = editMode;
        }

        if (editMode != lastEditMode)
        {
            // if not selected general, switch tab to whatever editor user is currently one
            if (selectedNavTab > 0)
                selectedNavTab = editMode + 1;
            
            lastEditMode = editMode;
        }

        if (!IsWindowOpen) return;

        if (ImGui.Begin("Shortcuts", ref IsWindowOpen))
        {
            ImGui.BeginChild("Nav", new Vector2(ImGui.GetTextLineHeight() * 12.0f, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);
            {
                for (int i = 0; i < NavTabs.Length; i++)
                {
                    if (ImGui.Selectable(NavTabs[i], i == selectedNavTab))
                    {
                        selectedNavTab = i;
                    }
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Controls", ImGui.GetContentRegionAvail());
            ShowTab();
            ImGui.EndChild();
        } ImGui.End();
    }

    private static void ShowTab()
    {
        var strBuilder = new StringBuilder();

        var tableFlags = ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("ControlTable", 2, tableFlags))
        {
            ImGui.TableSetupColumn("Shortcut");
            ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();

            var tabData = TabData[selectedNavTab];

            for (int i = 0; i < tabData.Length; i++)
            {
                var tuple = tabData[i];
                var str = ShortcutRegex().Replace(tuple.Item1, ShortcutEvaluator);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(str);
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(tuple.Item2);
            }
            
            ImGui.EndTable();
        }
    }

    private static string ShortcutEvaluator(Match match)
    {
        var shortcutId = Enum.Parse<KeyShortcut>(match.Value[1..^1]);
        return KeyShortcuts.GetShortcutString(shortcutId);
    }

    [GeneratedRegex("\\[(\\w+?)\\]")]
    private static partial Regex ShortcutRegex();
}