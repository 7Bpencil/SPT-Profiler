using BepInEx;

namespace SevenBoldPencil.Profiler {
    [BepInPlugin("7Bpencil.Profiler.SourceGenerator", "7Bpencil.Profiler.SourceGenerator", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        private void Awake() {
			SourceGenerator.Generate();
        }
    }
}
