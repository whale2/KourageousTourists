using System;
using System.Linq;
using UnityEngine;

namespace KourageousTourists
{
	public class JetpackLock : PartModule
	{

		public JetpackLock ()
		{
		}

		[KSPAction("Disable Jetpack")]
		public void disableJetpack(KSPActionParam ignored) {
			print ("KT: Jetpack Lock - disabling jetpack");
			vessel.ActionGroups.SetGroup (KSPActionGroup.RCS, false);
		}

		public static void addToActionGroup(Vessel v) {

			foreach (JetpackLock l in v.parts[0].Modules.OfType<JetpackLock>())
				return;  // or better clear and reinit?

			JetpackLock instance = new JetpackLock();
			print ("KT: Jetpack Lock - setting action");
			KSPAction kspAction = new KSPAction ("Disable Jetpack");
			kspAction.actionGroup = KSPActionGroup.RCS;
			BaseAction action = 
				new BaseAction (v.parts[0].Actions, "Disable Jetpack", instance.disableJetpack, kspAction);
			instance.Actions.Add (action);
			v.parts [0].Modules.Add (instance);
		}
	}
}

