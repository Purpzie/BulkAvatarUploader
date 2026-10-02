#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Purpzie.BulkAvatarUploader {
	/// <summary>
	/// Platforms supported by VRChat.
	/// </summary>
	[Flags]
	internal enum Platforms {
		Windows = 1,
		Android = 2,
		iOS = 4,

		None = 0,
		Everything = Windows | Android | iOS
	}

	internal static class PlatformsExt {
		private static readonly Dictionary<Platforms, PlatformInfo> PLATFORM_INFO = new() {
			{Platforms.Windows, new() {
				buildTarget = BuildTarget.StandaloneWindows64,
				buildTargetGroup = BuildTargetGroup.Standalone,
				icon = EditorGUIUtility.FindTexture("d_BuildSettings.Metro.Small")
			}},
			{Platforms.Android, new() {
				buildTarget = BuildTarget.Android,
				buildTargetGroup = BuildTargetGroup.Android,
				icon = EditorGUIUtility.FindTexture("d_BuildSettings.Android.Small")
			}},
			{Platforms.iOS, new() {
				buildTarget = BuildTarget.iOS,
				buildTargetGroup = BuildTargetGroup.iOS,
				icon = EditorGUIUtility.FindTexture("d_BuildSettings.iPhone.Small")
			}}
		};

		private struct PlatformInfo {
			public BuildTarget buildTarget;
			public BuildTargetGroup buildTargetGroup;
			public Texture2D icon;
		}

		private static PlatformInfo Info(this Platforms platform) {
			if (!PLATFORM_INFO.TryGetValue(platform, out var info))
				throw new InvalidEnumArgumentException("platform", (int)platform, typeof(Platforms));
			return info;
		}

		public static BuildTarget ToBuildTarget(this Platforms platform)
			=> platform.Info().buildTarget;

		public static BuildTargetGroup ToBuildTargetGroup(this Platforms platform)
			=> platform.Info().buildTargetGroup;

		public static Texture2D ToIcon(this Platforms platform)
			=> platform.Info().icon;

		public static bool IsSingle(this Platforms platform) {
			// is power of 2 and > 0
			int i = (int)platform;
			return ((i & (i - 1)) == 0) && (i > 0);
		}

		private static readonly Platforms[] PLATFORMS_ARRAY =
			Enum.GetValues(typeof(Platforms)).Cast<Platforms>().Where(IsSingle).ToArray();

		private static readonly Dictionary<BuildTarget, Platforms> REVERSE_MAP =
			PLATFORMS_ARRAY.ToDictionary(ToBuildTarget);

		public static Platforms ToPlatform(this BuildTarget target) {
			if (!REVERSE_MAP.TryGetValue(target, out var platform))
				throw new InvalidEnumArgumentException("target", (int)target, typeof(BuildTarget));
			return platform;
		}

		public static readonly Platforms CURRENT = EditorUserBuildSettings.activeBuildTarget.ToPlatform();
		public static bool IsCurrent(this Platforms platform) => CURRENT == platform;
		public static bool HasCurrent(this Platforms platforms) => platforms.HasFlag(CURRENT);

		public static readonly Platforms SUPPORTED = PLATFORMS_ARRAY
			.Where(p => BuildPipeline.IsBuildTargetSupported(p.ToBuildTargetGroup(), p.ToBuildTarget()))
			.ToPlatforms();

		public static bool Supported(this Platforms platform) => SUPPORTED.HasFlag(platform);

		public static int Count(this Platforms platforms) {
			// count set bits
			int count = 0;
			int i = (int)platforms;
			while (i > 0) {
				count += i & 1;
				i >>= 1;
			}
			return count;
		}

		public static bool IsEmpty(this Platforms platforms) => platforms == Platforms.None;

		public static IEnumerable<Platforms> AsEnumerable(this Platforms source)
			=> PLATFORMS_ARRAY.Where(p => source.HasFlag(p));

		public static IEnumerator<Platforms> GetEnumerator(this Platforms platforms)
			=> platforms.AsEnumerable().GetEnumerator();

		public static Platforms ToPlatforms(this IEnumerable<Platforms> source) {
			var result = Platforms.None;
			foreach (var platforms in source) {
				result |= platforms;
				if (result == Platforms.Everything) break;
			}
			return result;
		}

		public static void Switch(Platforms platform) {
			if (!platform.Supported())
				throw new Exception($"Support for {platform} is not installed");

			bool success = EditorUserBuildSettings.SwitchActiveBuildTargetAsync(
				platform.ToBuildTargetGroup(),
				platform.ToBuildTarget()
			);

			if (!success) throw new Exception($"Failed to switch to {platform}");
		}
	}
}
