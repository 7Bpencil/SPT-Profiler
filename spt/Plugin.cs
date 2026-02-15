using BepInEx;
using BepInEx.Logging;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using NonPipScopes.ExamplePatches;

namespace NonPipScopes {
	public struct FovData {
		public float BaseFOV;
		public float Zoom;
		public float ResultFOV;
	}

    [BepInPlugin("7Bpencil.NonPipScopes", "NonPipScopes", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        public static Plugin Instance;

		public ManualLogSource LoggerInstance;
		public Option<FovData> FovDataOption;
    	public Shader DepthOnlyShader;
    	public Shader OpticSightShader;
		public Coroutine ChangeFovCoroutine;
		public Camera WorldCamera;

        private void Awake() {
            Instance = this;

			LoggerInstance = Logger;

            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var bundlePath = Path.Combine(assemblyDir, "assets", "bundles", "non_pip_scopes");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            DepthOnlyShader = bundle.LoadAsset<Shader>("Assets/NonPipScopes/Shaders/DepthOnly.shader");
            OpticSightShader = bundle.LoadAsset<Shader>("Assets/NonPipScopes/ShadersDecompiled/OpticSight.shader");

            new Patch_Player_CalculateScaleValueByFov().Enable();
            new Patch_PWA_method_23().Enable();
			new Patch_OpticSight_LensFade().Enable();
        }

        public void ChangeWorldCameraFov(float targetFov, float time) {
            if (ChangeFovCoroutine != null) {
                StopCoroutine(ChangeFovCoroutine);
            }
            if (WorldCamera) {
                ChangeFovCoroutine = StartCoroutine(method_5(WorldCamera, targetFov, time));
            }
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
