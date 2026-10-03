#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Purpzie.BulkAvatarUploader {
	using static BulkAction;
	using static BulkState;

	internal class Window : EditorWindow {
		private static Window? _Instance;
		private static Window Instance => GetOrOpen(false);

		[NonSerialized] private bool initialized = false;
		private bool settingsOpen = false;
		private float progress = 0;
		public static string? ProgressText;

		[SerializeField] private VisualTreeAsset uxml = null!; // already set in script editor
		private MultiColumnListView avatarsUI = null!;
		private VisualElement settingsContainer = null!;
		private Label titleLabel = null!;
		private ProgressBar progressBar = null!;
		private Toggle? headerAvatarToggle;
		private readonly Dictionary<Platforms, PlatformToggle> headerPlatformToggles = new();

		private Button settingsButton = null!;
		private Button settingsBackButton = null!;
		private Button settingsResetButton = null!;
		private Button refreshButton = null!;
		private Button testButton = null!;
		private Button uploadButton = null!;
		private Button doneButton = null!;
		private Button resumeButton = null!;
		private Button stopButton = null!;
		private Button pauseButton = null!;

		public static int? selection {
			get => Instance.avatarsUI?.selectedIndex;
			set {
				if (Instance.avatarsUI == null) return;
				if (value.HasValue) Instance.avatarsUI.SetSelection(value.Value);
				else Instance.avatarsUI.ClearSelection();
			}
		}

		public static Window GetOrOpen(bool focus = true) {
			if (_Instance == null) {
				_Instance = GetWindow<Window>("Bulk Avatar Uploader", focus);
				_Instance.minSize = new Vector2(501, 250);
				_Instance.titleContent.image = EditorGUIUtility.IconContent("Column Reverse").image;
			} else if (focus && !_Instance.hasFocus)
				_Instance.Focus();
			return _Instance;
		}

		private void CreateGUI() {
			BulkAvatarUploader.RefreshAvatars();
			var root = rootVisualElement;
			root.Add(uxml.Instantiate());

			avatarsUI = root.Q<MultiColumnListView>("avatar-list");
			titleLabel = root.Q<Label>("title");
			progressBar = root.Q<ProgressBar>("progress");
			settingsContainer = root.Q("settings-container");
			settingsButton = root.Q<Button>("settings");
			settingsBackButton = root.Q<Button>("settings-back");
			settingsResetButton = root.Q<Button>("settings-reset");
			refreshButton = root.Q<Button>("refresh");
			testButton = root.Q<Button>("test");
			uploadButton = root.Q<Button>("upload");
			doneButton = root.Q<Button>("done");
			resumeButton = root.Q<Button>("resume");
			stopButton = root.Q<Button>("stop");
			pauseButton = root.Q<Button>("pause");

			initialized = true;

			avatarsUI.itemsSource = BulkAvatarUploader.Avatars;
			settingsContainer.Add(new InspectorElement(Settings.instance));
			settingsButton.clicked += ToggleSettings;
			settingsBackButton.clicked += ToggleSettings;
			settingsResetButton.clicked += Settings.Reset;
			refreshButton.clicked += BulkAvatarUploader.RefreshAvatars;
			testButton.clicked += async () => await BulkAvatarUploader.BulkBuild(Test);
			uploadButton.clicked += async () => await BulkAvatarUploader.BulkBuild(Upload);
			doneButton.clicked += BulkAvatarUploader.Reset;
			resumeButton.clicked += async () => await BulkAvatarUploader.BulkBuild(BulkAvatarUploader.Action);
			stopButton.clicked += BulkAvatarUploader.Reset;
			pauseButton.clicked += BulkAvatarUploader.Cancel;

			SetupAvatarToggleColumn();
			SetupAvatarColumn();
			InsertPlatformColumns(2);
			SetupProgressColumn();

			OnProgressChanged();
			OnStateChanged(BulkAvatarUploader.State);

			BulkAvatarUploader.OnStateChanged -= OnStateChanged;
			BulkAvatarUploader.OnStateChanged += OnStateChanged;
			BulkAvatarUploader.OnProgressChanged -= OnProgressChanged;
			BulkAvatarUploader.OnProgressChanged += OnProgressChanged;
		}

		private void OnDestroy() {
			BulkAvatarUploader.Reset();
			Settings.Save();
			BulkAvatarUploader.OnStateChanged -= OnStateChanged;
			BulkAvatarUploader.OnProgressChanged -= OnProgressChanged;
		}

		private void OnStateChanged(BulkState state) {
			if (!initialized) return;

			bool building = state == Building;
			bool paused = state == Paused;
			bool idle = state == Idle;

			//avatarsUI.sortingEnabled = !building;
			//avatarsUI.reorderable = !building;
			avatarsUI.RefreshItems();
			titleContent.text = paused ? "Bulk Avatar Uploader*" : "Bulk Avatar Uploader";

			titleLabel.SetVisible(idle);
			progressBar.SetVisible(!idle);
			avatarsUI.SetVisible(!settingsOpen);
			settingsContainer.SetVisible(settingsOpen);
			settingsButton.SetVisible(!building && !settingsOpen);
			settingsBackButton.SetVisible(settingsOpen);
			settingsResetButton.SetVisible(settingsOpen);
			refreshButton.SetVisible(!building && !settingsOpen);
			testButton.SetVisible(idle && !settingsOpen);
			uploadButton.SetVisible(idle && !settingsOpen);
			doneButton.SetVisible(paused && !settingsOpen);
			resumeButton.SetVisible(paused && !settingsOpen && progress < 1);
			stopButton.SetVisible(building);
			pauseButton.SetVisible(building);
		}

		public static void RefreshItem(int index) => Instance.avatarsUI?.RefreshItem(index);

		public static void RefreshItems() => Instance.avatarsUI?.RefreshItems();

		private void UpdateHeaderToggles() {
			var platforms = BulkAvatarUploader.Avatars.Select(a => a.selectedPlatforms).ToPlatforms();
			headerAvatarToggle?.SetValueWithoutNotify(!platforms.IsEmpty());
			foreach (var (platform, toggle) in headerPlatformToggles) {
				bool value = platforms.HasFlag(platform);
				toggle.SetValueWithoutNotify(value);
				toggle.iconColor = value ? Settings.SelectedColor : Settings.UnselectedColor;
			}
		}

		private void ToggleSettings() {
			settingsOpen = !settingsOpen;
			OnStateChanged(BulkAvatarUploader.State);
		}

		private void OnProgressChanged() {
			if (!initialized) return;

			float totalProgress = 0;
			int totalAvatarCount = 0;
			foreach (var avatar in BulkAvatarUploader.Avatars) {
				int selectedCount = avatar.selectedPlatforms.Count();
				totalAvatarCount += selectedCount;
				if (avatar.failedPlatforms.HasFlag(Platforms.Windows))
					totalProgress += selectedCount;
				else {
					var finishedPlatforms = avatar.builtPlatforms | avatar.failedPlatforms;
					totalProgress += (finishedPlatforms & ~PlatformsExt.CURRENT).Count()
						+ (finishedPlatforms.HasCurrent() ? 1 : avatar.progress);
				}
			}

			progress = totalProgress / totalAvatarCount;
			progressBar.SetValueAnimated(progress);
			int percent = (int)Math.Floor(progress * 100);
			if (ProgressText == null)
				progressBar.title = percent + "%";
			else
				progressBar.title = $"{ProgressText} {percent}%";
		}

		private class IndexedToggle : Toggle {
			public int index = 0;
		}

		private class IndexedPlatformToggle : PlatformToggle {
			public int index = 0;
			public IndexedPlatformToggle(Platforms platform) : base(platform) { }
		}

		private void SetupAvatarToggleColumn() {
			var avatarToggleColumn = avatarsUI.columns["avatar-toggle"];

			avatarToggleColumn.makeHeader = () => {
				headerAvatarToggle = new Toggle();
				UpdateHeaderToggles();
				headerAvatarToggle.RegisterValueChangedCallback(v => {
					if (BulkAvatarUploader.State == Building) {
						headerAvatarToggle.SetValueWithoutNotify(v.previousValue);
						return;
					}
					foreach (var avatar in BulkAvatarUploader.Avatars)
						avatar.selectedPlatforms = v.newValue ? PlatformsExt.SUPPORTED : Platforms.None;
					foreach (var (platform, toggle) in headerPlatformToggles)
						if (platform.Supported()) toggle.SetValueWithoutNotify(v.newValue);
					avatarsUI.RefreshItems();
				});
				return headerAvatarToggle;
			};

			avatarToggleColumn.makeCell = () => {
				var avatarToggle = new IndexedToggle { value = true };
				avatarToggle.RegisterValueChangedCallback(v => {
					if (BulkAvatarUploader.State == Building) {
						avatarToggle.SetValueWithoutNotify(v.previousValue);
						return;
					}
					var avatar = BulkAvatarUploader.Avatars[avatarToggle.index];
					avatar.selectedPlatforms = v.newValue ? PlatformsExt.SUPPORTED : Platforms.None;
					UpdateHeaderToggles();
					avatarsUI.RefreshItems();
				});
				return avatarToggle;
			};

			avatarToggleColumn.bindCell = (element, index) => {
				var avatarToggle = (element as IndexedToggle)!;
				avatarToggle.index = index;
				var selectedPlatforms = BulkAvatarUploader.Avatars[index].selectedPlatforms;
				avatarToggle.SetValueWithoutNotify(
					BulkAvatarUploader.Avatars[index].selectedPlatforms != Platforms.None
				);
			};
		}

		private class IndexedTextElement : TextElement {
			public int index = 0;
		}

		private void SetupAvatarColumn() {
			var avatarColumn = avatarsUI.columns["avatar"];

			avatarColumn.makeCell = () => {
				IndexedTextElement field = new();
				field.style.paddingLeft = 3;
				field.style.unityFontStyleAndWeight = FontStyle.Bold;
				field.style.whiteSpace = WhiteSpace.NoWrap;
				field.RegisterCallback<MouseDownEvent>(_ => {
					var obj = BulkAvatarUploader.Avatars[field.index].gameObject;
					if (obj == null) return;
					EditorGUIUtility.PingObject(obj);
					Selection.activeObject = obj;
				});
				return field;
			};

			avatarColumn.bindCell = (element, index) => {
				var field = (element as IndexedTextElement)!;
				field.index = index;
				var avatar = BulkAvatarUploader.Avatars[index];
				field.text = avatar.name ?? "(Missing)";
				field.tooltip = field.text;
				field.SetEnabled(avatar.selectedPlatforms != Platforms.None);
			};
		}

		private void SetupProgressColumn() {
			var progressColumn = avatarsUI.columns["progress"];
			progressColumn.makeCell = () => new ProgressBar { highValue = 1 };
			progressColumn.bindCell = (element, index) => {
				var avatar = BulkAvatarUploader.Avatars[index];
				var progressBar = (element as ProgressBar)!;
				if (string.IsNullOrEmpty(avatar.progressText))
					progressBar.SetVisible(false);
				else {
					progressBar.SetVisible(true);
					progressBar.SetValueAnimated(avatar.progress);
					progressBar.title = avatar.progressText;
				}
			};
		}

		private void InsertPlatformColumns(int index) {
			foreach (var platform in Platforms.Everything) {
				Column platformColumn = new() {
					title = platform.ToString(),
					minWidth = 40,
					width = 40,
					maxWidth = 40,
					resizable = false
				};

				avatarsUI.columns.Insert(index, platformColumn);

				platformColumn.makeHeader = () => {
					var headerToggle = new PlatformToggle(platform);
					headerPlatformToggles[platform] = headerToggle;
					UpdateHeaderToggles();
					if (!platform.Supported()) {
						headerToggle.SetEnabled(false);
						headerToggle.tooltip = $"You don't have {platform} support installed.";
						headerToggle.iconColor = Settings.UnselectedColor;
						return headerToggle;
					}
					headerToggle.tooltip = platform.ToString();
					headerToggle.RegisterValueChangedCallback(v => {
						if (BulkAvatarUploader.State == Building) {
							headerToggle.SetValueWithoutNotify(v.previousValue);
							return;
						}
						foreach (var avatar in BulkAvatarUploader.Avatars) {
							if (v.newValue) avatar.selectedPlatforms |= platform;
							else avatar.selectedPlatforms &= ~platform;
						}
						avatarsUI.RefreshItems();
						UpdateHeaderToggles();
					});
					return headerToggle;
				};

				platformColumn.makeCell = () => {
					var toggle = new IndexedPlatformToggle(platform);
					if (!platform.Supported()) {
						toggle.SetEnabled(false);
						toggle.tooltip = $"You don't have {platform} support installed.";
						toggle.iconColor = Settings.UnselectedColor;
						return toggle;
					}
					toggle.RegisterValueChangedCallback(v => {
						if (BulkAvatarUploader.State == Building) {
							toggle.SetValueWithoutNotify(v.previousValue);
							return;
						}
						var avatar = BulkAvatarUploader.Avatars[toggle.index];
						if (v.newValue) avatar.selectedPlatforms |= platform;
						else avatar.selectedPlatforms &= ~platform;
						avatar.builtPlatforms &= ~platform;
						avatar.failedPlatforms &= ~platform;
						avatarsUI.RefreshItems(); // refresh all so platform overrides update
						UpdateHeaderToggles();
					});
					return toggle;
				};

				platformColumn.bindCell = (element, index) => {
					if (!platform.Supported()) return;

					var toggle = (element as IndexedPlatformToggle)!;
					toggle.index = index;
					var avatar = BulkAvatarUploader.Avatars[index];
					bool building = BulkAvatarUploader.State == Building;
					bool overridden = avatar.overriddenPlatforms.HasFlag(platform);

					// vrcfury parameter sync
					bool windowsFailed = platform != Platforms.Windows && avatar.failedPlatforms.HasFlag(Platforms.Windows);

					toggle.SetValueWithoutNotify(avatar.remainingPlatforms.HasFlag(platform));
					toggle.SetEnabled(!overridden && !windowsFailed);

					if (windowsFailed) {
						toggle.iconColor = Settings.UnselectedColor;
						toggle.tooltip = $"Can't select {platform} because Windows failed";
					} else if (avatar.failedPlatforms.HasFlag(platform)) {
						toggle.iconColor = Settings.FailureColor;
						toggle.tooltip = $"This avatar had an error on {platform}.";
					} else if (avatar.builtPlatforms.HasFlag(platform)) {
						toggle.iconColor = Settings.SuccessColor;
						toggle.tooltip = $"This avatar was successfully {(BulkAvatarUploader.Action == Upload ? "uploaded" : "built")} on {platform}.";
					} else if (building && platform.IsCurrent() && avatar.progressText != null) {
						toggle.iconColor = Settings.BuildingColor;
						toggle.tooltip = null;
					} else {
						toggle.tooltip = overridden ? $"This avatar is overridden on {platform}." : platform.ToString();
						toggle.iconColor = avatar.selectedPlatforms.HasFlag(platform)
							? Settings.SelectedColor
							: Settings.UnselectedColor;
					}
				};

				index++;
			}
		}
	}
}
