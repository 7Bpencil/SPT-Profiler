using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Diagnostics;
using UnityEngine;
using System.Collections.Generic;

namespace SevenBoldPencil.Profiler {
    [BepInPlugin("7Bpencil.Profiler", "Profiler", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
		private struct Measurement
		{
			public string Name;
			public double TimeMs;
			public long Instances;
		}

        public static Plugin Instance;

        public static ConfigEntry<KeyboardShortcut> HideShowWindow;
        public static ConfigEntry<KeyboardShortcut> PauseContinueRecording;

		public ManualLogSource LoggerInstance;
		private List<IProfiler> profilers;
		private List<Measurement> measurements;
		private double measurementsTotalMs;
		private bool isProfilerWindowVisible;
		private bool isProfilerRecording;
		private Rect windowRect;

		private const int maxVisibleAmount = 25;
		private const float windowWidth = 400f;
		private const float headerHeight = 15f;
		private const float startX = 15f;
		private const float startY = headerHeight + 15f + separatorY;
		private const float height = 20f;
		private const float separatorY = 2.5f;

        private void Awake() {
			var section = "Main";
            HideShowWindow = Config.Bind(section, "Hide/Show Window", new KeyboardShortcut(KeyCode.F4), "Hide/Show Window");
            PauseContinueRecording = Config.Bind(section, "Pause/Continue Recording", new KeyboardShortcut(KeyCode.F5), "Pause/Continue Recording");

			isProfilerWindowVisible = true;
			isProfilerRecording = true;

            Instance = this;
			LoggerInstance = Logger;

			// SourceGenerator.Generate();
			// profilers = new List<IProfiler>(0);

			profilers = Generated.GetProfilers(0);

			measurements = new List<Measurement>(profilers.Count);

			foreach (var profiler in profilers)
			{
				profiler.Init();
			}
        }

		public void Update()
		{
			if (Input.GetKeyDown(HideShowWindow.Value.MainKey))
			{
				isProfilerWindowVisible = !isProfilerWindowVisible;
			}
			if (Input.GetKeyDown(PauseContinueRecording.Value.MainKey))
			{
				isProfilerRecording = !isProfilerRecording;
			}
		}

		public void OnGUI()
        {
			if (isProfilerWindowVisible)
			{
				if (isProfilerRecording)
				{
					CollectMeasurements();
				}

				var visibleAmount = GetVisibleMeasurementsCount();
				var windowHeight = startY + visibleAmount * (height + separatorY) - separatorY + headerHeight;

				// if width == 0, then windowRect has not been initialized, so init it
				if (windowRect.width == 0f)
				{
					windowRect.width = windowWidth;
					windowRect.x = 50;
					windowRect.y = 50;
				}

				windowRect.height = windowHeight;
	            windowRect = GUI.Window(0, windowRect, WindowFunction, $"7Bpencil Profiler | {measurementsTotalMs:0.0000} ms");
			}
        }

		private void CollectMeasurements()
		{
			var currentTime = Stopwatch.GetTimestamp();
			var validTime = GetTicksFromMilliseconds(100);

			double total = 0;
			measurements.Clear();
			foreach (var profiler in profilers)
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

		private int GetVisibleMeasurementsCount()
		{
			return Mathf.Min(measurements.Count, maxVisibleAmount);
		}

        private void WindowFunction(int windowID)
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

			GUI.DragWindow();
		}

    }
}
