using Comfort.Common;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace SevenBoldPencil.Profiler
{
	public interface IProfiler
	{
		public void Init();
		public string GetName();
		public (long totalDuration, long calls) CollectMeasurements();
	}

	public class InstanceData
	{
		public List<RunningMethod> RunningMethods = new(1);
		public long TotalDuration;
		public long Calls;
	}

	public struct RunningMethod
	{
		public long Start;
		public int ThreadId;
	}

	// V type arg is used to generate different classes with same T type
	// otherwise methods T.MethodA and T.MethodB will overwrite each other stopwatches
	public abstract class MethodProfiler<T, V> : ModulePatch, IProfiler
	{
		private static string _methodName;
		private static string _fullName;
		private static bool _isStaticMethod;
		private static InstanceData _staticInstanceData;
		private static Dictionary<T, InstanceData> _instancesData;

		public MethodProfiler(string methodName)
		{
			_methodName = methodName;
			_fullName = $"{typeof(T).Name}.{methodName}";
		}

        protected override MethodBase GetTargetMethod()
        {
			var targetMethod = AccessTools.Method(typeof(T), _methodName);
			_isStaticMethod = targetMethod.IsStatic;

			if (_isStaticMethod)
			{
				_staticInstanceData = new();
			}
			else
			{
				_instancesData = new();
			}

            return targetMethod;
        }

		public void Init()
		{
			Enable();
		}

		public string GetName()
		{
			return _fullName;
		}

		public (long totalDuration, long calls) CollectMeasurements()
		{
			long totalDuration = 0;
			long calls = 0;

			if (_isStaticMethod)
			{
				ProcessInstance(_staticInstanceData, ref totalDuration, ref calls);
			}
			else
			{
				foreach (var instanceData in _instancesData.Values)
				{
					ProcessInstance(instanceData, ref totalDuration, ref calls);
				}
			}

			return (totalDuration, calls);
		}

		private static void ProcessInstance(InstanceData instanceData, ref long totalDuration, ref long calls)
		{
			totalDuration += instanceData.TotalDuration;
			calls += instanceData.Calls;

			instanceData.TotalDuration = 0;
			instanceData.Calls = 0;
		}

		public static bool StartMeasure(T instance)
		{
			if (!IsCorrectType(instance))
			{
				return true;
			}

			var instanceData = GetInstanceData(instance);
			var runningMethods = instanceData.RunningMethods;
			var threadId = Thread.CurrentThread.ManagedThreadId;

			runningMethods.Add(new RunningMethod()
			{
				Start = Stopwatch.GetTimestamp(),
				ThreadId = threadId,
			});

			return true;
		}

		public static void StopMeasure(T instance)
		{
			if (!IsCorrectType(instance))
			{
				return;
			}

			var instanceData = GetInstanceData(instance);
			var runningMethods = instanceData.RunningMethods;
			var threadId = Thread.CurrentThread.ManagedThreadId;

			for (var i = 0; i < runningMethods.Count; i++)
			{
				var runningMethod = runningMethods[i];
				if (runningMethod.ThreadId == threadId)
				{
					var runningMethodEnd = Stopwatch.GetTimestamp();
					instanceData.TotalDuration += runningMethodEnd - runningMethod.Start;
					instanceData.Calls += 1;
					SwapRemove(runningMethods, i);
					break;
				}
			}
		}

	    public static void SwapRemove<R>(List<R> list, int index)
	    {
			var lastIndex = list.Count - 1;
			if (index != lastIndex)
			{
		        list[index] = list[lastIndex];
			}
	        list.RemoveAt(lastIndex);
	    }

		// if we have class A with virtual method
		// and class B that inherits A,
		// then instance of B will be tracked twice.
		// this detects when tracker A receives instance of B
		public static bool IsCorrectType(T instance)
		{
			return _isStaticMethod || instance.GetType() == typeof(T);
		}

		public static InstanceData GetInstanceData(T instance)
		{
			if (_isStaticMethod)
			{
				return _staticInstanceData;
			}
			if (_instancesData.TryGetValue(instance, out var instanceData))
			{
				return instanceData;
			}

			var newInstanceData = new InstanceData();
			_instancesData.Add(instance, newInstanceData);

			return newInstanceData;
		}
	}
}
