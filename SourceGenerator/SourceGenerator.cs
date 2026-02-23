using Comfort.Common;
using SPT.Reflection.Patching;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using EFT;

namespace SevenBoldPencil.Profiler
{
	public static class SourceGenerator
	{
		public static void Generate()
		{
			var groups = new List<ProfilersGroupDescription>()
			{
				new ProfilersGroupDescription()
				{
					Name = "MonoBehaviour",
					Profilers = GetMonoBehaviourProfilers()
				},
				new ProfilersGroupDescription()
				{
					Name = "AI",
					Profilers = new List<ProfilerDescription>()
					{
						new ProfilerDescription(typeof(BotsController), nameof(BotsController.method_0)),
						new ProfilerDescription(typeof(AICoreControllerClass), nameof(AICoreControllerClass.Update)),
						new ProfilerDescription(typeof(AITaskManager), nameof(AITaskManager.Update)),
						new ProfilerDescription(typeof(BotsClass), nameof(BotsClass.UpdateByUnity)),
					}
				},
				new ProfilersGroupDescription()
				{
					Name = "Physics",
					Profilers = new List<ProfilerDescription>()
					{
						new ProfilerDescription(typeof(EFTPhysicsClass.SyncTransformsClass), nameof(EFTPhysicsClass.SyncTransformsClass.Update)),
						new ProfilerDescription(typeof(EFTPhysicsClass.GClass745), nameof(EFTPhysicsClass.GClass745.Update)),
					}
				},
			};
			GenerateSourceCode(groups);
		}

		public struct ProfilersGroupDescription
		{
			public string Name;
			public List<ProfilerDescription> Profilers;
		}

		public struct ProfilerDescription
		{
			public string TypeName;
			public string MethodName;
			public string MethodSignature;
			public string ProfilerName;

			public ProfilerDescription(Type type, string methodName, string methodSignature = null)
			{
				// if class is nested, its name is returned as OuterClass+Class,
				// in that case convert it to OuterClass.Class
				var typeName = type.FullName.Replace("+", ".");
				var typeNameWithoutDots = typeName.Replace(".", "_");
				var profilerName = $"{typeNameWithoutDots}_{methodName}";

				TypeName = typeName;
				MethodName = methodName;
				MethodSignature = methodSignature;
				ProfilerName = profilerName;
			}
		}

		public static List<ProfilerDescription> GetMonoBehaviourProfilers()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var assemblyName = "Assembly-CSharp";
			var assembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);

			// https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.html
			// I tried all events, but only these three are really visible in profiler
			var unityEvents = new HashSet<string>()
			{
				"FixedUpdate",
				"Update",
				"LateUpdate",
			};

			var resultProfilers = new HashSet<string>();
			var resultDescriptions = new List<ProfilerDescription>();
			foreach (var assemblyType in assembly.GetTypes())
			{
				// do not check structs and ignore generic classes
				if (assemblyType.IsClass && !assemblyType.ContainsGenericParameters && assemblyType.IsSubclassOf(typeof(MonoBehaviour)))
				{
					foreach (var method in assemblyType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
					{
						if (unityEvents.Contains(method.Name) && method.GetParameters().Length == 0 && method.GetMethodBody() != null)
						{
							// some methods are returned multiple times, so prevent repetitions
							var description = new ProfilerDescription(assemblyType, method.Name);
							if (resultProfilers.Add(description.ProfilerName))
							{
								resultDescriptions.Add(description);
							}
						}
					}
				}
			}

			return resultDescriptions;
		}

		public static void GenerateSourceCode(List<ProfilersGroupDescription> groups)
		{
			var builder = new StringBuilder();

			builder.AppendLine(@"using Comfort.Common;");
			builder.AppendLine(@"using SPT.Reflection.Patching;");
			builder.AppendLine(@"using System;");
			builder.AppendLine(@"using System.Collections.Generic;");
			builder.AppendLine(@"using System.Diagnostics;");
			builder.AppendLine(@"using System.Reflection;");
			builder.AppendLine(@"using System.Linq;");
			builder.AppendLine(@"using HarmonyLib;");
			builder.AppendLine(@"using EFT;");

			builder.AppendLine(@"namespace SevenBoldPencil.Profiler");
			builder.AppendLine(@"{");

			builder.AppendLine($"    public static class Profilers");
			builder.AppendLine(@"    {");
			builder.AppendLine(@"        public static List<ProfilersGroup> GetGroups()");
			builder.AppendLine(@"        {");
			builder.AppendLine($"            var groups = new List<ProfilersGroup>({groups.Count})");
			builder.AppendLine(@"            {");
		foreach (var group in groups)
		{
			builder.AppendLine($"                _Generated_Profilers_{group.Name}.GetProfilersGroup(),");
		}
			builder.AppendLine(@"            };");
			builder.AppendLine(@"            return groups;");
			builder.AppendLine(@"        }");
			builder.AppendLine(@"    }");

		foreach (var group in groups)
		{
			builder.AppendLine($"    public static class _Generated_Profilers_{group.Name}");
			builder.AppendLine(@"    {");
			builder.AppendLine(@"        public static ProfilersGroup GetProfilersGroup()");
			builder.AppendLine(@"        {");
			builder.AppendLine($"            var profilers = new List<IProfiler>({group.Profilers.Count})");
			builder.AppendLine(@"            {");
		foreach (var description in group.Profilers)
		{
			builder.AppendLine($"                new _Measure_{description.ProfilerName}(),");
		}
			builder.AppendLine(@"            };");
			builder.AppendLine($"            var profilersGroup = new ProfilersGroup(\"{group.Name}\", profilers);");
			builder.AppendLine(@"            return profilersGroup;");
			builder.AppendLine(@"        }");
			builder.AppendLine(@"    }");

		foreach (var description in group.Profilers)
		{
			var patchMethodSignature = string.IsNullOrWhiteSpace(description.MethodSignature)
				? $"{description.TypeName} __instance"
				: $"{description.TypeName} __instance, {description.MethodSignature}";

			builder.AppendLine($"    public class _Dummy_{description.ProfilerName} {{ }}");
			builder.AppendLine($"    public class _Measure_{description.ProfilerName} : MethodProfiler<{description.TypeName}, _Dummy_{description.ProfilerName}>");
			builder.AppendLine(@"    {");
			builder.AppendLine($"        public _Measure_{description.ProfilerName}() : base(\"{description.MethodName}\") {{ }}");
			builder.AppendLine($"        [PatchPrefix] [HarmonyPriority(int.MaxValue)] public static void Prefix({patchMethodSignature}) {{ StartMeasure(__instance); }}");
			builder.AppendLine($"        [PatchPostfix] [HarmonyPriority(int.MinValue)] public static void Postfix({patchMethodSignature}) {{ StopMeasure(__instance); }}");
			builder.AppendLine(@"    }");
		}
		}
			builder.AppendLine(@"}");

			var result = builder.ToString();
			File.WriteAllText($"Development/SPT-Profiler/Profiler/Generated.g.cs", result);
		}
	}
}
