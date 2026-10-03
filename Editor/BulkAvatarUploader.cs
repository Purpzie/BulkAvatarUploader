#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;

namespace Purpzie.BulkAvatarUploader {
	using static BulkAction;
	using static BulkState;

	internal enum BulkAction {
		NoAction = 0,
		Test,
		Upload
	}

	internal enum BulkState {
		Idle = 0,
		Paused,
		Building
	}

	internal delegate void ChangeEventHandler();
	internal delegate void ChangeEventHandler<T>(T newValue);

	internal static class BulkAvatarUploader {
		#region variables

		private const string MENU_PATH = "Tools/Bulk Avatar Uploader";
		public const string KEY_PREFIX = "Purpzie.BulkAvatarUploader";
		private const string STATE_KEY = KEY_PREFIX + ".State";
		private const string ACTION_KEY = KEY_PREFIX + ".LastAction";
		private const string INITIAL_PLATFORM_KEY = KEY_PREFIX + ".InitialPlatform";

		private static IVRCSdkAvatarBuilderApi? AvatarBuilder;
		private static CancellationTokenSource? BuilderCancelSource;
		private static bool Cancelled => BuilderCancelSource?.IsCancellationRequested ?? false;
		public static readonly List<AvatarStatus> Avatars = new();
		private static int AvatarIndex = 0;
		public static event ChangeEventHandler<BulkState>? OnStateChanged;
		public static event ChangeEventHandler? OnProgressChanged;

		public static BulkState State {
			get => (BulkState)SessionState.GetInt(STATE_KEY, (int)Idle);
			private set {
				if (State == value) return;
				SessionState.SetInt(STATE_KEY, (int)value);
				OnStateChanged?.Invoke(value);
			}
		}

		public static BulkAction Action {
			get => (BulkAction)SessionState.GetInt(ACTION_KEY, (int)NoAction);
			private set => SessionState.SetInt(ACTION_KEY, (int)value);
		}

		private static Platforms InitialPlatform {
			get => (Platforms)SessionState.GetInt(INITIAL_PLATFORM_KEY, (int)Platforms.None);
			set => SessionState.SetInt(INITIAL_PLATFORM_KEY, (int)value);
		}

		#endregion variables
		#region events

		[InitializeOnLoadMethod]
		private static void InitializeOnLoad() {
			AssemblyReloadEvents.beforeAssemblyReload -= DisposeNullAvatars;
			AssemblyReloadEvents.beforeAssemblyReload += DisposeNullAvatars;
			VRCSdkControlPanel.OnSdkPanelEnable -= OnVRCSdkPanelEnable;
			VRCSdkControlPanel.OnSdkPanelEnable += OnVRCSdkPanelEnable;
			if (State == Building) {
				EditorApplication.delayCall += OpenVRCSdkControlPanel;
				Window.ProgressText = "Waiting for VRC SDK...";
				OnProgressChanged?.Invoke();
			}
		}

		[MenuItem(MENU_PATH)]
		private static void OnMenuItemClicked() {
			OpenVRCSdkControlPanel();
			Window.GetOrOpen(true);
		}

		private static async void OnVRCSdkPanelEnable(object sender, EventArgs args) {
			if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out AvatarBuilder))
				throw new Exception("Failed to get builder from VRC SDK");

			AvatarBuilder.OnSdkBuildProgress -= OnSdkBuildProgress;
			AvatarBuilder.OnSdkBuildProgress += OnSdkBuildProgress;
			AvatarBuilder.OnSdkUploadStart -= OnSdkUploadStart;
			AvatarBuilder.OnSdkUploadStart += OnSdkUploadStart;
			AvatarBuilder.OnSdkUploadProgress -= OnSdkUploadProgress;
			AvatarBuilder.OnSdkUploadProgress += OnSdkUploadProgress;
			AvatarBuilder.OnSdkBuildSuccess -= OnSdkBuildSuccess;
			AvatarBuilder.OnSdkBuildSuccess += OnSdkBuildSuccess;

