using BepInEx;
using BepInEx.Logging;

namespace NonPipScopes
{
    [BepInPlugin("7Bpencil.NonPipScopes", "NonPipScopes", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        private void Awake()
        {
            // save the Logger to public static field so we can use it elsewhere in the project
            LogSource = Logger;
            LogSource.LogInfo("NonPipScopes!");

            // uncomment line(s) below to enable desired example patch, then press F6 to build the project
            // if this solution is properly placed in a YourSPTInstall/Development folder, the compiled plugin will automatically be copied into YourSPTInstall/BepInEx/plugins
            // new SimplePatch().Enable();
        }
    }
}
