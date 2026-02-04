using EFT;
using EFT.InventoryLogic;
using EFT.Animations;
using EFT.CameraControl;
using EFT.UI;
using EFT.UI.Settings;
using System.Collections;

namespace NonPipScopes.ExamplePatches {
	public struct FovData {
		public float BaseFOV;
		public float Zoom;
		public float ResultFOV;
	}

	public class FovManager {
		public Option<FovData> FovDataOption;
	    public float CurrentScopeFOV;
		public bool IsAiming;

	    public void UpateScopeFOV()
	    {
	        if (CameraClass.Instance?.OpticCameraManager?.Camera) {
				CurrentScopeFOV = CameraClass.Instance.OpticCameraManager.Camera.fieldOfView;
			}
	    }

	    public void CheckAiming(Player player)
	    {
	        if (player.ProceduralWeaponAnimation) {
		        IsAiming = player.ProceduralWeaponAnimation.IsAiming;
			}
	    }

		public void Run() {
            var playerStatus = Helpers.GetPlayerStatus();
            if (playerStatus.PlayerOption.Some(out var player)) {
	            UpateScopeFOV();
	            CheckAiming(player);
			}
		}
	}
}
