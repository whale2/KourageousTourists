using System;
using System.Linq;
using UnityEngine;

namespace KourageousTourists
{
	public class ModuleJetpackLock : PartModule
	{
		[KSPField]
		public bool disabled = false;

		public float origLinPower;
		public float origRotPower;

		public float governedLinPower;
		public float governedRotPower;

		public ModuleJetpackLock ()
		{
		}

		// Seems like we can't do it via action groups. It works with usual vessel,
		// but not with kerbalEVA, so FixedUpdate()

		/*public void FixedUpdate() {

			if (disabled && vessel.evaController.JetpackDeployed) {
				ScreenMessages.PostScreenMessage (
					"<color=red>Hey! We told you you're not ready yet. Turn it off!</color>");
				vessel.evaController.ToggleJetpack ();
			}
		}*/

		public override void OnAwake() {
			base.OnAwake ();
			origLinPower = vessel.evaController.linPower;
			origRotPower = vessel.evaController.rotPower;
			setLock (disabled);
		}

		public void setLock(bool jpLock) {
			if (jpLock) {
				
				origLinPower = vessel.evaController.linPower;
				origRotPower = vessel.evaController.rotPower;
				governedLinPower = 0f;
				governedRotPower = 0f;

			} else {
				governedLinPower = origLinPower;
				governedRotPower = origRotPower;
			}
			setPower ();
		}

		public void setPower() {
			vessel.evaController.linPower = governedLinPower;
			vessel.evaController.rotPower = governedRotPower;
		}

		public override void OnActive() {
			base.OnActive ();
			setPower ();
		}
	}
}

