#nullable enable
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Purpzie.BulkAvatarUploader
{
	[FilePath("Purpzie/BulkAvatarUploader.yml", FilePathAttribute.Location.PreferencesFolder)]
	internal class Settings : ScriptableSingleton<Settings>
	{
		public static void Save() => instance.Save(true);

		// defaults
		private static readonly Color SELECTED_COLOR = new(1f, 1f, 1f);
		private static readonly Color UNSELECTED_COLOR = new(0.66f, 0.66f, 0.66f);
		private static readonly Color SUCCESS_COLOR = new(0.349f, 0.922f, 0.361f);
		private static readonly Color FAILURE_COLOR = new(1, 0.463f, 0.463f);
		private static readonly Color BUILDING_COLOR = new(0.38f, 0.82f, 0.98f);
		private static readonly bool PAUSE_ON_AVATAR_ERRORS = true;

		[SerializeField] private Color _selectedColor = SELECTED_COLOR;
		public static Color SelectedColor => instance._selectedColor;

		[SerializeField] private Color _unselectedColor = UNSELECTED_COLOR;
		public static Color UnselectedColor => instance._unselectedColor;

		[SerializeField] private Color _successColor = SUCCESS_COLOR;
		public static Color SuccessColor => instance._successColor;

		[SerializeField] private Color _failureColor = FAILURE_COLOR;
		public static Color FailureColor => instance._failureColor;

		[SerializeField] private Color _buildingColor = BUILDING_COLOR;
		public static Color BuildingColor => instance._buildingColor;

		[Tooltip("If you turn this off, avatars with errors will be skipped. <color=red>You might miss error details!</color>")]
		[SerializeField] private bool _pauseOnAvatarErrors = PAUSE_ON_AVATAR_ERRORS;
		public static bool PauseOnAvatarErrors => instance._pauseOnAvatarErrors;

		public static void Reset()
		{
			instance._selectedColor = SELECTED_COLOR;
			instance._unselectedColor = UNSELECTED_COLOR;
			instance._successColor = SUCCESS_COLOR;
			instance._failureColor = FAILURE_COLOR;
			instance._buildingColor = BUILDING_COLOR;
			instance._pauseOnAvatarErrors = PAUSE_ON_AVATAR_ERRORS;
			Save();
		}

		[CustomEditor(typeof(Settings))]
		private class SettingsEditor : Editor
		{
			public override VisualElement CreateInspectorGUI()
			{
				VisualElement container = new();
				var prop = serializedObject.FindProperty("m_Script"); // just hide this, it's ugly
				while (prop.NextVisible(false)) container.Add(new PropertyField(prop));
				return container;
			}
		}
	}
}
