using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using RobloxMacroStudio.Models;
using RobloxMacroStudio.Services;

namespace RobloxMacroStudio.Engine
{
    public enum EngineState
    {
        Stopped,
        Running,
        Paused
    }

    public class MacroEngine
    {
        private readonly IWindowCaptureService _captureService;
        private readonly IInputSimulationService _inputService;
        private readonly IVisionService _visionService;
        private readonly IRobloxCameraService _robloxCameraService;

        private readonly ConcurrentDictionary<string, Mat> _templateCache = new();

        private CancellationTokenSource? _cts;
        private ManualResetEventSlim? _pauseEvent;
        private Task? _executionTask;

        private readonly ExecutionContext _context = new();
        private readonly Random _random = new();

        public EngineState State { get; private set; } = EngineState.Stopped;

        public event Action<EngineState>? StateChanged;
        public event Action<MacroLogEntry>? LogEmitted;
        public event Action<int, MacroAction>? StepStarted;
        public event Action<int, MacroAction, bool>? StepCompleted;
        public event Action<Mat, TemplateMatchResult?>? FrameProcessed;

        public MacroEngine(
            IWindowCaptureService captureService,
            IInputSimulationService inputService,
            IVisionService visionService,
            IRobloxCameraService robloxCameraService)
        {
            _captureService = captureService;
            _inputService = inputService;
            _visionService = visionService;
            _robloxCameraService = robloxCameraService;
        }

        public void Start(IntPtr targetHwnd, MacroScript script)
        {
            if (State != EngineState.Stopped)
                return;

            if (targetHwnd == IntPtr.Zero)
            {
                Log(MacroLogLevel.Error, "Target window handle is invalid (0x0). Please select a window first.");
                return;
            }

            if (script.Actions == null || script.Actions.Count == 0)
            {
                Log(MacroLogLevel.Warning, "Macro script has no actions to execute.");
                return;
            }

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _context.Reset();

            State = EngineState.Running;
            StateChanged?.Invoke(State);
            Log(MacroLogLevel.Info, $"Macro started. Loop limit: {(script.LoopCount == 0 ? "Infinite" : script.LoopCount.ToString())}");

            _executionTask = Task.Run(() => RunScriptLoopAsync(targetHwnd, script, _cts.Token));
        }

        public void Pause()
        {
            if (State == EngineState.Running)
            {
                _pauseEvent?.Reset();
                State = EngineState.Paused;
                StateChanged?.Invoke(State);
                Log(MacroLogLevel.Warning, "Macro paused.");
            }
        }

        public void Resume()
        {
            if (State == EngineState.Paused)
            {
                _pauseEvent?.Set();
                State = EngineState.Running;
                StateChanged?.Invoke(State);
                Log(MacroLogLevel.Info, "Macro resumed.");
            }
        }

        public async Task StopAsync()
        {
            if (State == EngineState.Stopped)
                return;

            Log(MacroLogLevel.Info, "Stopping macro...");
            _pauseEvent?.Set();
            _cts?.Cancel();

            if (_executionTask != null)
            {
                try
                {
                    await _executionTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log(MacroLogLevel.Error, $"Error during engine shutdown: {ex.Message}");
                }
            }

            ClearTemplateCache();
            State = EngineState.Stopped;
            StateChanged?.Invoke(State);
            Log(MacroLogLevel.Success, "Macro stopped.");
        }

