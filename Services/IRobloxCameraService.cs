using System;
using System.Threading.Tasks;
using OpenCvSharp;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public interface IRobloxCameraService
    {
        Task ResetCameraZoomAsync(IntPtr hWnd, int zoomInSteps = 8, int zoomOutSteps = 5);
        Task<bool> SweepAndAlignCompassAsync(
            IntPtr hWnd,
            Mat compassTemplate,
            Func<Mat?> captureFunc,
            double threshold = 0.80,
            int maxSteps = 12,
            int dragStepPixels = 60,
            System.Drawing.Rectangle? compassRoi = null);
        Task TiltToTopDownAsync(IntPtr hWnd, int dragUpPixels = 200);
        Task ToggleShiftLockAsync(IntPtr hWnd);
    }
}
