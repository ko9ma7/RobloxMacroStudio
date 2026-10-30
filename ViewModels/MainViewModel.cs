using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using RobloxMacroStudio.Engine;
using RobloxMacroStudio.Models;
using RobloxMacroStudio.Services;

namespace RobloxMacroStudio.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IWindowCaptureService _captureService;
        private readonly IInputSimulationService _inputService;
        private readonly IVisionService _visionService;
        private readonly IRobloxCameraService _robloxCameraService;
        private readonly WindowPickerService _windowPickerService;
        private readonly MacroEngine _engine;

        public ObservableCollection<WindowInfo> OpenWindows { get; } = new();
        public ObservableCollection<MacroLogEntry> Logs { get; } = new();

        private WindowInfo? _selectedWindow;
        public WindowInfo? SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                if (SetProperty(ref _selectedWindow, value))
                {
                    OnSelectedWindowChanged();
                }
            }
        }

        private MacroScript _script = new();
        public MacroScript Script
        {
            get => _script;
            set => SetProperty(ref _script, value);
        }

        private MacroAction? _selectedAction;
        public MacroAction? SelectedAction
        {
            get => _selectedAction;
            set => SetProperty(ref _selectedAction, value);
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private Mat? _currentCapturedMat;

        private EngineState _engineState = EngineState.Stopped;
        public EngineState EngineState
        {
            get => _engineState;
            set
            {
                if (SetProperty(ref _engineState, value))
                {
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(IsStopped));
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusBadgeColor));
                }
            }
        }

        public bool IsRunning => EngineState == EngineState.Running;
        public bool IsStopped => EngineState == EngineState.Stopped;

        public string StatusBadgeText => EngineState switch
        {
            EngineState.Running => "RUNNING",
            EngineState.Paused => "PAUSED",
            _ => "IDLE"
        };

        public string StatusBadgeColor => EngineState switch
        {
            EngineState.Running => "#10B981", // Green
            EngineState.Paused => "#F59E0B",  // Amber
            _ => "#6B7280"                   // Slate Gray
        };

        public bool UseBackgroundInput
        {
            get => _inputService.UseBackgroundMode;
            set
            {
                _inputService.UseBackgroundMode = value;
                OnPropertyChanged();
            }
        }

        // Selection Rect on Preview Image (ROI or Template Snipping)
        private System.Drawing.Rectangle _selectionRect = System.Drawing.Rectangle.Empty;
        public System.Drawing.Rectangle SelectionRect
        {
            get => _selectionRect;
            set
            {
                if (SetProperty(ref _selectionRect, value))
                {
                    OnPropertyChanged(nameof(HasValidSelection));
                    OnPropertyChanged(nameof(SelectionStatusText));
                }
            }
        }

        public bool HasValidSelection => SelectionRect.Width > 5 && SelectionRect.Height > 5;
        public string SelectionStatusText => HasValidSelection
            ? $"Selected: ({SelectionRect.X}, {SelectionRect.Y}) {SelectionRect.Width}x{SelectionRect.Height}px"
            : "Drag on preview to select ROI or snip template";

        // Commands
        public ICommand RefreshWindowsCommand { get; }
        public ICommand CapturePreviewCommand { get; }
        public ICommand StartMacroCommand { get; }
        public ICommand StopMacroCommand { get; }
        public ICommand PauseMacroCommand { get; }

        public ICommand AddActionCommand { get; }
        public ICommand DeleteActionCommand { get; }
        public ICommand MoveActionUpCommand { get; }
        public ICommand MoveActionDownCommand { get; }
        public ICommand DuplicateActionCommand { get; }

        public ICommand LoadScriptCommand { get; }
        public ICommand SaveScriptCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public ICommand SaveTemplateFromSelectionCommand { get; }
        public ICommand SetRoiFromSelectionCommand { get; }
        public ICommand TestCurrentActionCommand { get; }

        public ICommand QuickResetRobloxCameraCommand { get; }
        public ICommand QuickTiltBirdEyeCommand { get; }
        public ICommand QuickShiftLockCommand { get; }

        public MainViewModel()
        {
            _captureService = new Win32CaptureService();
            _inputService = new WindowsInputService();
            _visionService = new OpenCvVisionService();
            _robloxCameraService = new RobloxCameraService(_inputService, _visionService, _captureService);
            _windowPickerService = new WindowPickerService();
            _engine = new MacroEngine(_captureService, _inputService, _visionService, _robloxCameraService);

            _engine.StateChanged += state =>
            {
                Application.Current.Dispatcher.Invoke(() => EngineState = state);
            };

            _engine.LogEmitted += entry =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Insert(0, entry);
                    if (Logs.Count > 300)
                    {
                        Logs.RemoveAt(Logs.Count - 1);
                    }
                });
            };

            _engine.FrameProcessed += (frame, result) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _currentCapturedMat?.Dispose();
                        _currentCapturedMat = frame.Clone();
                        PreviewImage = _currentCapturedMat.ToBitmapSource();
                    }
                    catch { }
                });
            };

            RefreshWindowsCommand = new RelayCommand(RefreshWindows);
            CapturePreviewCommand = new RelayCommand(CapturePreview);
            StartMacroCommand = new RelayCommand(StartMacro, () => IsStopped && SelectedWindow != null);
            StopMacroCommand = new RelayCommand(async () => await _engine.StopAsync(), () => !IsStopped);
            PauseMacroCommand = new RelayCommand(TogglePause, () => !IsStopped);

            AddActionCommand = new RelayCommand(param => AddAction(param as string));
            DeleteActionCommand = new RelayCommand(DeleteSelectedAction, () => SelectedAction != null);
            MoveActionUpCommand = new RelayCommand(MoveActionUp, () => SelectedAction != null);
            MoveActionDownCommand = new RelayCommand(MoveActionDown, () => SelectedAction != null);
            DuplicateActionCommand = new RelayCommand(DuplicateAction, () => SelectedAction != null);

            LoadScriptCommand = new RelayCommand(LoadScript);
            SaveScriptCommand = new RelayCommand(SaveScript);
            ClearLogsCommand = new RelayCommand(() => Logs.Clear());

            SaveTemplateFromSelectionCommand = new RelayCommand(SaveTemplateFromSelection, () => HasValidSelection && _currentCapturedMat != null);
            SetRoiFromSelectionCommand = new RelayCommand(SetRoiFromSelection, () => HasValidSelection && SelectedAction != null);
            TestCurrentActionCommand = new RelayCommand(async () => await TestCurrentActionAsync(), () => SelectedAction != null && SelectedWindow != null);

            QuickResetRobloxCameraCommand = new RelayCommand(async () =>
            {
                if (SelectedWindow == null) return;
                AddLog(MacroLogLevel.Info, "Quick Action: Resetting Roblox 3D camera to heading...");
                await _robloxCameraService.ResetCameraZoomAsync(SelectedWindow.Handle, 8, 5);
                AddLog(MacroLogLevel.Success, "Roblox 3D camera reset completed.");
            }, () => SelectedWindow != null);

            QuickTiltBirdEyeCommand = new RelayCommand(async () =>
            {
                if (SelectedWindow == null) return;
                AddLog(MacroLogLevel.Info, "Quick Action: Tilting camera to Top-Down Bird's Eye view...");
                await _robloxCameraService.TiltToTopDownAsync(SelectedWindow.Handle, 200);
                AddLog(MacroLogLevel.Success, "Top-down camera tilt completed.");
            }, () => SelectedWindow != null);

            QuickShiftLockCommand = new RelayCommand(async () =>
            {
                if (SelectedWindow == null) return;
                AddLog(MacroLogLevel.Info, "Quick Action: Toggling Roblox Shift-Lock...");
                await _robloxCameraService.ToggleShiftLockAsync(SelectedWindow.Handle);
                AddLog(MacroLogLevel.Success, "Shift-Lock toggled.");
            }, () => SelectedWindow != null);

            // Populate sample default actions
            InitializeDefaultScript();
            RefreshWindows();
        }

        public void InitializeDefaultScript()
        {
            Script.Title = "Roblox 3D Viewport Stabilization & Auto Loop";
            Script.Description = "Resets 3D camera zoom/heading, verifies HUD anchor, and performs calibrated navigation.";
            Script.LoopCount = 0; // Infinite
            Script.LoopIntervalMs = 500;

            Script.Actions.Clear();

            Script.Actions.Add(new MacroAction
            {
                Name = "Stabilize 3D Camera",
                Type = ActionType.RobloxCameraReset,
                CameraResetMode = RobloxCameraResetMode.ZoomFirstPersonAndOut,
                ZoomInCount = 8,
                ZoomOutCount = 5
            });

            Script.Actions.Add(new MacroAction
            {
                Name = "Wait Camera Settle",
                Type = ActionType.Delay,
                DelayMs = 300,
                UseRandomJitter = true
            });

            Script.Actions.Add(new MacroAction
            {
                Name = "Calibrated Walk Forward",
                Type = ActionType.CalibratedMove,
                KeyCode = "W",
                KeyHoldDurationMs = 800
            });

            Script.Actions.Add(new MacroAction
            {
                Name = "Interact / Attack (Click)",
                Type = ActionType.Click,
                CoordSource = CoordinateSource.ScreenCenter,
                MouseButton = MouseButtonType.Left,
                OffsetX = 0,
                OffsetY = 0
            });

            Script.Actions.Add(new MacroAction
            {
                Name = "Rest Delay",
                Type = ActionType.Delay,
                DelayMs = 1000,
                UseRandomJitter = true,
                JitterPercentage = 20
            });

            SelectedAction = Script.Actions.FirstOrDefault();
        }

        public void RefreshWindows()
        {
            var windows = _captureService.GetOpenWindows();
            OpenWindows.Clear();

            WindowInfo? preferred = null;

            foreach (var win in windows)
            {
                OpenWindows.Add(win);

                // Auto-pick LDPlayer or Roblox if found
                if (preferred == null && win.IsEmulator)
                {
                    preferred = win;
                }
            }

            if (preferred != null)
            {
                SelectedWindow = preferred;
                AddLog(MacroLogLevel.Success, $"Auto-selected detected emulator window: {preferred.DisplayName}");
            }
            else if (OpenWindows.Count > 0 && SelectedWindow == null)
            {
                SelectedWindow = OpenWindows[0];
            }
        }

        private void OnSelectedWindowChanged()
        {
            if (SelectedWindow != null)
            {
                AddLog(MacroLogLevel.Info, $"Target window set to: {SelectedWindow.Title} (HWND: 0x{SelectedWindow.Handle.ToInt64():X8})");
                CapturePreview();
            }
        }

        public void CapturePreview()
        {
            if (SelectedWindow == null) return;

            try
            {
                var mat = _captureService.CaptureWindow(SelectedWindow.Handle, true);
                if (mat != null && !mat.Empty())
                {
                    _currentCapturedMat?.Dispose();
                    _currentCapturedMat = mat;
                    PreviewImage = _currentCapturedMat.ToBitmapSource();
                }
                else
                {
                    AddLog(MacroLogLevel.Warning, "Window capture returned empty frame.");
                }
            }
            catch (Exception ex)
            {
                AddLog(MacroLogLevel.Error, $"Capture failed: {ex.Message}");
            }
        }

        public void SelectWindowFromPoint(IntPtr hWnd)
        {
            var info = _captureService.GetWindowInfo(hWnd);
            if (info != null)
            {
                // Find or add to list
                var existing = OpenWindows.FirstOrDefault(w => w.Handle == info.Handle);
                if (existing != null)
                {
                    SelectedWindow = existing;
                }
                else
                {
                    OpenWindows.Insert(0, info);
                    SelectedWindow = info;
                }
                AddLog(MacroLogLevel.Success, $"Target Crosshair picked: {info.DisplayName}");
            }
        }

        public void StartMacro()
        {
            if (SelectedWindow == null)
            {
                MessageBox.Show("Please select a target window first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _engine.Start(SelectedWindow.Handle, Script);
        }

        public void TogglePause()
        {
            if (EngineState == EngineState.Running)
            {
                _engine.Pause();
            }
            else if (EngineState == EngineState.Paused)
            {
                _engine.Resume();
            }
        }

        private void AddAction(string? typeName)
        {
            var type = ActionType.Delay;
            if (!string.IsNullOrEmpty(typeName) && Enum.TryParse<ActionType>(typeName, out var parsed))
            {
                type = parsed;
            }

            var newAction = new MacroAction
            {
                Name = $"New {type}",
                Type = type
            };

            Script.Actions.Add(newAction);
            SelectedAction = newAction;
            AddLog(MacroLogLevel.Info, $"Added new action: {newAction.Name}");
        }

        private void DeleteSelectedAction()
        {
            if (SelectedAction == null) return;

            int idx = Script.Actions.IndexOf(SelectedAction);
            Script.Actions.Remove(SelectedAction);

            if (Script.Actions.Count > 0)
            {
                SelectedAction = Script.Actions[Math.Clamp(idx, 0, Script.Actions.Count - 1)];
            }
            else
            {
                SelectedAction = null;
            }
        }

        private void MoveActionUp()
        {
            if (SelectedAction == null) return;
            int idx = Script.Actions.IndexOf(SelectedAction);
            if (idx > 0)
            {
                Script.Actions.Move(idx, idx - 1);
            }
        }

        private void MoveActionDown()
        {
            if (SelectedAction == null) return;
            int idx = Script.Actions.IndexOf(SelectedAction);
            if (idx >= 0 && idx < Script.Actions.Count - 1)
            {
                Script.Actions.Move(idx, idx + 1);
            }
        }

        private void DuplicateAction()
        {
            if (SelectedAction == null) return;

            // Deep clone via JSON
            var json = System.Text.Json.JsonSerializer.Serialize(SelectedAction);
            var copy = System.Text.Json.JsonSerializer.Deserialize<MacroAction>(json);
            if (copy != null)
            {
                copy.Id = Guid.NewGuid().ToString("N")[..8];
                copy.Name += " (Copy)";
                int idx = Script.Actions.IndexOf(SelectedAction);
                Script.Actions.Insert(idx + 1, copy);
                SelectedAction = copy;
            }
        }

        private void SaveTemplateFromSelection()
        {
            if (_currentCapturedMat == null || !HasValidSelection)
            {
                MessageBox.Show("Please select a valid region on the preview image first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                FileName = $"template_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var r = new OpenCvSharp.Rect(SelectionRect.X, SelectionRect.Y, SelectionRect.Width, SelectionRect.Height);
                    using var cropped = new Mat(_currentCapturedMat, r);
                    Cv2.ImWrite(sfd.FileName, cropped);

                    AddLog(MacroLogLevel.Success, $"Template snippet saved: {sfd.FileName}");

                    // If currently selected action is FindImage, auto-fill the path!
                    if (SelectedAction != null && SelectedAction.Type == ActionType.FindImage)
                    {
                        SelectedAction.TemplateImagePath = sfd.FileName;
                        OnPropertyChanged(nameof(SelectedAction));
                        AddLog(MacroLogLevel.Info, $"Assigned saved template to selected action '{SelectedAction.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    AddLog(MacroLogLevel.Error, $"Failed to save snippet: {ex.Message}");
                }
            }
        }

        private void SetRoiFromSelection()
        {
            if (SelectedAction == null || !HasValidSelection) return;

            SelectedAction.RoiX = SelectionRect.X;
            SelectedAction.RoiY = SelectionRect.Y;
            SelectedAction.RoiWidth = SelectionRect.Width;
            SelectedAction.RoiHeight = SelectionRect.Height;
            OnPropertyChanged(nameof(SelectedAction));

            AddLog(MacroLogLevel.Info, $"Set ROI for '{SelectedAction.Name}' to ({SelectionRect.X}, {SelectionRect.Y}, {SelectionRect.Width}x{SelectionRect.Height})");
        }

        private async Task TestCurrentActionAsync()
        {
            if (SelectedAction == null || SelectedWindow == null) return;

            AddLog(MacroLogLevel.Info, $"Testing single action: '{SelectedAction.Name}' ({SelectedAction.Type})...");

            switch (SelectedAction.Type)
            {
                case ActionType.FindImage:
                {
                    if (string.IsNullOrEmpty(SelectedAction.TemplateImagePath) || !File.Exists(SelectedAction.TemplateImagePath))
                    {
                        AddLog(MacroLogLevel.Warning, "Template image path is missing or invalid.");
                        return;
                    }

                    using var frame = _captureService.CaptureWindow(SelectedWindow.Handle, true);
                    if (frame == null) { AddLog(MacroLogLevel.Error, "Capture failed."); return; }

                    using var tmpl = Cv2.ImRead(SelectedAction.TemplateImagePath, ImreadModes.Color);
                    var roi = (SelectedAction.RoiWidth > 0 && SelectedAction.RoiHeight > 0)
                        ? new System.Drawing.Rectangle(SelectedAction.RoiX, SelectedAction.RoiY, SelectedAction.RoiWidth, SelectedAction.RoiHeight)
                        : (System.Drawing.Rectangle?)null;

                    var res = SelectedAction.UseMultiScale
                        ? _visionService.MatchTemplateMultiScale(frame, tmpl, SelectedAction.MatchThreshold, SelectedAction.MinScale, SelectedAction.MaxScale, SelectedAction.ScaleStep, roi)
                        : _visionService.MatchTemplate(frame, tmpl, SelectedAction.MatchThreshold, roi);

                    using var preview = frame.Clone();
                    if (res.IsFound)
                    {
                        _visionService.DrawMatchResult(preview, res, new Scalar(0, 255, 0), "MatchTest");
                        AddLog(MacroLogLevel.Success, $"[TEST SUCCESS] Found at ({res.CenterX}, {res.CenterY}) Score: {res.Confidence:P1}");
                    }
                    else
                    {
                        AddLog(MacroLogLevel.Warning, $"[TEST FAILED] Not found. Best Score: {res.Confidence:P1} (Threshold: {SelectedAction.MatchThreshold:P1})");
                    }
                    PreviewImage = preview.ToBitmapSource();
                    break;
                }

                case ActionType.Click:
                {
                    AddLog(MacroLogLevel.Info, $"Testing click at ({SelectedAction.TargetX}, {SelectedAction.TargetY})...");
                    await _inputService.ClickAsync(SelectedWindow.Handle, SelectedAction.TargetX, SelectedAction.TargetY, SelectedAction.MouseButton, 60);
                    AddLog(MacroLogLevel.Success, "Click test sent.");
                    break;
                }

                case ActionType.KeyPress:
                {
                    AddLog(MacroLogLevel.Info, $"Testing keypress '{SelectedAction.KeyCode}' for {SelectedAction.KeyHoldDurationMs}ms...");
                    await _inputService.KeyPressAsync(SelectedWindow.Handle, SelectedAction.KeyCode, SelectedAction.KeyHoldDurationMs);
                    AddLog(MacroLogLevel.Success, "Key test sent.");
                    break;
                }

                case ActionType.RobloxCameraReset:
                {
                    AddLog(MacroLogLevel.Info, "Testing 3D camera reset...");
                    await _robloxCameraService.ResetCameraZoomAsync(SelectedWindow.Handle, SelectedAction.ZoomInCount, SelectedAction.ZoomOutCount);
                    AddLog(MacroLogLevel.Success, "Camera reset test sent.");
                    break;
                }

                default:
                    AddLog(MacroLogLevel.Info, $"Action type {SelectedAction.Type} verified.");
                    break;
            }
        }

        private void LoadScript()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Macro Script (*.macro.json;*.json)|*.macro.json;*.json|All Files (*.*)|*.*",
                Title = "Open Macro Script"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var loaded = MacroScript.LoadFromFile(ofd.FileName);
                    Script = loaded;
                    SelectedAction = Script.Actions.FirstOrDefault();
                    AddLog(MacroLogLevel.Success, $"Loaded script: {Path.GetFileName(ofd.FileName)} ({Script.Actions.Count} actions)");
                }
                catch (Exception ex)
                {
                    AddLog(MacroLogLevel.Error, $"Failed to load script: {ex.Message}");
                }
            }
        }

        private void SaveScript()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Macro Script (*.macro.json)|*.macro.json|JSON File (*.json)|*.json",
                DefaultExt = ".macro.json",
                FileName = $"{Script.Title.Replace(" ", "_")}.macro.json",
                Title = "Save Macro Script"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    Script.SaveToFile(sfd.FileName);
                    AddLog(MacroLogLevel.Success, $"Saved script to: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    AddLog(MacroLogLevel.Error, $"Failed to save script: {ex.Message}");
                }
            }
        }

        private void AddLog(MacroLogLevel level, string message)
        {
            Logs.Insert(0, new MacroLogEntry { Level = level, Message = message });
            if (Logs.Count > 300)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
