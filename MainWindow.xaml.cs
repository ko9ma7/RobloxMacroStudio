using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using Microsoft.Win32;
using RobloxMacroStudio.Services;
using RobloxMacroStudio.ViewModels;

namespace RobloxMacroStudio
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly WindowPickerService _windowPickerService;

        private bool _isCrosshairDragging = false;
        private bool _isSelectingOnCanvas = false;
        private System.Windows.Point _selectionStartPt;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;

            _hotkeyService = new GlobalHotkeyService();
            _windowPickerService = new WindowPickerService();

            _hotkeyService.OnStartHotkeyPressed += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_vm.StartMacroCommand.CanExecute(null))
                    {
                        _vm.StartMacroCommand.Execute(null);
                    }
                });
            };

            _hotkeyService.OnStopHotkeyPressed += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_vm.StopMacroCommand.CanExecute(null))
                    {
                        _vm.StopMacroCommand.Execute(null);
                    }
                });
            };

            _hotkeyService.OnPauseHotkeyPressed += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_vm.PauseMacroCommand.CanExecute(null))
                    {
                        _vm.PauseMacroCommand.Execute(null);
                    }
                });
            };

            Loaded += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                _hotkeyService.Register(helper.Handle);
            };

            Closed += (s, e) =>
            {
                _hotkeyService.Dispose();
            };
        }

        // ================= TARGET CROSSHAIR DRAG & DROP =================
        private void CrosshairBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isCrosshairDragging = true;
                CrosshairBorder.CaptureMouse();
                Cursor = Cursors.Cross;
            }
        }

        private void CrosshairBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isCrosshairDragging)
            {
                var targetWin = _windowPickerService.GetWindowUnderCursor();
                if (targetWin != null && targetWin.Handle != new WindowInteropHelper(this).Handle)
                {
                    _windowPickerService.HighlightWindow(targetWin.Handle);
                }
            }
        }

        private void CrosshairBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCrosshairDragging)
            {
                _isCrosshairDragging = false;
                CrosshairBorder.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;

                var targetWin = _windowPickerService.GetWindowUnderCursor();
                if (targetWin != null && targetWin.Handle != new WindowInteropHelper(this).Handle)
                {
                    _vm.SelectWindowFromPoint(targetWin.Handle);
                }
            }
        }

        // ================= INTERACTIVE CANVAS SELECTION (ROI & TEMPLATE SNIP) =================
        private void PreviewCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _vm.PreviewImage != null)
            {
                _isSelectingOnCanvas = true;
                _selectionStartPt = e.GetPosition(PreviewCanvas);

                SelectionVisualRect.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionVisualRect, _selectionStartPt.X);
                Canvas.SetTop(SelectionVisualRect, _selectionStartPt.Y);
                SelectionVisualRect.Width = 0;
                SelectionVisualRect.Height = 0;

                PreviewCanvas.CaptureMouse();
            }
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelectingOnCanvas)
            {
                var currentPt = e.GetPosition(PreviewCanvas);

                double x = Math.Min(_selectionStartPt.X, currentPt.X);
                double y = Math.Min(_selectionStartPt.Y, currentPt.Y);
                double w = Math.Abs(currentPt.X - _selectionStartPt.X);
                double h = Math.Abs(currentPt.Y - _selectionStartPt.Y);

                // Constrain within image bounds
                if (_vm.PreviewImage != null)
                {
                    x = Math.Max(0, x);
                    y = Math.Max(0, y);
                    w = Math.Min(w, _vm.PreviewImage.PixelWidth - x);
                    h = Math.Min(h, _vm.PreviewImage.PixelHeight - y);
                }

                Canvas.SetLeft(SelectionVisualRect, x);
                Canvas.SetTop(SelectionVisualRect, y);
                SelectionVisualRect.Width = w;
                SelectionVisualRect.Height = h;

                _vm.SelectionRect = new System.Drawing.Rectangle((int)x, (int)y, (int)w, (int)h);
            }
        }

        private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelectingOnCanvas)
            {
                _isSelectingOnCanvas = false;
                PreviewCanvas.ReleaseMouseCapture();

                if (_vm.SelectionRect.Width < 5 || _vm.SelectionRect.Height < 5)
                {
                    SelectionVisualRect.Visibility = Visibility.Collapsed;
                    _vm.SelectionRect = System.Drawing.Rectangle.Empty;
                }
            }
        }

        // ================= PRESET SELECTION =================
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is string presetName)
            {
                _vm.LoadPresetByName(presetName);
            }
        }

        // ================= BROWSE TEMPLATE FILE =================
        private void BrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
                Title = "Select Template Image"
            };

            if (ofd.ShowDialog() == true && _vm.SelectedAction != null)
            {
                _vm.SelectedAction.TemplateImagePath = ofd.FileName;
                // Force UI update
                _vm.SelectedAction = _vm.SelectedAction;
            }
        }
    }
}
