using EFT;
using EFT.InventoryLogic;
using EFT.Animations;
using EFT.CameraControl;
using EFT.UI;
using EFT.UI.Settings;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Reflection;
using Comfort.Common;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using static EFT.Player;

namespace NonPipScopes.ExamplePatches {
    public struct PlayerStatus {
        public Option<Player> PlayerOption;
        public bool IsWeaponReady;
        public bool IsInHideout;
    }

    public static class Helpers {
        public static string CompactCollimator = "55818acf4bdc2dde698b456b";
        public static string Collimator = "55818ad54bdc2ddc698b4569";
        public static string AssaultScope = "55818add4bdc2d5b648b456f";
        public static string OpticScope = "55818ae44bdc2dde698b456c";
        public static string IronSight = "55818ac54bdc2d5b648b456e";
        public static string SpecialScope = "55818aeb4bdc2ddc698b456a";
        public static string[] Scopes = new string[] { CompactCollimator, Collimator, AssaultScope, OpticScope, IronSight, SpecialScope };

        public static bool IsScope(Mod mod) {
            foreach (string scopeTypeId in Scopes) {
                if (mod.GetType() == TemplateIdToObjectMappingsClass.TypeTable[scopeTypeId]) {
                    return true;
                }
            }

            return false;
        }

        public static PlayerStatus GetPlayerStatus() {
            var gameWorld = Singleton<GameWorld>.Instance;
            if (!gameWorld) {
                return new PlayerStatus() {
                    PlayerOption = default,
                    IsWeaponReady = false,
                    IsInHideout = false,
                };
            }

            var player = gameWorld.MainPlayer;
            if (!player) {
                return new PlayerStatus() {
                    PlayerOption = default,
                    IsWeaponReady = false,
                    IsInHideout = false,
                };
            }

            return new PlayerStatus() {
                PlayerOption = new Option<Player>(player),
                IsWeaponReady = player.HandsController && player.HandsController.Item != null && player.HandsController.Item is Weapon,
                IsInHideout = player is HideoutPlayer,
            };
        }

        public static string GetGameObjectPath(GameObject obj)
        {
            StringBuilder pathBuilder = new StringBuilder();
            Transform current = obj.transform;

            while (current)
            {
                if (pathBuilder.Length == 0)
                {
                    pathBuilder.Append(current.name);
                }
                else
                {
                    pathBuilder.Append("/");
                    pathBuilder.Append(current.name);
                }

                current = current.parent;
            }

            return pathBuilder.ToString();
        }

        public static List<string> GetAllLayers()
        {
            var result = new List<string>(32);
            for (var i = 0; i < 32; i++)
            {
                result.Add(LayerMask.LayerToName(i));
            }
            return result;
        }

        public static int AddLayer(int layerMask, int layer) {
            return layerMask | 1 << layer;
        }

        public static int RemoveLayer(int layerMask, int layer) {
            return layerMask & ~(1 << layer);
        }

        public static int AddLayerByName(int layerMask, string layerName, List<string> layers) {
            var layer = layers.IndexOf(layerName);
            return AddLayer(layerMask, layer);
        }

        public static int RemoveLayerByName(int layerMask, string layerName, List<string> layers) {
            var layer = layers.IndexOf(layerName);
            return RemoveLayer(layerMask, layer);
        }

        public static bool LayerMaskContains(int layerMask, int layer) {
            return (layerMask & (1 << layer)) != 0;
        }

        public static void PrintLayerMask(int layerMask, List<string> layers) {
            for (var i = 0; i < 32; i++) {
                if (LayerMaskContains(layerMask, i)) {
                    Console.WriteLine(layers[i]);
                }
            }
        }
    }

    // changes "HUD FOV", or how the player model is rendered
    public class Patch_Player_CalculateScaleValueByFov : ModulePatch
    {
        private static FieldInfo _ribcageScaleCompensated;

