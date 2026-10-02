#nullable enable
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Purpzie.BulkAvatarUploader {
	internal class PlatformToggle : Toggle {
		public readonly Platforms platform;
		private readonly Image icon;

		public PlatformToggle(Platforms platform) : base() {
			this.platform = platform;
			icon = new Image { image = platform.ToIcon() };
			Add(icon);
		}

		public Color iconColor {
			get => icon.tintColor;
			set => icon.tintColor = value;
		}
	}

	internal static class ProgressBarExt {
		public static void SetValueAnimated(
			this ProgressBar progressBar,
			float newValue,
			int durationMs = 500
		) {
			progressBar.experimental.animation.Start(
				progressBar.value,
				newValue,
				durationMs,
				(p, val) => ((ProgressBar)p).value = val
			);
		}
	}

	// Button.iconImage isn't available until 2023.2 :(
	internal class IconButton : Button {
		public new class UxmlFactory : UxmlFactory<IconButton, UxmlTraits> { }

		public new class UxmlTraits : Button.UxmlTraits {
			private readonly UxmlStringAttributeDescription m_Icon
				= new() { name = "icon", defaultValue = "d__Help" };

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				var self = (IconButton)ve;
				self.icon = m_Icon.GetValueFromBag(bag, cc);
			}
		}

		private readonly TextElement textElement = new();
		private readonly Image image = new();

		private string _icon = string.Empty;
		public string icon {
			get => _icon;
			set {
				_icon = value;
				image.image = EditorGUIUtility.FindTexture(value);
			}
		}

		public override string text {
			get => textElement.text;
			set => textElement.text = value;
		}

		public IconButton() : base() {
			style.flexDirection = FlexDirection.Row;
			image.style.marginRight = 2;
			Add(image);
			Add(textElement);
		}
	}
}