        private async Task RunScriptLoopAsync(IntPtr targetHwnd, MacroScript script, CancellationToken token)
        {
            int loopIndex = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    loopIndex++;
                    _context.CurrentLoop = loopIndex;
                    Log(MacroLogLevel.Info, $"=== Loop #{loopIndex} started ===");

                    int actionCount = script.Actions.Count;
                    int i = 0;

                    while (i < actionCount && !token.IsCancellationRequested)
                    {
                        _pauseEvent?.Wait(token);

                        var action = script.Actions[i];
                        if (!action.IsEnabled)
                        {
                            i++;
                            continue;
                        }

                        _context.CurrentStepIndex = i;
                        StepStarted?.Invoke(i, action);

                        bool success = await ExecuteSingleActionAsync(targetHwnd, action, token);
                        StepCompleted?.Invoke(i, action, success);

                        if (!success && script.StopOnError)
                        {
                            Log(MacroLogLevel.Error, $"Step #{i + 1} ({action.Name}) failed and StopOnError is enabled. Aborting macro.");
                            return;
                        }

                        if (action.Type == ActionType.ConditionBranch)
                        {
                            bool conditionMet = action.ConditionIfFound ? _context.LastMatch.IsFound : !_context.LastMatch.IsFound;
                            if (conditionMet && action.JumpToStepIndex >= 0 && action.JumpToStepIndex < actionCount)
                            {
                                Log(MacroLogLevel.Debug, $"Condition met -> Jumping to step #{action.JumpToStepIndex + 1}");
                                i = action.JumpToStepIndex;
                                continue;
                            }
                        }

                        i++;
                    }

                    if (script.LoopCount > 0 && loopIndex >= script.LoopCount)
                    {
                        Log(MacroLogLevel.Success, $"Completed specified loop count ({script.LoopCount}). Finished successfully.");
                        break;
                    }

                    if (script.LoopIntervalMs > 0)
                    {
                        await Task.Delay(script.LoopIntervalMs, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log(MacroLogLevel.Error, $"Unhandled exception in macro loop: {ex.Message}");
            }
            finally
            {
                State = EngineState.Stopped;
                StateChanged?.Invoke(State);
            }
        }

        private async Task<bool> ExecuteSingleActionAsync(IntPtr targetHwnd, MacroAction action, CancellationToken token)
        {
            try
            {
                switch (action.Type)
                {
                    case ActionType.FindImage:
                    {
                        if (string.IsNullOrWhiteSpace(action.TemplateImagePath) || !File.Exists(action.TemplateImagePath))
                        {
                            Log(MacroLogLevel.Warning, $"Template image file not found: {action.TemplateImagePath}");
                            _context.LastMatch = TemplateMatchResult.NotFound;
                            return false;
                        }

                        var template = GetOrCreateTemplate(action.TemplateImagePath);
                        if (template == null || template.Empty())
                        {
                            Log(MacroLogLevel.Warning, "Failed to load template image.");
                            _context.LastMatch = TemplateMatchResult.NotFound;
                            return false;
                        }

                        using var frame = _captureService.CaptureWindow(targetHwnd, true);
                        if (frame == null || frame.Empty())
                        {
                            Log(MacroLogLevel.Warning, "Failed to capture window frame.");
                            _context.LastMatch = TemplateMatchResult.NotFound;
                            return false;
                        }

                        Rectangle? roi = (action.RoiWidth > 0 && action.RoiHeight > 0)
                            ? new Rectangle(action.RoiX, action.RoiY, action.RoiWidth, action.RoiHeight)
                            : null;

                        TemplateMatchResult match;
                        if (action.UseMultiScale)
                        {
                            match = _visionService.MatchTemplateMultiScale(
                                frame,
                                template,
                                action.MatchThreshold,
                                action.MinScale,
                                action.MaxScale,
                                action.ScaleStep,
                                roi);
                        }
                        else
                        {
                            match = _visionService.MatchTemplate(frame, template, action.MatchThreshold, roi);
                        }

                        _context.LastMatch = match;

                        using var preview = frame.Clone();
                        if (match.IsFound)
                        {
                            _visionService.DrawMatchResult(preview, match, new Scalar(0, 255, 0), action.Name);
                            Log(MacroLogLevel.Success, $"Found '{action.Name}' at ({match.CenterX}, {match.CenterY}) [Score: {match.Confidence:P1}, Scale: {match.Scale:F2}x]");
                        }
                        else
                        {
                            Log(MacroLogLevel.Debug, $"Template '{action.Name}' not found [Best Score: {match.Confidence:P1}]");
                        }

                        FrameProcessed?.Invoke(preview, match);
                        return match.IsFound;
                    }

                    case ActionType.FindColor:
                    {
                        using var frame = _captureService.CaptureWindow(targetHwnd, true);
                        if (frame == null || frame.Empty())
                        {
                            Log(MacroLogLevel.Warning, "Failed to capture frame for color blob search.");
                            _context.LastMatch = TemplateMatchResult.NotFound;
                            return false;
                        }

                        Rectangle? roi = (action.RoiWidth > 0 && action.RoiHeight > 0)
                            ? new Rectangle(action.RoiX, action.RoiY, action.RoiWidth, action.RoiHeight)
                            : null;

                        var match = _visionService.FindColorBlob(
                            frame,
                            action.HsvMinH, action.HsvMaxH,
                            action.HsvMinS, action.HsvMaxS,
                            action.HsvMinV, action.HsvMaxV,
                            action.MinContourArea,
                            roi);

                        _context.LastMatch = match;

                        using var preview = frame.Clone();
                        if (match.IsFound)
                        {
                            _visionService.DrawMatchResult(preview, match, new Scalar(0, 255, 255), "ColorMatch");
                            Log(MacroLogLevel.Success, $"Color Blob detected at ({match.CenterX}, {match.CenterY}) Area: {match.BoundingBox.Width * match.BoundingBox.Height}px");
                        }
                        else
                        {
                            Log(MacroLogLevel.Debug, "No matching color blob detected.");
                        }

                        FrameProcessed?.Invoke(preview, match);
                        return match.IsFound;
                    }

                    case ActionType.Click:
                    {
                        var (clickX, clickY) = ResolveCoordinates(targetHwnd, action);
                        Log(MacroLogLevel.Info, $"Clicking {action.MouseButton} at ({clickX}, {clickY})");
                        await _inputService.ClickAsync(targetHwnd, clickX, clickY, action.MouseButton, 60);
                        return true;
                    }

                    case ActionType.Drag:
                    {
                        Log(MacroLogLevel.Info, $"Dragging from ({action.DragStartX}, {action.DragStartY}) to ({action.DragEndX}, {action.DragEndY}) over {action.DragDurationMs}ms");
                        await _inputService.DragAsync(targetHwnd, action.DragStartX, action.DragStartY, action.DragEndX, action.DragEndY, action.DragDurationMs, action.MouseButton);
                        return true;
                    }

                    case ActionType.KeyPress:
                    {
                        Log(MacroLogLevel.Info, $"Pressing Key '{action.KeyCode}' for {action.KeyHoldDurationMs}ms");
                        await _inputService.KeyPressAsync(targetHwnd, action.KeyCode, action.KeyHoldDurationMs);
                        return true;
                    }

                    case ActionType.RobloxCameraReset:
                    {
                        Log(MacroLogLevel.Info, $"Resetting Roblox 3D camera (Mode: {action.CameraResetMode})...");
                        switch (action.CameraResetMode)
                        {
                            case RobloxCameraResetMode.ZoomFirstPersonAndOut:
                                await _robloxCameraService.ResetCameraZoomAsync(targetHwnd, action.ZoomInCount, action.ZoomOutCount);
                                break;
                            case RobloxCameraResetMode.ShiftLockToggle:
                                await _robloxCameraService.ToggleShiftLockAsync(targetHwnd);
                                break;
                            case RobloxCameraResetMode.TopDownBirdEye:
                                await _robloxCameraService.TiltToTopDownAsync(targetHwnd, 200);
                                break;
                        }
                        Log(MacroLogLevel.Success, "Roblox 3D camera stabilized.");
                        return true;
                    }

                    case ActionType.RobloxCameraSweep:
                    {
                        if (string.IsNullOrWhiteSpace(action.TemplateImagePath) || !File.Exists(action.TemplateImagePath))
                        {
                            Log(MacroLogLevel.Warning, "RobloxCameraSweep requires an anchor/compass template image.");
                            return false;
                        }

                        var template = GetOrCreateTemplate(action.TemplateImagePath);
                        if (template == null) return false;

                        Log(MacroLogLevel.Info, $"Sweeping Roblox 3D camera to align with compass/anchor ({action.SweepSteps} steps)...");
                        bool aligned = await _robloxCameraService.SweepAndAlignCompassAsync(
                            targetHwnd,
                            template,
                            () => _captureService.CaptureWindow(targetHwnd, true),
                            action.MatchThreshold,
                            action.SweepSteps,
                            action.SweepDeltaPixels);

                        if (aligned)
                            Log(MacroLogLevel.Success, "Roblox 3D camera successfully aligned to anchor!");
                        else
                            Log(MacroLogLevel.Warning, "Camera sweep completed but anchor was not detected within threshold.");

                        return aligned;
                    }

                    case ActionType.CalibratedMove:
                    {
                        Log(MacroLogLevel.Info, $"Calibrated Walk '{action.KeyCode}' for {action.KeyHoldDurationMs}ms");
                        await _inputService.KeyPressAsync(targetHwnd, action.KeyCode, action.KeyHoldDurationMs);
                        return true;
                    }

                    case ActionType.MoveUntilCheckpoint:
                    {
                        Log(MacroLogLevel.Info, $"Move Until Checkpoint: walking '{action.KeyCode}' (Max: {action.MaxDurationMs}ms)...");

                        Mat? template = null;
                        if (action.CheckpointType == CheckpointCheckType.TemplateImage &&
                            !string.IsNullOrWhiteSpace(action.TemplateImagePath) &&
                            File.Exists(action.TemplateImagePath))
                        {
                            template = GetOrCreateTemplate(action.TemplateImagePath);
                        }

                        var stopwatch = Stopwatch.StartNew();
                        bool checkpointReached = false;

                        // 1. Begin movement (hold key in background)
                        await _inputService.KeyDownAsync(targetHwnd, action.KeyCode);

                        try
                        {
                            while (!token.IsCancellationRequested && stopwatch.ElapsedMilliseconds < action.MaxDurationMs)
                            {
                                _pauseEvent?.Wait(token);

                                int checkDelay = Math.Max(50, action.CheckIntervalMs);
                                await Task.Delay(checkDelay, token);

                                using var frame = _captureService.CaptureWindow(targetHwnd, true);
                                if (frame == null || frame.Empty())
                                    continue;

                                Rectangle? roi = (action.RoiWidth > 0 && action.RoiHeight > 0)
                                    ? new Rectangle(action.RoiX, action.RoiY, action.RoiWidth, action.RoiHeight)
                                    : null;

                                if (action.CheckpointType == CheckpointCheckType.TemplateImage && template != null)
                                {
                                    var match = action.UseMultiScale
                                        ? _visionService.MatchTemplateMultiScale(frame, template, action.MatchThreshold, action.MinScale, action.MaxScale, action.ScaleStep, roi)
                                        : _visionService.MatchTemplate(frame, template, action.MatchThreshold, roi);

                                    if (match.IsFound)
                                    {
                                        checkpointReached = true;
                                        _context.LastMatch = match;
                                        Log(MacroLogLevel.Success, $"Checkpoint reached! Template found at ({match.CenterX}, {match.CenterY}) Score: {match.Confidence:P1}");
                                        break;
                                    }
                                }
                                else if (action.CheckpointType == CheckpointCheckType.ColorBlob)
                                {
                                    var match = _visionService.FindColorBlob(
                                        frame,
                                        action.HsvMinH, action.HsvMaxH,
                                        action.HsvMinS, action.HsvMaxS,
                                        action.HsvMinV, action.HsvMaxV,
                                        action.MinContourArea,
                                        roi);

                                    if (match.IsFound)
                                    {
                                        checkpointReached = true;
                                        _context.LastMatch = match;
                                        Log(MacroLogLevel.Success, $"Checkpoint reached! Color blob detected at ({match.CenterX}, {match.CenterY})");
                                        break;
                                    }
                                }
                                else if (action.CheckpointType == CheckpointCheckType.TimedOnly)
                                {
                                    if (stopwatch.ElapsedMilliseconds >= action.KeyHoldDurationMs)
                                    {
                                        checkpointReached = true;
                                        break;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // 2. Guaranteed stop: release movement key
                            await _inputService.KeyUpAsync(targetHwnd, action.KeyCode);
                        }

                        if (!checkpointReached)
                        {
                            Log(MacroLogLevel.Warning, $"Movement timed out after {stopwatch.ElapsedMilliseconds}ms without reaching checkpoint. Stopped.");
                        }

                        // 3. Inertia settle delay
                        await Task.Delay(150, token);

                        // 4. Perform OnArrivalAction (e.g. Rotate Camera)
                        if (checkpointReached || action.ArrivalAction != OnArrivalAction.None)
                        {
                            var clientRect = _captureService.GetClientRect(targetHwnd);
                            int centerX = clientRect.Width > 0 ? clientRect.Width / 2 : 400;
                            int centerY = clientRect.Height > 0 ? clientRect.Height / 2 : 300;

                            switch (action.ArrivalAction)
                            {
                                case OnArrivalAction.RotateCameraRight:
                                    Log(MacroLogLevel.Info, $"Rotating camera RIGHT by {action.RotateDeltaPixels}px...");
                                    await _inputService.DragAsync(targetHwnd, centerX, centerY, centerX + action.RotateDeltaPixels, centerY, 200, MouseButtonType.Right);
                                    break;

                                case OnArrivalAction.RotateCameraLeft:
                                    Log(MacroLogLevel.Info, $"Rotating camera LEFT by {action.RotateDeltaPixels}px...");
                                    await _inputService.DragAsync(targetHwnd, centerX, centerY, Math.Max(20, centerX - action.RotateDeltaPixels), centerY, 200, MouseButtonType.Right);
                                    break;

                                case OnArrivalAction.ResetCameraHeading:
                                    Log(MacroLogLevel.Info, "Stabilizing camera heading via 1st person snap...");
                                    await _robloxCameraService.ResetCameraZoomAsync(targetHwnd, action.ZoomInCount, action.ZoomOutCount);
                                    break;

                                case OnArrivalAction.PressKey:
                                    Log(MacroLogLevel.Info, $"Pressing arrival action key '{action.ArrivalKey}'...");
                                    await _inputService.KeyPressAsync(targetHwnd, action.ArrivalKey, 100);
                                    break;

                                case OnArrivalAction.None:
                                default:
                                    break;
                            }

                            await Task.Delay(150, token);
                        }

                        return checkpointReached;
                    }

                    case ActionType.Delay:
                    {
                        int delay = action.DelayMs;
                        if (action.UseRandomJitter && action.JitterPercentage > 0)
                        {
                            double factor = 1.0 + (_random.NextDouble() * 2.0 - 1.0) * (action.JitterPercentage / 100.0);
                            delay = Math.Max(10, (int)(delay * factor));
                        }
                        await Task.Delay(delay, token);
                        return true;
                    }

                    case ActionType.ConditionBranch:
                        return true;

                    case ActionType.LogMessage:
                        Log(MacroLogLevel.Info, $"[User Log] {action.LogText}");
                        return true;

                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log(MacroLogLevel.Error, $"Error executing '{action.Name}': {ex.Message}");
                return false;
            }
        }

        private (int x, int y) ResolveCoordinates(IntPtr targetHwnd, MacroAction action)
        {
            switch (action.CoordSource)
            {
                case CoordinateSource.LastMatchCenter:
                    if (_context.LastMatch.IsFound)
                    {
                        return (
                            _context.LastMatch.CenterX + action.OffsetX,
                            _context.LastMatch.CenterY + action.OffsetY
                        );
                    }
                    return (action.TargetX + action.OffsetX, action.TargetY + action.OffsetY);

                case CoordinateSource.ScreenCenter:
                    var client = _captureService.GetClientRect(targetHwnd);
                    int cx = client.Width > 0 ? client.Width / 2 : 400;
                    int cy = client.Height > 0 ? client.Height / 2 : 300;
                    return (cx + action.OffsetX, cy + action.OffsetY);

                case CoordinateSource.Fixed:
                default:
                    return (action.TargetX + action.OffsetX, action.TargetY + action.OffsetY);
            }
        }

        private Mat? GetOrCreateTemplate(string path)
        {
            return _templateCache.GetOrAdd(path, p =>
            {
                if (!File.Exists(p)) return null!;
                return Cv2.ImRead(p, ImreadModes.Color);
            });
        }

        private void ClearTemplateCache()
        {
            foreach (var kvp in _templateCache)
            {
                kvp.Value?.Dispose();
            }
            _templateCache.Clear();
        }

        private void Log(MacroLogLevel level, string message)
        {
            LogEmitted?.Invoke(new MacroLogEntry { Level = level, Message = message });
        }
    }
}
