using System.Collections.Generic;

namespace KourageousTourists
{
	public static class EVASupport
	{
		private static readonly HashSet<string> EVENT_WHITELIST = new HashSet<string>() {
			"ChangeHelmet", "ChangeNeckRing", "OnDeboardSeat"
		};

		private static readonly HashSet<string> EVENT_BLACKLIST = new HashSet<string>() {
			"MakeReference"
		};

		public static void disableEvaEvents(Vessel v, bool isEvaEnabled)
		{
			KerbalEVA evaCtl;
			if (null == (evaCtl = v.evaController)) return;
			disableEvaEvents(evaCtl, isEvaEnabled);
		}

		public static void disableEvaEvents(Part p, bool isEvaEnabled)
		{
			KerbalEVA evaCtl;
			if (null == (evaCtl = p.Modules.GetModule<KerbalEVA>())) return;
			disablePartEvents(p, isEvaEnabled);
			disableEvaEvents(evaCtl, isEvaEnabled);
		}

		public static bool isHelmetOn(Vessel v)
		{
			List<ProtoCrewMember> roster = v.GetVesselCrew ();
			if (0 == roster.Count) return false;
			ProtoCrewMember crew = roster[0];
			return crew.hasHelmetOn;
		}

		public static void equipHelmet(Vessel v)
		{
			KerbalEVA evaCtl;
			if (null == (evaCtl = v.evaController)) return;
			evaCtl.ToggleHelmet(true);
		}

		public static void removeHelmet(Vessel v)
		{
			KerbalEVA evaCtl;
			if (null == (evaCtl = v.evaController)) return;
			evaCtl.ToggleHelmet(false);
		}

		private static void disablePartEvents(Part p, bool isEvaEnabled)
		{
			foreach (BaseEvent e in p.Events) {
				// Preserving the actions needed for EVA. These events should not be preserved if the Tourist can't EVA!
				if (isEvaEnabled && EVENT_WHITELIST.Contains(e.name)) continue;

				// Everything not in the Black List will stay
				if (!EVENT_BLACKLIST.Contains(e.name)) continue;

				printDebug("disabling event {0} -- {1}", e.name, e.guiName);
				e.guiActive = false;
				e.guiActiveUnfocused = false;
				e.guiActiveUncommand = false;
			}
		}

		private static void disableEvaEvents(KerbalEVA evaCtl, bool isEvaEnabled)
		{
			foreach (BaseEvent e in evaCtl.Events) {
				// Preserving the actions needed for EVA. These events should not be preserved if the Tourist can't EVA!
				if (isEvaEnabled && EVENT_WHITELIST.Contains(e.name)) continue;

				printDebug("disabling event {0} -- {1}", e.name, e.guiName);
				e.guiActive = false;
				e.guiActiveUnfocused = false;
				e.guiActiveUncommand = false;
			}

			// ModuleScienceExperiment is only supported on KSP 1.7 **with** Breaking Ground installed, but it does not hurt
			// saving a DLL on the package by shoving the code here.
			foreach (PartModule m in evaCtl.part.Modules) {
				if (!m.ClassName.Equals ("ModuleScienceExperiment"))
					continue;
				printDebug("science module id: {0}", ((ModuleScienceExperiment)m).experimentID);
				// Disable all science
				foreach (BaseEvent e in m.Events) {
					printDebug("disabling event {0}", e.guiName);
					e.guiActive = false;
					e.guiActiveUnfocused = false;
					e.guiActiveUncommand = false;
				}

				foreach (BaseAction a in m.Actions)
				{
					printDebug("disabling action {0}", a.guiName);
					a.active = false;
				}
			}
		}

		private static void printDebug(string message, params object[] @params)
		{
			KourageousTourists.KourageousTouristsAddOn.printDebug(message, @params);
		}
	}
}
