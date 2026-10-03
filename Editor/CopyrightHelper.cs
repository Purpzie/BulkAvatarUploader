#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using VRC.SDKBase.Editor.Api;

namespace Purpzie.BulkAvatarUploader {
	using static BulkState;

	/// <summary>
	/// While the VRC SDK has public API for accepting its copyright agreement, it only works on one
	/// avatar at a time. This exists so the user doesn't need to click a button for every avatar.
	/// </summary>
	internal static class CopyrightHelper {
		// keep in sync with VRCCopyrightAgreement.cs in SDK
		private const int VRC_AGREEMENT_VERSION = 1;
		private const string VRC_AGREEMENT_CODE = "content.copyright.owned";
		private const string VRC_AGREEMENT_TEXT = "By clicking OK, I certify that I have the necessary rights to upload this content and that it will not infringe on any third-party legal or intellectual property rights.";
		private const string VRC_CONTENT_LIST_KEY = "VRCSdkControlPanel.CopyrightAgreement.ContentList";
		private const string VRC_PROMPT_TITLE = "Copyright ownership agreement";
		private const string VRC_PROMPT_OK = "OK";
		private const string VRC_PROMPT_CANCEL = "Cancel";
		private struct VRCAgreement {
			[JsonProperty("id")]
			public string ID { get; set; }
			public string AgreementCode { get; set; }
			public string ContentId { get; set; }
			public string AgreementFulltext { get; set; }
			public int Version { get; set; }
			public string[] Tags { get; set; }
		}

		private static readonly HashSet<string> ContentCache = new();

		private const string ACCEPTED_KEY = BulkAvatarUploader.KEY_PREFIX + ".CopyrightHelper.Accepted";
		private static bool Accepted {
			get => SessionState.GetBool(ACCEPTED_KEY, false);
			set => SessionState.SetBool(ACCEPTED_KEY, value);
		}

		public static bool ShowVRCAgreement() {
			// identical to VRC's agreement prompt
			Accepted = EditorUtility.DisplayDialog(
				VRC_PROMPT_TITLE,
				VRC_AGREEMENT_TEXT,
				VRC_PROMPT_OK,
				VRC_PROMPT_CANCEL
			);
			return Accepted;
		}

		public static async Task SetAccepted(string blueprintId) {
			if (!Accepted) throw new Exception("User has not accepted VRC copyright agreement yet");

			if (ContentCache.Contains(blueprintId)) return;
			string[]? acceptedContent = SessionState.GetString(VRC_CONTENT_LIST_KEY, null)
				?.Split(';', StringSplitOptions.RemoveEmptyEntries);
			if (acceptedContent != null) {
				ContentCache.UnionWith(acceptedContent);
				if (ContentCache.Contains(blueprintId)) return;
			}

			var result = await VRCApi.Post<VRCAgreement, VRCAgreement>("agreement", new VRCAgreement {
				AgreementCode = VRC_AGREEMENT_CODE,
				AgreementFulltext = VRC_AGREEMENT_TEXT,
				Version = VRC_AGREEMENT_VERSION,
				ContentId = blueprintId
			});

			if (
				result.ContentId != blueprintId
				|| result is not { Version: VRC_AGREEMENT_VERSION, AgreementCode: VRC_AGREEMENT_CODE }
			) throw new Exception($"Failed to accept VRC copyright agreement for {blueprintId}");

			ContentCache.Add(blueprintId);
			SessionState.SetString(VRC_CONTENT_LIST_KEY, string.Join(';', ContentCache));
		}

		[InitializeOnLoadMethod]
		private static void InitializeOnLoad() {
			BulkAvatarUploader.OnStateChanged -= OnStateChanged;
			BulkAvatarUploader.OnStateChanged += OnStateChanged;
		}

		private static void OnStateChanged(BulkState state) {
			if (state != Building) Accepted = false;
		}
	}
}
