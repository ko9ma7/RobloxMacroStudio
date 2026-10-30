using System;
using System.Text.Json.Serialization;

namespace RobloxMacroStudio.Models
{
    public enum ActionType
    {
        FindImage,
        FindColor,
        Click,
        Drag,
        KeyPress,
        RobloxCameraReset,
        RobloxCameraSweep,
        CalibratedMove,
        Delay,
        ConditionBranch,
        LogMessage
    }

    public enum MouseButtonType
    {
        Left,
        Right,
        Middle,
        DoubleLeft
    }

    public enum CoordinateSource
    {
        Fixed,
        LastMatchCenter,
        ScreenCenter
    }

    public enum RobloxCameraResetMode
    {
        ZoomFirstPersonAndOut,   // Zoom in ('I') x N to snap heading, then zoom out ('O') x M
        ShiftLockToggle,         // Toggle Shift Lock to snap camera behind avatar
        TopDownBirdEye           // Drag right mouse upward to make view top-down
    }

    public class MacroAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "New Action";
        public ActionType Type { get; set; } = ActionType.Delay;
        public bool IsEnabled { get; set; } = true;

        // Image / Template Matching Parameters
        public string TemplateImagePath { get; set; } = string.Empty;
        public double MatchThreshold { get; set; } = 0.80; // 80% default confidence
        public bool UseMultiScale { get; set; } = false;
        public double MinScale { get; set; } = 0.8;
        public double MaxScale { get; set; } = 1.2;
        public double ScaleStep { get; set; } = 0.1;

        // Region of Interest (ROI) - 0 means full screen
        public int RoiX { get; set; } = 0;
        public int RoiY { get; set; } = 0;
        public int RoiWidth { get; set; } = 0;
        public int RoiHeight { get; set; } = 0;

        // Color Detection Parameters (HSV)
        public int HsvMinH { get; set; } = 0;
        public int HsvMaxH { get; set; } = 180;
        public int HsvMinS { get; set; } = 100;
        public int HsvMaxS { get; set; } = 255;
        public int HsvMinV { get; set; } = 100;
        public int HsvMaxV { get; set; } = 255;
        public int MinContourArea { get; set; } = 50;

        // Mouse Click / Coordinate Parameters
        public CoordinateSource CoordSource { get; set; } = CoordinateSource.Fixed;
        public int TargetX { get; set; } = 100;
        public int TargetY { get; set; } = 100;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;
        public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;

        // Mouse Drag / Camera Rotation Parameters
        public int DragStartX { get; set; } = 500;
        public int DragStartY { get; set; } = 300;
        public int DragEndX { get; set; } = 600;
        public int DragEndY { get; set; } = 300;
        public int DragDurationMs { get; set; } = 300;

        // Key Press Parameters
        public string KeyCode { get; set; } = "W"; // e.g. W, A, S, D, Space, E, I, O
        public int KeyHoldDurationMs { get; set; } = 100;

        // Roblox 3D Camera Parameters
        public RobloxCameraResetMode CameraResetMode { get; set; } = RobloxCameraResetMode.ZoomFirstPersonAndOut;
        public int ZoomInCount { get; set; } = 8;
        public int ZoomOutCount { get; set; } = 5;
        public int SweepSteps { get; set; } = 12; // 12 steps * 30 deg = 360 deg sweep
        public int SweepDeltaPixels { get; set; } = 60; // Right-drag pixels per step

        // Timing & Jitter
        public int DelayMs { get; set; } = 500;
        public bool UseRandomJitter { get; set; } = true;
        public int JitterPercentage { get; set; } = 15; // +/- 15% random delay

        // Conditional Flow
        public bool ConditionIfFound { get; set; } = true; // true: if found, false: if not found
        public int JumpToStepIndex { get; set; } = -1;     // -1 means proceed to next

        // Custom Log Message
        public string LogText { get; set; } = string.Empty;

        [JsonIgnore]
        public string Summary => Type switch
        {
            ActionType.FindImage => $"Find: {System.IO.Path.GetFileName(TemplateImagePath)} (Threshold: {MatchThreshold:P0})",
            ActionType.FindColor => $"Color Blob: H[{HsvMinH}-{HsvMaxH}] S[{HsvMinS}-{HsvMaxS}]",
            ActionType.Click => $"Click {MouseButton} at {CoordSource} ({TargetX}+{OffsetX}, {TargetY}+{OffsetY})",
            ActionType.Drag => $"Drag ({DragStartX},{DragStartY}) -> ({DragEndX},{DragEndY}) in {DragDurationMs}ms",
            ActionType.KeyPress => $"Key '{KeyCode}' for {KeyHoldDurationMs}ms",
            ActionType.RobloxCameraReset => $"3D Camera Reset ({CameraResetMode})",
            ActionType.RobloxCameraSweep => $"Camera Sweep ({SweepSteps} steps x {SweepDeltaPixels}px)",
            ActionType.CalibratedMove => $"Calibrated Walk '{KeyCode}' {KeyHoldDurationMs}ms",
            ActionType.Delay => $"Wait {DelayMs}ms (Jitter: {(UseRandomJitter ? JitterPercentage + "%" : "Off")})",
            ActionType.ConditionBranch => $"If {(ConditionIfFound ? "Found" : "Not Found")} -> Jump to #{JumpToStepIndex}",
            ActionType.LogMessage => $"Log: {LogText}",
            _ => "Unknown Action"
        };
    }
}
