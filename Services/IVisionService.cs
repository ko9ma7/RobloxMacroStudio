using System.Drawing;
using OpenCvSharp;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public interface IVisionService
    {
        TemplateMatchResult MatchTemplate(Mat source, Mat template, double threshold = 0.8, System.Drawing.Rectangle? roi = null);
        TemplateMatchResult MatchTemplateMultiScale(Mat source, Mat template, double threshold = 0.8, double minScale = 0.8, double maxScale = 1.2, double scaleStep = 0.1, System.Drawing.Rectangle? roi = null);
        TemplateMatchResult FindColorBlob(Mat source, int minH, int maxH, int minS, int maxS, int minV, int maxV, int minArea = 50, System.Drawing.Rectangle? roi = null);
        void DrawMatchResult(Mat target, TemplateMatchResult result, Scalar color, string label = "");
    }
}
