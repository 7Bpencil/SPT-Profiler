using BepInEx;
using BepInEx.Logging;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using NonPipScopes.ExamplePatches;

namespace NonPipScopes {
    [BepInPlugin("7Bpencil.NonPipScopes", "NonPipScopes", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        public static Plugin Instance;

        public FovManager FovManager;
    	public Shader DepthOnlyShader;
		public Coroutine ChangeFovCoroutine;

        private void Awake() {
            Instance = this;
            FovManager = new FovManager();

            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assetDirPath = Path.Combine(assemblyDir, "assets");
            var bundleDirPath = Path.Combine(assetDirPath, "bundles");
            var bundlePath = Path.Combine(bundleDirPath, "non_pip_scopes.bundle");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            DepthOnlyShader = bundle.LoadAsset<Shader>("Assets/NonPipScopes/Shaders/DepthOnly.shader");

            new Patch_Player_CalculateScaleValueByFov().Enable();
            new Patch_PwaWeaponParamsPatch().Enable();
        }

        private void Update() {
            FovManager.Run();
        }

        public void ChangeFov(Camera camera, float targetFov, float time) {
            if (ChangeFovCoroutine != null) {
                StopCoroutine(ChangeFovCoroutine);
            }
            ChangeFovCoroutine = StartCoroutine(method_5(camera, targetFov, time));
        }

        // Same tweening bsg uses for main camera
    	public IEnumerator method_5(Camera camera, float targetFov, float time)
    	{
    		float timeLeft = 1f;
    		while (timeLeft > 0f && camera)
    		{
    			camera.fieldOfView = Mathf.Lerp(targetFov, camera.fieldOfView, timeLeft);
    			timeLeft -= Time.deltaTime / time;
    			yield return null;
    		}
    		if (camera)
    		{
    			camera.fieldOfView = targetFov;
    		}
            ChangeFovCoroutine = null;
    		yield break;
    	}

    }
}
