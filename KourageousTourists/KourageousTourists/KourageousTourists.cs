using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace KourageousTourists
{


	public class EVAAttempt
	{
		public bool status { get; set; }
		public String message { get; set; }

		public EVAAttempt(String message, bool status)
		{
			this.message = message;
			this.status = status;
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, true)]
	public class KourageousTouristsAddOn : MonoBehaviour
	{
		private const String cfgRoot = "KOURAGECONFIG";
		private const String cfgLevel = "LEVEL";
		private List<Tourist> touristConfig;

		public KourageousTouristsAddOn ()
		{
			touristConfig = new List<Tourist> ();
			readConfig ();
		}

		public void Start()
		{
			print ("KT: Start()");
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			print ("KT: Setting handlers");

			//GameEvents.onVesselChange.Add (OnVesselChange);
			GameEvents.onVesselGoOffRails.Add (OnVesselGoOffRails);
			GameEvents.onFlightReady.Add (OnFlightReady);
			GameEvents.onAttemptEva.Add (OnAttemptEVA);
			GameEvents.OnVesselRecoveryRequested.Add (OnVesselRecoveryRequested);
			//GameEvents.onNewVesselCreated.Add (OnNewVesselCreated);
			//GameEvents.onVesselCreate.Add (OnVesselCreate);
			GameEvents.onVesselChange.Add (OnVesselChange);

			//reinitCrew (FlightGlobals.ActiveVessel);
		}

		private void OnAttemptEVA(ProtoCrewMember crewMemeber, Part part, Transform transform) {

			print ("KT: On EVA attempt");

			if (crewMemeber.trait.Equals("Tourist")) {
				Vessel v = FlightGlobals.ActiveVessel;

				print ("KT: Body: " + v.mainBody.GetName () + "; situation: " + v.situation);
				EVAAttempt attempt = touristCanEVA(crewMemeber, v);
				if (!attempt.status) {
				
					ScreenMessages.PostScreenMessage ("<color=orange>" + attempt.message + "</color>");
					FlightEVA.fetch.overrideEVA = true;
				}
			}
		}

		private void OnNewVesselCreated(Vessel vessel)
		{
			print ("KT: OnNewVesselCreated; name=" + vessel.GetName ());
		}

		private void OnVesselCreate(Vessel vessel)
		{
			print ("KT: OnVesselCreated; name=" + vessel.GetName ());

			if (vessel.evaController == null) {
				print ("KT: no EVA ctrl");
				return;
			}

			if (vessel.GetVesselCrew ().Count == 0) {
				print ("KT: no crew");
				return;
			}

			if (!vessel.GetVesselCrew () [0].trait.Equals ("Tourist")) {
				print ("KT: crew 0 is not tourist (" + vessel.GetVesselCrew () [0].trait + ")");
				return;
			}

			BaseEventList pEvents = vessel.evaController.Events;
			foreach (BaseEvent e in pEvents) {
				print ("KT: disabling event " + e.guiName);
				e.guiActive = false;
			}

			foreach (PartModule m in vessel.evaController.part.Modules) {

				if (!m.ClassName.Equals ("ModuleScienceExperiment"))
					continue;
				print ("KT: science module id: " + ((ModuleScienceExperiment)m).experimentID);
				// Disable all science
				foreach (BaseEvent e in m.Events)
					e.guiActive = false;

				foreach (BaseAction a in m.Actions)
					a.active = false;
			}

			// Take away EVA fuel if tourist is not allowed to use it
			ProtoCrewMember crew = vessel.GetVesselCrew() [0];
			Tourist t = findTouristConfigForLvl(crew.experienceLevel);
			if (t == null) {
				Debug.Log ("KourageousTourists: Can't find config for tourists level " + crew.experienceLevel);
				return;
			}

			// I wonder if this happens before or after OnCrewOnEVA (which is 'internal and due to overhaul')
			if (!t.hasAbility ("Jetpack")) {
				Debug.Log ("KT: Pumping out EVA fuel; resource name=" + vessel.evaController.propellantResourceName);
				vessel.parts [0].RequestResource (vessel.evaController.propellantResourceName, 
					vessel.evaController.propellantResourceDefaultAmount);
				vessel.evaController.propellantResourceDefaultAmount = 0.0;
				ScreenMessages.PostScreenMessage (String.Format(
					"<color=orange>Jetpack not fueld as tourists of level {0} are not allowed to use it</color>", 
					t.level));
			}
		}

		private void OnVesselGoOffRails(Vessel vessel)
		{
			print ("KT: OnVesselGoOffRails()");
			reinitCrew (vessel);
		}

		private void OnVesselChange(Vessel Vessel)
		{
			print ("KT: OnVesselChange()");
			// OnVesselChange called after OnVesselCreate, but with more things initialized
			OnVesselCreate(Vessel);
			reinitCrew(Vessel);
		}

		private void OnFlightReady() 
		{
			print ("KT: OnFlightReady()");
			reinitCrew (FlightGlobals.ActiveVessel);
		}

		private void reinitCrew(Vessel vessel) 
		{

			print ("KT: reinitVessel()");
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				print ("KT: Crew: " + crew.ToString () + 
					"; Exp = " + crew.experience + 
					"; expLvl = " + crew.experienceLevel +
					"; trait = " + crew.trait);
				
				if (crew.type == ProtoCrewMember.KerbalType.Tourist)
					crew.type = ProtoCrewMember.KerbalType.Crew;
			}

		}

		private void OnVesselRecoveryRequested(Vessel vessel) 
		{
			print ("KT: OnVesselRecoveryRequested()");
			// Switch tourists back to tourists
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				print ("KT: crew=" + crew.name);
				if (crew.trait.Equals("Tourist"))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		private void readConfig() 
		{
			print ("KT: reading config");
			ConfigNode config = GameDatabase.Instance.GetConfigNodes(cfgRoot).FirstOrDefault();
			if (config == null) {
				print ("KT: no config found in game database");
				return;
			}

			ConfigNode[] nodes = config.GetNodes (cfgLevel);
			foreach (ConfigNode cfg in nodes) {

				String tLvl = cfg.GetValue("touristlevel");
				if (tLvl == null) {
					Debug.Log ("KourageousTourists: tourist config entry has no attribute 'level'");
					return;
				}

				print ("KT: lvl=" + tLvl);
				Tourist t = new Tourist (Int32.Parse(tLvl));

				if (cfg.HasValue("situations"))
					t.situations.AddRange(cfg.GetValue ("situations").Replace (" ", "").Split (','));
				if (cfg.HasValue("bodies"))
					t.celestialBodies.AddRange(cfg.GetValue ("bodies").Replace (" ", "").Split (','));
				if (cfg.HasValue("abilities"))
					t.abilities.AddRange(cfg.GetValue ("abilities").Replace (" ", "").Split (','));

				if (cfg.HasValue("srfspeed")) {
					String srfSpeed = cfg.GetValue ("srfspeed");
					print ("KT: srfspeed = " + srfSpeed);
					double spd = 0.0;
					if (Double.TryParse (srfSpeed, out spd))
						t.srfspeed = spd;
					else
						t.srfspeed = Double.NaN;
				}

				print ("KT: Adding cfg: " + t.ToString());
				this.touristConfig.Add (t);
			}
		}

		private Tourist findTouristConfigForLvl(int lvl) 
		{
			foreach (Tourist t in touristConfig) {
				if (t.level == lvl)
					return t;
			}
			return null;
		}

		private EVAAttempt checkSituation(ProtoCrewMember crewMember, Vessel v)
		{
			Tourist t = findTouristConfigForLvl (crewMember.experienceLevel);
			if (t == null) {
				Debug.Log ("KourageousTourists: Can't find config for tourists level " + crewMember.experienceLevel);
				return new EVAAttempt ("", false);
			}

			String message = "";

			// Check if our situation is among allowed
			bool situationOk = t.situations.Count == 0;
			foreach (String situation in t.situations)
				if (v.situation.ToString().Equals(situation)) {
					situationOk = true;
					break;
				}

			bool celestialBodyOk = t.celestialBodies.Count == 0;
			foreach (String body in t.celestialBodies)
				if (v.mainBody.GetName().Equals(body)) {
					celestialBodyOk = true;
					break;
				}

			bool srfSpeedOk = Double.IsNaN(t.srfspeed) || Math.Abs (v.srfSpeed) < t.srfspeed;

			String preposition = "";
			switch (v.situation) {
			case Vessel.Situations.LANDED:
			case Vessel.Situations.SPLASHED:
				preposition = " at ";
				break;
			case Vessel.Situations.FLYING:
			case Vessel.Situations.SUB_ORBITAL:
			case Vessel.Situations.DOCKED:
				preposition = " above ";
				break;
			}

			message = String.Format ("Level {0} tourists can not go EVA when craft is {1}{2}{3}",
				crewMember.experienceLevel, preposition, v.situation.ToString ().ToLower ().Replace ("_", ""),
				v.mainBody.GetName ());
			if (!srfSpeedOk)
				message += String.Format (" and moving at speed {0:F2} m/s", v.srfSpeed);

			// message makes sense when they can not go EVA
			return new EVAAttempt(message, situationOk && celestialBodyOk && srfSpeedOk);

		}

		private EVAAttempt touristCanEVA(ProtoCrewMember crewMember, Vessel v)
		{
			Tourist t = findTouristConfigForLvl (crewMember.experienceLevel);
			if (t == null) {
				Debug.Log ("KourageousTourists: Can't find config for tourists level " + crewMember.experienceLevel);
				return new EVAAttempt ("", false);
			}

			if (!t.hasAbility("EVA")) 
				return new EVAAttempt(String.Format("Level {0} tourists can not go EVA at all", 
					crewMember.experienceLevel), false);
					
			// message makes sense when they can not go EVA
			return(checkSituation (crewMember, v));
		}
	}
}