			// unfortunately we need to use some of the internal SDK API to fully support multiplatform builds
			// when 'ContentInfoLoaded' is fired, we can safely start building without being interrupted
			try {
				var fullyLoadedEvent = AvatarBuilder.GetType().GetEvent("ContentInfoLoaded");
				Action<object, object> handler = OnSdkFullyLoaded;
				var handlerDelegate = Delegate.CreateDelegate(
					fullyLoadedEvent.EventHandlerType,
					handler.Target,
					handler.Method
				);
				fullyLoadedEvent.RemoveEventHandler(AvatarBuilder, handlerDelegate);
				fullyLoadedEvent.AddEventHandler(AvatarBuilder, handlerDelegate);
			} catch (Exception error) {
				Debug.LogError($"Failed to listen to ContentInfoLoaded. Multiplatform bulk builds might break.\n{error}");
				await Task.Delay(5000); // hopefully this is long enough
				OnSdkFullyLoaded(null!, null!);
			}
		}

		private static bool HandledSdkFullyLoaded = false;
		private static async void OnSdkFullyLoaded(object _, object __) {
			if (HandledSdkFullyLoaded) return;
			HandledSdkFullyLoaded = true;
			if (State == Building) await BulkBuild(Action);
		}

		private static void OnSdkBuildProgress(object _, string status) {
			if (State != Building) return;
			Avatars[AvatarIndex].progressText = status;
			Window.RefreshItem(AvatarIndex);
		}

		private static void OnSdkBuildSuccess(object _, string __) {
			if (State == Building && Action == Test) {
				Avatars[AvatarIndex].progress = 1;
				Window.RefreshItem(AvatarIndex);
				OnProgressChanged?.Invoke();
			}
		}

		private static void OnSdkUploadStart(object _, object __) {
			if (State != Building) return;
			Avatars[AvatarIndex].progressText = "Uploading";
			Window.RefreshItem(AvatarIndex);
		}

		// this needs to be async or uploading will fail
		// https://feedback.vrchat.com/sdk-bug-reports/p/ivrcsdkbuilderapionsdkuploadprogress-must-be-async-or-upload-fails-with-unhelpfu
#pragma warning disable CS1998 // This async method lacks 'await' operators
		private static async void OnSdkUploadProgress(object _, (string status, float percentage) data) {
			if (State != Building) return;
			var avatar = Avatars[AvatarIndex];
			avatar.progress = data.percentage;
			int percent = (int) Math.Floor(data.percentage * 100);
			avatar.progressText = $"{data.status} {percent}%";
			Window.RefreshItem(AvatarIndex);
			OnProgressChanged?.Invoke();
		}
