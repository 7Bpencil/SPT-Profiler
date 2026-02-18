using Diz.Jobs;
using GPUInstancer;
using Audio.SpatialSystem;
using Audio.AmbientSubsystem;
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
				new Measure_GameWorld_DoWorldTick(),
				new Measure_GameWorld_DoOtherWorldTick(),
				new Measure_LateUpdate<GameWorldUnityTickListener>(),
				new Measure_Update<BetterAudio>(),
				new Measure_Update<SpatialAudioSystem>(),
				new Measure_LateUpdate<SpatialAudioSystem>(),
				new Measure_Update<AmbientAudioSystem>(),
				new Measure_LateUpdate<AmbientAudioSystem>(),
				new Measure_Update<GPUInstancerManager>(),
				new Measure_LateUpdate<JobScheduler>(),
				new Measure_Update<CullingManager>(),
			];

			foreach (var profiler in Profilers)
			{
				profiler.Init();
			}
        }

		private const float windowWidth = 400f;
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

		public static double GetDurationSeconds(long durationTicks)
		{
			return (double)durationTicks / Stopwatch.Frequency;
		}

		public static double GetDurationMilliseconds(long durationTicks)
		{
			return GetDurationSeconds(durationTicks) * 1000;
		}

		public static long GetTicksFromMilliseconds(uint milliseconds)
		{
			return (milliseconds * Stopwatch.Frequency) / 1000;
		}

        private void WindowFunction(int TWCWindowID)
		{
			var x = startX;
			var y = startY;
			var currentTime = Stopwatch.GetTimestamp();
			var validTime = GetTicksFromMilliseconds(100);
			foreach (var profiler in Profilers)
			{
				var name = profiler.GetName();
				var (duration, instances) = profiler.GetDuration(currentTime, validTime);
				var timeMs = GetDurationMilliseconds(duration);
				var text = $"{timeMs:0.0000} ms | {instances} | {name}";
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
		public (long sumDuration, long instances) GetDuration(long time, long validTime);
	}

	public class InstanceData
	{
		public bool IsRunning;
		public long Start;
		public long End;
	}

	// V type arg is used to generate different classes with same T type
	// otherwise T.Method0 and T.Method1 will overwrite each other stopwatches
	public abstract class MethodProfiler<T, V> : ModulePatch, IProfiler
	{
		private readonly string _methodName;
		private readonly string _fullName;

		private static Dictionary<T, InstanceData> _instancesData = new();

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

		public (long sumDuration, long instances) GetDuration(long time, long validTime)
		{
			long sum = 0;
			long count = 0;
			foreach (var instanceData in _instancesData.Values)
			{
				if (!instanceData.IsRunning && time - instanceData.End <= validTime)
				{
					sum += instanceData.End - instanceData.Start;
					count += 1;
				}
			}
			return (sum, count);
		}

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(T), _methodName);
        }

		public static bool StartMeasure(T instance)
		{
			var instanceData = GetInstanceData(instance);
			instanceData.IsRunning = true;
			instanceData.Start = Stopwatch.GetTimestamp();
			return true;
		}

		public static void StopMeasure(T instance)
		{
			var instanceData = GetInstanceData(instance);
			instanceData.IsRunning = false;
			instanceData.End = Stopwatch.GetTimestamp();
		}

		public static InstanceData GetInstanceData(T instance)
		{
			if (_instancesData.TryGetValue(instance, out var instanceData))
			{
				return instanceData;
			}

			var newInstanceData = new InstanceData();
			_instancesData.Add(instance, newInstanceData);

			return newInstanceData;
		}
	}

	public class Measure_Update<T> : MethodProfiler<T, int>
	{
		public Measure_Update() : base("Update") { }
        [PatchPrefix] public static bool Prefix(T __instance) { return StartMeasure(__instance); }
        [PatchPostfix] public static void Postfix(T __instance) { StopMeasure(__instance); }
	}

	public class Measure_LateUpdate<T> : MethodProfiler<T, bool>
	{
		public Measure_LateUpdate() : base("LateUpdate") { }
        [PatchPrefix] public static bool Prefix(T __instance) { return StartMeasure(__instance); }
        [PatchPostfix] public static void Postfix(T __instance) { StopMeasure(__instance); }
	}

	public class Measure_GameWorld_DoWorldTick : MethodProfiler<GameWorld, int>
	{
		public Measure_GameWorld_DoWorldTick() : base(nameof(GameWorld.DoWorldTick)) { }
        [PatchPrefix] public static bool Prefix(GameWorld __instance, float dt) { return StartMeasure(__instance); }
        [PatchPostfix] public static void Postfix(GameWorld __instance, float dt) { StopMeasure(__instance); }
	}

	public class Measure_GameWorld_DoOtherWorldTick : MethodProfiler<GameWorld, bool>
	{
		public Measure_GameWorld_DoOtherWorldTick() : base(nameof(GameWorld.DoOtherWorldTick)) { }
        [PatchPrefix] public static bool Prefix(GameWorld __instance, float dt) { return StartMeasure(__instance); }
        [PatchPostfix] public static void Postfix(GameWorld __instance, float dt) { StopMeasure(__instance); }
	}

}
