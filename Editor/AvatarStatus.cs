#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;

namespace Purpzie.BulkAvatarUploader {
	internal class AvatarStatus : IDisposable {
		public readonly PipelineManager pipelineManager;
		public float progress = 0;
		public string? progressText;

		public string? blueprintId => pipelineManager != null ? pipelineManager.blueprintId : null;
		public GameObject? gameObject => pipelineManager != null ? pipelineManager.gameObject : null;
		public string? name => gameObject != null ? gameObject.name : null;

		private AvatarSessionState session;
		private readonly Dictionary<Platforms, AvatarSessionState> platformOverrides = new();

		public Platforms overriddenPlatforms => platformOverrides
			.Where(pair => pair.Value.selectedPlatforms.HasFlag(pair.Key))
			.Select(pair => pair.Key)
			.ToPlatforms();

		public Platforms selectedPlatforms {
			get => session.selectedPlatforms & ~overriddenPlatforms;
			set => session.selectedPlatforms = value;
		}

		public Platforms builtPlatforms {
			get => session.builtPlatforms;
			set => session.builtPlatforms = value;
		}

		public Platforms failedPlatforms {
			get => session.failedPlatforms;
			set => session.failedPlatforms = value;
		}

		public Platforms remainingPlatforms {
			get {
				// vrcfury parameter sync
				if (failedPlatforms.HasFlag(Platforms.Windows)) return Platforms.None;
				return selectedPlatforms & ~builtPlatforms & ~failedPlatforms;
			}
		}

		public AvatarStatus(PipelineManager pipelineManager) {
			this.pipelineManager = pipelineManager;
			session = new AvatarSessionState(pipelineManager);
			session.Initialize();

			foreach (var platOver in PerPlatformOverrides.GetPlatformOverrides(gameObject) ?? new())
				if (
					platOver.avatar != null
					&& !EditorUtility.IsPersistent(platOver.avatar) // not a prefab in assets
					&& platOver.avatar.gameObject.activeInHierarchy
					&& platOver.avatar.TryGetComponent<PipelineManager>(out var otherManager)
					&& otherManager.blueprintId == pipelineManager.blueprintId
				)
					platformOverrides[platOver.platform.ToPlatform()] = new AvatarSessionState(otherManager);
		}

		public void ClearStatus() {
			progress = 0;
			progressText = null;
			builtPlatforms = Platforms.None;
			failedPlatforms = Platforms.None;
		}

		public void Dispose() => session.Dispose();

		/// <summary>
		/// Stores information about an avatar across assembly reloads during multiplatform builds
		/// </summary>
		private struct AvatarSessionState : IDisposable {
			private readonly string SELECTED_PLATFORMS_KEY;
			public Platforms selectedPlatforms {
				get => GetPlatforms(SELECTED_PLATFORMS_KEY) & PlatformsExt.SUPPORTED; // just in case
				set => SetPlatforms(SELECTED_PLATFORMS_KEY, value);
			}

			private readonly string BUILT_PLATFORMS_KEY;
			public Platforms builtPlatforms {
				get => GetPlatforms(BUILT_PLATFORMS_KEY);
				set => SetPlatforms(BUILT_PLATFORMS_KEY, value);
			}

			private readonly string FAILED_PLATFORMS_KEY;
			public Platforms failedPlatforms {
				get => GetPlatforms(FAILED_PLATFORMS_KEY);
				set => SetPlatforms(FAILED_PLATFORMS_KEY, value);
			}

			private readonly string EXISTS_KEY;
			private bool exists {
				get => SessionState.GetBool(EXISTS_KEY, false);
				set => SessionState.SetBool(EXISTS_KEY, value);
			}

			public AvatarSessionState(PipelineManager pipelineManager) {
				EXISTS_KEY = new StringBuilder(BulkAvatarUploader.KEY_PREFIX)
					.Append(".AvatarStatus.")
					.Append(pipelineManager.GetInstanceID())
					.ToString();
				SELECTED_PLATFORMS_KEY = EXISTS_KEY + ".SelectedPlatforms";
				BUILT_PLATFORMS_KEY = EXISTS_KEY + ".BuiltPlatforms";
				FAILED_PLATFORMS_KEY = EXISTS_KEY + ".FailedPlatforms";
			}

			public readonly void Dispose() {
				SessionState.EraseBool(EXISTS_KEY);
				SessionState.EraseInt(SELECTED_PLATFORMS_KEY);
				SessionState.EraseInt(BUILT_PLATFORMS_KEY);
				SessionState.EraseInt(FAILED_PLATFORMS_KEY);
			}

			public void Initialize() {
				if (!exists) {
					exists = true;
					selectedPlatforms = PlatformsExt.CURRENT;
				}
			}

			private readonly Platforms GetPlatforms(string key, Platforms defaultPlatforms = default)
				=> (Platforms)SessionState.GetInt(key, (int)defaultPlatforms);

			private void SetPlatforms(string key, Platforms platforms)
				=> SessionState.SetInt(key, (int)platforms);
		}
	}
}
