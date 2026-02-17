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
}
