using System;
using System.IO;
using RROML.Abstractions;

namespace SlightlyBetterVisuals
{
    public sealed class SlightlyBetterVisualsMod : IRromlMod
    {
        public string Id { get { return "SlightlyBetterVisuals"; } }
        public string Name { get { return "(Slightly) Better Visuals"; } }
        public string Version { get { return "1.0.0"; } }

        public void OnLoad(IModContext context)
        {
            try
            {
                var settingsPath = context.GetUserGameConfigPath("GameUserSettings.ini");
                if (!File.Exists(settingsPath))
                {
                    context.Logger.Warn("SlightlyBetterVisuals could not find GameUserSettings.ini at " + settingsPath);
                    return;
                }

                var text = File.ReadAllText(settingsPath);
                const string oldLine = "fBrightness=2.200000";
                const string newLine = "fBrightness=2.000000";

                if (text.Contains(newLine))
                {
                    context.Logger.Info("SlightlyBetterVisuals brightness already set in " + settingsPath);
                    return;
                }

                if (!text.Contains(oldLine))
                {
                    context.Logger.Warn("SlightlyBetterVisuals did not find target brightness line in " + settingsPath);
                    return;
                }

                File.WriteAllText(settingsPath, text.Replace(oldLine, newLine));
                context.Logger.Info("SlightlyBetterVisuals updated brightness in " + settingsPath);
            }
            catch (Exception exception)
            {
                context.Logger.Error("SlightlyBetterVisuals failed to update GameUserSettings.ini.", exception);
            }
        }
    }
}