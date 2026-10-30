using System;
using System.Drawing;
using System.Threading.Tasks;
using OpenCvSharp;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public class RobloxCameraService : IRobloxCameraService
    {
        private readonly IInputSimulationService _inputService;
        private readonly IVisionService _visionService;
        private readonly IWindowCaptureService _captureService;

        public RobloxCameraService(
            IInputSimulationService inputService,
            IVisionService visionService,
            IWindowCaptureService captureService)
        {
            _inputService = inputService;
            _visionService = visionService;
            _captureService = captureService;
        }

        /// <summary>
        /// Zooms in all the way to First Person (snapping camera orientation to avatar forward vector),
        /// then zooms out a fixed number of steps to restore standard perspective.
        /// </summary>
        public async Task ResetCameraZoomAsync(IntPtr hWnd, int zoomInSteps = 8, int zoomOutSteps = 5)
        {
            // Zoom in with 'I' key to enter 1st person
            for (int i = 0; i < zoomInSteps; i++)
            {
                await _inputService.KeyPressAsync(hWnd, "I", 40);
                await Task.Delay(40);
            }

            // Brief pause for Roblox physics/camera interpolation to settle
            await Task.Delay(150);

            // Zoom out with 'O' key to fixed distance
            for (int i = 0; i < zoomOutSteps; i++)
            {
                await _inputService.KeyPressAsync(hWnd, "O", 40);
                await Task.Delay(40);
            }

            await Task.Delay(100);
        }

        /// <summary>
        /// Sweeps camera around 360 degrees via right-click horizontal drags until compass/landmark template matches.
        /// </summary>
        public async Task<bool> SweepAndAlignCompassAsync(
            IntPtr hWnd,
            Mat compassTemplate,
            Func<Mat?> captureFunc,
            double threshold = 0.80,
            int maxSteps = 12,
            int dragStepPixels = 60,
            Rectangle? compassRoi = null)
        {
            var clientRect = _captureService.GetClientRect(hWnd);
            int centerX = clientRect.Width > 0 ? clientRect.Width / 2 : 400;
            int centerY = clientRect.Height > 0 ? clientRect.Height / 2 : 300;

            for (int step = 0; step < maxSteps; step++)
            {
                // Capture current view
                using var currentFrame = captureFunc();
                if (currentFrame != null && !currentFrame.Empty())
                {
                    var match = _visionService.MatchTemplate(currentFrame, compassTemplate, threshold, compassRoi);
                    if (match.IsFound && match.Confidence >= threshold)
                    {
                        return true; // Successfully aligned!
                    }
                }

                // Drag right mouse to rotate camera yaw
                int startX = centerX;
                int startY = centerY;
                int endX = centerX + dragStepPixels;
                int endY = centerY;

                await _inputService.DragAsync(hWnd, startX, startY, endX, endY, 150, MouseButtonType.Right);
                await Task.Delay(200); // Allow camera rendering to settle
            }

            return false;
        }

        /// <summary>
        /// Drags right mouse upward to tilt the camera towards a top-down / bird's-eye angle.
        /// Top-down angles significantly reduce 3D perspective distortion.
        /// </summary>
        public async Task TiltToTopDownAsync(IntPtr hWnd, int dragUpPixels = 200)
        {
            var clientRect = _captureService.GetClientRect(hWnd);
            int centerX = clientRect.Width > 0 ? clientRect.Width / 2 : 400;
            int centerY = clientRect.Height > 0 ? (clientRect.Height * 2) / 3 : 400;

            int startX = centerX;
            int startY = centerY;
            int endX = centerX;
            int endY = Math.Max(50, centerY - dragUpPixels);

            await _inputService.DragAsync(hWnd, startX, startY, endX, endY, 250, MouseButtonType.Right);
            await Task.Delay(150);
        }

        /// <summary>
        /// Toggles Roblox Shift-Lock mode (Left Shift key).
        /// </summary>
        public async Task ToggleShiftLockAsync(IntPtr hWnd)
        {
            await _inputService.KeyPressAsync(hWnd, "SHIFT", 80);
            await Task.Delay(100);
        }
    }
}
