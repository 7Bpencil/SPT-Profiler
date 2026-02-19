using AbsolutDecals;
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
using System.Linq;
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
using SevenBoldPencil.Profiler;
using static EFT.Player;

namespace NonPipScopes {
    [BepInPlugin("7Bpencil.NonPipScopes", "NonPipScopes", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
		private struct Measurement
		{
			public string Name;
			public double TimeMs;
			public long Instances;
		}

        public static Plugin Instance;

		public ManualLogSource LoggerInstance;
		public List<IProfiler> Profilers;
		private List<Measurement> measurements;
		private double measurementsTotalMs;

        private void Awake() {
            Instance = this;
			LoggerInstance = Logger;

			// SourceGenerator.Generate();
			// Profilers = new List<IProfiler>(0);

			Profilers = Generated.GetProfilers(0);

			measurements = new List<Measurement>(Profilers.Count);

			foreach (var profiler in Profilers)
			{
				profiler.Init();
			}
        }

		private void CollectMeasurements()
		{
			var currentTime = Stopwatch.GetTimestamp();
			var validTime = GetTicksFromMilliseconds(100);

			double total = 0;
			measurements.Clear();
			foreach (var profiler in Profilers)
			{
				var name = profiler.GetName();
				var (duration, instances) = profiler.GetDuration(currentTime, validTime);
				if (duration == 0)
				{
					continue;
				}

				var timeMs = GetDurationMilliseconds(duration);
				measurements.Add(new Measurement()
				{
					Name = name,
					TimeMs = timeMs,
					Instances = instances,
				});
				total += timeMs;
			}
			measurements.Sort((a, b) => b.TimeMs.CompareTo(a.TimeMs));
			measurementsTotalMs = total;
		}

		private int GetVisibleMeasurementsCount()
		{
			return Mathf.Min(measurements.Count, maxVisibleAmount);
		}

		private const int maxVisibleAmount = 20;
		private const float windowWidth = 400f;
		private const float headerHeight = 15f;
		private const float startX = 15f;
		private const float startY = headerHeight + 15f + separatorY;
		private const float height = 20f;
		private const float separatorY = 2.5f;

		public void OnGUI()
        {
			// TODO add settings to hide/show window, stop/resume updates, make window draggable
			CollectMeasurements();
			var visibleAmount = GetVisibleMeasurementsCount();
			var windowHeight = startY + visibleAmount * (height + separatorY) - separatorY + headerHeight;
            GUI.Window(0, new Rect(50, 50, windowWidth, windowHeight), WindowFunction, $"7Bpencil Profiler | {measurementsTotalMs:0.0000} ms");
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

			var visibleAmount = GetVisibleMeasurementsCount();
			for (var i = 0; i < visibleAmount; i++)
			{
				var measurement = measurements[i];
				var text = $"{measurement.TimeMs:0.0000} ms | {measurement.Instances} | {measurement.Name}";
				var rect = new Rect(x, y, windowWidth, height);
				GUI.Label(rect, text);
				y += height + separatorY;
			}
		}
    }
}