#pragma warning restore CS1998

		#endregion events
		#region behavior

		private static void OpenVRCSdkControlPanel()
			=> EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");

		public static void RefreshAvatars() {
			DisposeNullAvatars();
			Avatars.Clear();
			var pipelineManagers = UnityEngine.Object.FindObjectsOfType<PipelineManager>();
			foreach (var manager in pipelineManagers.OrderBy(p => p.gameObject.name))
				Avatars.Add(new AvatarStatus(manager));
			if (State == Idle) Reset(); // just in case
			Window.RefreshItems();
		}

		private static void DisposeNullAvatars() {
			foreach (var avatarStatus in Avatars)
				if (avatarStatus.pipelineManager == null || !avatarStatus.pipelineManager.isActiveAndEnabled)
					avatarStatus.Dispose();
		}

		public static void Cancel() {
			BuilderCancelSource?.Cancel();
			if (State == Building) State = Paused;
		}

		public static void Reset() {
			BuilderCancelSource?.Cancel();
			Action = NoAction;
			foreach (var avatar in Avatars) avatar.ClearStatus();
			State = Idle;
			InitialPlatform = Platforms.None;
		}

		public static bool Startup(BulkAction action, Platforms platformsToBuild) {
			OpenVRCSdkControlPanel();
			RefreshAvatars();

			if (platformsToBuild.Count() == 0) {
				EditorUtility.DisplayDialog(
					"Bulk Avatar Uploader",
					"There are no avatars selected.",
					"OK"
				);
				return false;
			}

			// check for missing blueprint ids
			if (action == Upload) {
				var missingBlueprints = Avatars
					.Where(avatar => string.IsNullOrWhiteSpace(avatar.blueprintId)).ToList();
				if (missingBlueprints.Count > 0) {
					bool singular = missingBlueprints.Count == 1;
					EditorUtility.DisplayDialog(
						"Bulk Avatar Uploader - Error",
						new StringBuilder(
							singular
								? "This avatar is missing a blueprint ID:\n\n"
								: "These avatars are missing blueprint IDS:\n\n"
						)
						.AppendJoin('\n', missingBlueprints.Select(avatar => avatar.name))
						.Append(
							singular
								? "\n\nPlease upload it normally before bulk uploading it, or assign an ID in its Pipeline Manager."
								: "\n\nPlease upload them normally before bulk uploading them, or assign blueprint IDs in their Pipeline Managers."
						)
						.ToString(),
						"OK"
					);
					return false;
				}
			}

			if (action == Test && platformsToBuild != Platforms.Windows) {
				var notWindows = platformsToBuild & ~Platforms.Windows;
				if (!EditorUtility.DisplayDialog(
					"Bulk Avatar Uploader - Warning",
					$"Are you sure you want to test on {notWindows}? This requires a separate device that can receive avatars. Please refer to VRChat's documentation: https://creators.vrchat.com/platforms",
					$"Yes, test on {notWindows}",
					"Cancel"
				)) return false;
			}

			if (action == Upload && !CopyrightHelper.ShowVRCAgreement()) return false;

			if (State == Idle) {
				Action = action;
				InitialPlatform = PlatformsExt.CURRENT;
			}

			State = Building;
			return true;
		}

		public static async Task BulkBuild(BulkAction action) {
			try {
				if (action == NoAction)
					throw new InvalidEnumArgumentException("action", (int)action, typeof(BulkAction));
				if (AvatarBuilder == null)
					throw new NullReferenceException("AvatarBuilder is null. Please wait for the VRC SDK window to open.");

				var remainingPlatforms = Avatars.Select(a => a.remainingPlatforms).ToPlatforms();
				if (State != Building && !Startup(action, remainingPlatforms)) return;

				// vrcfury parameter sync
				if (
					action == Upload
					&& !Platforms.Windows.IsCurrent()
					&& remainingPlatforms.HasFlag(Platforms.Windows)
				) {
					Window.ProgressText = "Switching to Windows...";
					OnProgressChanged?.Invoke();
					PlatformsExt.Switch(Platforms.Windows);
					return;
				}

				Window.ProgressText = null;
				OnProgressChanged?.Invoke();
				bool success = true;

				for (AvatarIndex = 0; AvatarIndex < Avatars.Count; AvatarIndex++) {
					if (Cancelled) {
						success = false;
						break;
					}

					var avatar = Avatars[AvatarIndex];
					if (!avatar.remainingPlatforms.HasCurrent()) continue;

					AvatarBuilder.SelectAvatar(avatar.gameObject);
					avatar.progress = 0;
					avatar.progressText = "Building";
					Window.selection = AvatarIndex;
					Window.RefreshItem(AvatarIndex);

					try {
						if (action == Upload) {
							if (string.IsNullOrWhiteSpace(avatar.blueprintId))
								throw new Exception("Missing blueprint ID");
							await CopyrightHelper.SetAccepted(avatar.blueprintId!);
							BuilderCancelSource ??= new();
							await AvatarBuilder.BuildAndUpload(
								avatar.gameObject,
								PerPlatformOverrides.GetPlatformOverrides(avatar.gameObject),
								await VRCApi.GetAvatar(avatar.blueprintId),
								cancellationToken: BuilderCancelSource.Token
							);
						} else
							await AvatarBuilder.BuildAndTest(avatar.gameObject);

						if (Cancelled) {
							avatar.progress = 0;
							success = false;
							break;
						} else {
							avatar.progress = 1;
							avatar.builtPlatforms |= PlatformsExt.CURRENT;
						}
					} catch (Exception error) {
						avatar.progress = 0;

						if (Cancelled) {
							// the error came from cancelling, not the avatar
							success = false;
							break;
						}

						avatar.failedPlatforms |= PlatformsExt.CURRENT;

						if (Settings.PauseOnAvatarErrors) {
							Cancel();
							EditorUtility.DisplayDialog(
								"Bulk Avatar Uploader - Avatar Error",
								error.Message + "\n\nSee logs for details.",
								"OK"
							);
							Debug.LogError(error);
							success = false;
							break;
						}

						Debug.LogError(error);
					} finally {
						avatar.progressText = null;
						Window.RefreshItem(AvatarIndex);
						OnProgressChanged?.Invoke();
					}
				}

				BuilderCancelSource?.Dispose();
				BuilderCancelSource = null;
				Window.selection = null;

				if (!success) {
					Cancel();
					return;
				}

				// this may have changed
				remainingPlatforms = Avatars.Select(a => a.remainingPlatforms).ToPlatforms();

				var nextPlatform = remainingPlatforms.AsEnumerable().FirstOrDefault();
				if (!nextPlatform.IsEmpty()) {
					Window.ProgressText = $"Switching to {nextPlatform}...";
					OnProgressChanged?.Invoke();
					PlatformsExt.Switch(nextPlatform);
					return;
				}

				// all done

				Cancel();
				if (!InitialPlatform.IsEmpty() && !InitialPlatform.IsCurrent()) {
					Window.ProgressText = $"Switching back to {InitialPlatform}...";
					OnProgressChanged?.Invoke();
					PlatformsExt.Switch(InitialPlatform);
				}
			} catch (Exception error) {
				Cancel();
				EditorUtility.DisplayDialog(
					"Bulk Avatar Uploader - Critical Error",
					error.Message + "\n\nCheck logs for details.",
					"Oof"
				);
				Debug.LogError(error);
			}
		}

		#endregion behavior
	}
}