        protected override MethodBase GetTargetMethod()
        {
            _ribcageScaleCompensated = AccessTools.Field(typeof(Player), "_ribcageScaleCompensated");
            return typeof(Player).GetMethod("CalculateScaleValueByFov");
        }

        [PatchPrefix]
        public static bool Prefix(Player __instance, ref float fov)
        {
            var scale = 1f;

            var fovManager = Plugin.Instance.FovManager;
            if (fovManager.FovDataOption.Some(out var fovData) && fovData.Zoom != 1f) {
                // scale = fovData.Zoom;
            }

            _ribcageScaleCompensated.SetValue(__instance, scale);

            return false;
        }
    }

    // reset camera zoom on weapon change and scope switch
    public class Patch_PwaWeaponParamsPatch : ModulePatch
    {
        private static FieldInfo _playerField;
        private static FieldInfo _fcField;

        protected override MethodBase GetTargetMethod()
        {
            _playerField = AccessTools.Field(typeof(FirearmController), "_player");
            _fcField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmController");
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("method_23", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            var firearmController = (FirearmController)_fcField.GetValue(__instance);
            if (!firearmController) {
                return;
            }

            var player = (Player)_playerField.GetValue(firearmController);
            if (player && player.IsYourPlayer)
            {
                ChangeMainCamFOV(player);
            }
        }

        public static void ChangeMainCamFOV(Player player)
        {
            var fovManager = Plugin.Instance.FovManager;

            var pwa = player.ProceduralWeaponAnimation;
            var firearmController = player.HandsController as FirearmController;

            float baseFov = pwa.Single_2;
            float zoom = 1f;

            if (firearmController
                && pwa.PointOfView == EPointOfView.FirstPerson
                && !pwa.Sprint
                && pwa.IsAiming)
            {
                var sight = pwa.CurrentAimingMod;
                var data = sight.AdjustableOpticData;
                var scopeIndex = sight.SelectedScope;
                var scopeModeIndex = sight.SelectedScopeMode;

                if (data is CollimatorTemplateClass) {
                    Logger.LogWarning($"CollimatorTemplateClass");
                }
                if (data is CompactCollimatorTemplateClass) {
                    Logger.LogWarning($"CompactCollimator");
                }
                if (data is OpticScopeTemplateClass opticScope) {
                    // var zooms = opticScope.Zooms[scopeIndex];
                    // zoom = zooms[scopeModeIndex];
                    Logger.LogWarning($"OpticScope");
                }
                if (data is AssaultScopeTemplateClass assaultScope) {
                    var zooms = assaultScope.Zooms[scopeIndex];
                    zoom = zooms[scopeModeIndex];

                    // we set lens and backLens materials to depth only shader,
                    // and render them before other scope meshes,
                    // this way we clip out inner mesh of the scope from view
                    var scope = pwa.CurrentScope.ScopePrefabCache;
                    var scopeMainRenderQueue = 5000;
                    var scopeLensRenderQueue = scopeMainRenderQueue - 1;

                    // TODO maybe can compare by shaders directly, not by names?
                    // meshRenderer.material.shader != depthOnlyShader
                    var depthOnlyShader = Plugin.Instance.DepthOnlyShader;
                    var weaponRoot = firearmController.ControllerGameObject;
                    foreach (var meshRenderer in weaponRoot.GetComponentsInChildren<MeshRenderer>()) {
                        if (meshRenderer.material.shader.name != depthOnlyShader.name) {
                            meshRenderer.material.renderQueue = scopeMainRenderQueue;
                        }
                    }
                    var opticCameraManager = CameraClass.Instance.OpticCameraManager;
                    var opticCamera = opticCameraManager.Camera;
                    opticCamera.cullingMask = 0;
                    opticCamera.clearFlags = CameraClearFlags.SolidColor;
                    opticCamera.backgroundColor = Color.clear;

                    var opticSight = scope.CurrentModOpticSight;
                    var opticSightLensRenderer = opticSight.LensRenderer;
                    if (opticSightLensRenderer.material.shader.name != depthOnlyShader.name) {
                        opticSightLensRenderer.material = new Material(depthOnlyShader);
                        opticSightLensRenderer.material.renderQueue = scopeLensRenderQueue;
                    }

                    var backLens = opticSight.transform.FindChild("backLens");
                    if (backLens) {
                        var backLensRenderer = backLens.GetComponent<MeshRenderer>();
                        if (backLensRenderer.material.shader.name != depthOnlyShader.name) {
                            backLensRenderer.material = new Material(depthOnlyShader);
                            backLensRenderer.material.renderQueue = scopeLensRenderQueue;
                        }
                    }

                    Logger.LogWarning($"AssaultScope zoom: {zoom}");
                }
            }

            var camera = CameraClass.Instance.Camera;

            var resultFov = baseFov;
            if (zoom != 1) {
                var baseFovRad = baseFov * Mathf.Deg2Rad;
                var near = camera.nearClipPlane;
                var height = 2 * near * Mathf.Tan(baseFovRad * 0.5f);
                var resultFovRad = 2 * Mathf.Atan2(height, 2 * zoom * near);
                resultFov = resultFovRad * Mathf.Rad2Deg;
            }

            fovManager.FovDataOption = new Option<FovData>(new FovData() {
                BaseFOV = baseFov,
                Zoom = zoom,
                ResultFOV = resultFov,
            });

            CameraClass.Instance.SetFov(baseFov, 1f, !pwa.IsAiming);
            player.CalculateScaleValueByFov(CameraClass.Instance.Fov);
            player.SetCompensationScale(false);

            var cullingMask = camera.cullingMask;
            var playerLayer = LayerMask.NameToLayer("Player");
            var onlyPlayerLayerMask = Helpers.AddLayer(0, playerLayer);

            if (cullingMask != onlyPlayerLayerMask) {
                var clearFlags = camera.clearFlags;

                camera.cullingMask = onlyPlayerLayerMask;
                camera.clearFlags = CameraClearFlags.Depth;

                var worldCameraGO = new GameObject("WorldCamera", typeof(Camera));

                var worldCamera = worldCameraGO.GetComponent<Camera>();
                worldCamera.allowMSAA = camera.allowMSAA;
                worldCamera.cullingMask = Helpers.RemoveLayer(cullingMask, playerLayer);
                worldCamera.nearClipPlane = camera.nearClipPlane;
                worldCamera.fieldOfView = baseFov;
                worldCamera.depth = camera.depth - 1;
                worldCamera.clearFlags = clearFlags;
                worldCamera.depthTextureMode = camera.depthTextureMode;
                worldCamera.eventMask = camera.eventMask;

                var worldCameraTrasform = worldCameraGO.transform;
                worldCameraTrasform.SetParent(camera.transform);
                worldCameraTrasform.localPosition = Vector3.zero;
                worldCameraTrasform.localRotation = Quaternion.identity;

                Plugin.Instance.WorldCamera = worldCamera;
            }

            Plugin.Instance.ChangeWorldCameraFov(resultFov, 1);
        }
    }

    public class Patch_OpticSight_LensFade : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(OpticSight).GetMethod("LensFade", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref OpticSight __instance, bool isHide = true)
        {
            var lensRenderer = __instance.LensRenderer;
            var opticSightShader = Plugin.Instance.OpticSightShader;
            if (lensRenderer.material.shader.name != opticSightShader.name)
            {
                // I am pretty sure that's the earliest time LensRenderer is used, so we swap its material here

                Logger.LogWarning("Patch_OpticSight_LensFade");
                var newMaterial = new Material(opticSightShader);
                newMaterial.CopyPropertiesFromMaterial(lensRenderer.material);

                lensRenderer.material = newMaterial;
            }

            return true;
        }
    }
}
