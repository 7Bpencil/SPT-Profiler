using BepInEx;
using BepInEx.Logging;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using NonPipScopes.ExamplePatches;
using EFT;
using EFT.InventoryLogic;
using EFT.Animations;
using EFT.CameraControl;
using EFT.UI;
using EFT.UI.Settings;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using Comfort.Common;
using System.Text;
using System.Collections.Generic;
using static EFT.Player;

namespace NonPipScopes {
    [BepInPlugin("7Bpencil.NonPipScopes", "NonPipScopes", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        public static Plugin Instance;

		public ManualLogSource LoggerInstance;

		public IProfiler[] Profilers;


        private void Awake() {
            Instance = this;

			LoggerInstance = Logger;

			Profilers = [
				new Patch_GameWorld_DoWorldTick(),
				new Patch_GameWorld_DoOtherWorldTick(),
			];

			foreach (var profiler in Profilers)
			{
				profiler.Init();
			}
        }

		private const float windowWidth = 300f;
		private const float headerHeight = 15f;
		private const float startX = 15f;
		private const float startY = headerHeight + 15f + separatorY;
		private const float height = 20f;
		private const float separatorY = 2.5f;

		public void OnGUI()
        {
			var windowHeight = startY + Profilers.Length * (height + separatorY) - separatorY + headerHeight;
            GUI.Window(0, new Rect(50, 50, windowWidth, windowHeight), WindowFunction, "7Bpencil Profiler");
        }

        private void WindowFunction(int TWCWindowID)
		{
			var x = startX;
			var y = startY;
			foreach (var profiler in Profilers)
			{
				var name = profiler.GetName();
				var timeMs = profiler.GetTimeMs();
				var text = $"{timeMs} ms | {name}";
				var rect = new Rect(x, y, windowWidth, height);
				GUI.Label(rect, text);
				y += height + separatorY;
			}
		}
    }

	public interface IProfiler
	{
		public void Init();
		public string GetName();
		public double GetTimeMs();
	}

	public abstract class MethodProfiler<T> : ModulePatch, IProfiler
	{
		private readonly string _methodName;
		private readonly string _fullName;

		private static Stopwatch _stopwatch = new Stopwatch();
		private static double _elapsed;

		public MethodProfiler(string methodName)
		{
			_methodName = methodName;
			_fullName = $"{typeof(T).Name}.{methodName}";
		}

		public void Init()
		{
			Enable();
		}

		public string GetName()
		{
			return _fullName;
		}

		public double GetTimeMs()
		{
			return _elapsed;
		}

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(T), _methodName);
        }

		public static bool StartMeasure()
		{
			_stopwatch.Restart();
			return true;
		}

		public static void StopMeasure()
		{
			_stopwatch.Stop();
			_elapsed = _stopwatch.Elapsed.TotalMilliseconds;
		}
	}

	public class Patch_GameWorld_DoWorldTick : MethodProfiler<GameWorld>
	{
		public Patch_GameWorld_DoWorldTick() : base(nameof(GameWorld.DoWorldTick)) { }

        [PatchPrefix]
        public static bool Prefix(GameWorld __instance, float dt)
        {
			return StartMeasure();
        }

        [PatchPostfix]
        public static void Postfix(GameWorld __instance, float dt)
        {
			StopMeasure();
        }
	}

	public class Patch_GameWorld_DoOtherWorldTick : MethodProfiler<GameWorld>
	{
		public Patch_GameWorld_DoOtherWorldTick() : base(nameof(GameWorld.DoOtherWorldTick)) { }

        [PatchPrefix]
        public static bool Prefix(GameWorld __instance, float dt)
        {
			return StartMeasure();
        }

        [PatchPostfix]
        public static void Postfix(GameWorld __instance, float dt)
        {
			StopMeasure();
        }
	}
}
