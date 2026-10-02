#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using VRC.SDKBase;
using VRC.SDKBase.Editor.Api;

namespace Purpzie.BulkAvatarUploader {
	using static BulkState;

	/// <summary>
	/// While the VRC SDK has public API for accepting its copyright agreement, it only works on one
	/// avatar at a time. This exists so the user doesn't need to click a button for every avatar.
	/// </summary>
	internal static class CopyrightHelper {
		private const string ACCEPTED_KEY = BulkAvatarUploader.KEY_PREFIX + ".CopyrightHelper.Accepted";
		private static bool Accepted {
			get => SessionState.GetBool(ACCEPTED_KEY, false);
			set => SessionState.SetBool(ACCEPTED_KEY, value);
		}

		[InitializeOnLoadMethod]
		private static void InitializeOnLoad() {
			BulkAvatarUploader.OnStateChanged -= OnStateChanged;
			BulkAvatarUploader.OnStateChanged += OnStateChanged;
		}

		private static void OnStateChanged(BulkState state) {
			if (state != Building) Accepted = false;
		}

		public static bool ShowVRCAgreement() {
			// identical to VRC's agreement prompt
			Accepted = EditorUtility.DisplayDialog(
				"Copyright ownership agreement",
				VRCCopyrightAgreement.AgreementText,
				"OK",
				"Cancel"
			);
			return Accepted;
		}

		private const string VRC_AGREEMENT_KEY = "VRCSdkControlPanel.CopyrightAgreement.ContentList";
		private const string VRC_AGREEMENT_CODE = "content.copyright.owned";
		private const int VRC_AGREEMENT_VERSION = 1;
		private static readonly HashSet<string> AcceptedCache = new();

		public static async Task SetAccepted(string blueprintId) {
			if (!Accepted) throw new Exception("user has not accepted copyright agreement yet");

			if (AcceptedCache.Contains(blueprintId)) return;
			string[]? acceptedIds = SessionState.GetString(VRC_AGREEMENT_KEY, null)
				?.Split(';', StringSplitOptions.RemoveEmptyEntries);
			if (acceptedIds != null) AcceptedCache.UnionWith(acceptedIds);
			if (AcceptedCache.Contains(blueprintId)) return;

			var result = await VRCApi.ContentUploadConsent(new VRCAgreement {
				AgreementCode = VRC_AGREEMENT_CODE,
				AgreementFulltext = VRCCopyrightAgreement.AgreementText,
				ContentId = blueprintId,
				Version = VRC_AGREEMENT_VERSION
			});

			if (
				result.ContentId != blueprintId
				|| result is not { Version: VRC_AGREEMENT_VERSION, AgreementCode: VRC_AGREEMENT_CODE }
			) throw new Exception($"Failed to accept VRC copyright agreement for {blueprintId}");

			AcceptedCache.Add(blueprintId);
			SessionState.SetString(VRC_AGREEMENT_KEY, string.Join(';', AcceptedCache));
		}
	}
}
