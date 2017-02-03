using System;
using System.Linq;
using UnityEngine;

namespace KourageousTourists
{
	public class ModuleJetpackLock : PartModule
	{
		[KSPField]
		public bool disabled = false;

		public ModuleJetpackLock ()
		{
		}

		// Seems like we can't do it via action groups. It works with usual vessel,
		// but not with kerbalEVA, so FixedUpdate()

		public void FixedUpdate() {

			if (disabled && vessel.evaController.JetpackDeployed) {
				ScreenMessages.PostScreenMessage (
					"<color=red>Hey! We told you you're not ready yet. Turn it off!</color>");
				vessel.evaController.ToggleJetpack ();
			}
		}
	}
}

