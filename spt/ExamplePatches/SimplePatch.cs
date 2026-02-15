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
using UnityEngine.Rendering;
using System.Text;
using System.Collections.Generic;
using static EFT.Player;

namespace NonPipScopes.ExamplePatches {
    public static class Helpers {
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
        private static TypedFieldInfo<Player, float> _ribcageScaleCompensated;

        protected override MethodBase GetTargetMethod()
        {
            _ribcageScaleCompensated = new TypedFieldInfo<Player, float>("_ribcageScaleCompensated");
            return AccessTools.Method(typeof(Player), nameof(Player.CalculateScaleValueByFov));
        }

        [PatchPrefix]
        public static bool Prefix(Player __instance, ref float fov)
        {
            var scale = 1f;

            if (Plugin.Instance.FovDataOption.Some(out var fovData) && fovData.Zoom != 1f) {
                // scale = fovData.Zoom;
            }

            _ribcageScaleCompensated.Set(__instance, scale);

            return false;
        }
    }

    // reset camera zoom on weapon change and scope switch
    public class Patch_PWA_method_23 : ModulePatch
    {
        private static TypedFieldInfo<FirearmController, Player> _playerField;
        private static TypedFieldInfo<ProceduralWeaponAnimation, FirearmController> _fcField;

        protected override MethodBase GetTargetMethod()
        {
            _playerField = new TypedFieldInfo<FirearmController, Player>("_player");
            _fcField = new TypedFieldInfo<ProceduralWeaponAnimation, FirearmController>("_firearmController");
            return AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.method_23));
        }

        [PatchPostfix]
        private static void PatchPostfix(ProceduralWeaponAnimation __instance)
        {
            var firearmController = _fcField.Get(__instance);
            if (!firearmController) {
                return;
            }

            var player = _playerField.Get(firearmController);
            if (player && player.IsYourPlayer)
            {
                ChangeMainCamFOV(player);
            }
        }

        public static void ChangeMainCamFOV(Player player)
        {
            var camera = CameraClass.Instance.Camera;

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

                    // var weaponRoot = firearmController.ControllerGameObject;
                    // var handsRoot = player.PlayerBody.BodySkins[EBodyModelPart.Hands];

                    var opticCamera = CameraClass.Instance.OpticCameraManager.Camera;
                    opticCamera.gameObject.SetActive(false);

                    var scope = pwa.CurrentScope.ScopePrefabCache;
                    var opticSight = scope.CurrentModOpticSight;
                    var backLensTransform = opticSight.transform.FindChild("backLens");
                    var backLensMaskTransform = opticSight.transform.FindChild("backLensMask");
                    if (backLensTransform && !backLensMaskTransform) {
                        var backLensMask = new GameObject("backLensMask", typeof(MeshFilter), typeof(MeshRenderer), typeof(BackLensMask));
                        backLensMask.GetComponent<BackLensMask>().Init(backLensTransform);
                        backLensTransform.gameObject.SetActive(false);
                    }

                    Logger.LogWarning($"AssaultScope zoom: {zoom}");
                }
            }

            var resultFov = baseFov;
            if (zoom != 1) {
                var baseFovRad = baseFov * Mathf.Deg2Rad;
                var resultFovRad = 2 * Mathf.Atan2(Mathf.Tan(baseFovRad * 0.5f), zoom);
                resultFov = resultFovRad * Mathf.Rad2Deg;
            }

            Plugin.Instance.FovDataOption = new Option<FovData>(new FovData() {
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
                camera.clearFlags = CameraClearFlags.Nothing;

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
            return AccessTools.Method(typeof(OpticSight), nameof(OpticSight.LensFade));
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
