using System.Collections.Generic;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Engine
{
    public class ExecutionContext
    {
        public TemplateMatchResult LastMatch { get; set; } = TemplateMatchResult.NotFound;
        public int CurrentLoop { get; set; } = 0;
        public int CurrentStepIndex { get; set; } = 0;
        public Dictionary<string, object> Variables { get; } = new();

        public void Reset()
        {
            LastMatch = TemplateMatchResult.NotFound;
            CurrentLoop = 0;
            CurrentStepIndex = 0;
            Variables.Clear();
        }
    }
}
