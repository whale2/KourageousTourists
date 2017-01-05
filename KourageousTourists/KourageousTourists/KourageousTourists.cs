using System;
using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace KourageousTourists
{

	public class Tourist
	{
		public int level { get; set; }
		public List<String> abilities { get; set; }
		public List<String> situations { get; set; }
		public List<String> celestialBodies { get; set; }
		public double srfspeed { get; set; }

		public Tourist(int lvl) 
		{
			level = lvl;
			abilities = new List<String> ();
			situations = new List<String> ();
			celestialBodies = new List<String> ();
			srfspeed = Double.NaN;
		}

		public override String ToString()
		{
			return (String.Format("Tourist: < lvl={0}, abilities: [{1}], situations: [{2}], bodies: [{3}], speed: {4:F2} >",
				level, 
				String.Join(", ", abilities.ToArray()),
				String.Join(", ", situations.ToArray()),
				String.Join(", ", celestialBodies.ToArray()), 
				srfspeed));
		}
	}

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
		private const String configFilePath = 
			"{0}GameData/KourageousTourists/Plugins/PluginData/settings.xml";
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

			reinitCrew (FlightGlobals.ActiveVessel);
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

		private void OnVesselGoOffRails(Vessel vessel)
		{
			print ("KT: OnVesselGoOffRails()");
			reinitCrew (vessel);
		}

		private void OnVesselChange(Vessel Vessel)
		{
			print ("KT: OnVesselChange()");
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
					"; trait = " + crew.experienceTrait.Description + ", " + crew.trait);
				
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
				
				if (crew.trait.Equals("Tourist"))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		private void readConfig() 
		{
			String cfgPath = string.Format (configFilePath, KSPUtil.ApplicationRootPath);
			print ("KT: cfgpath = " + cfgPath);
			if (!File.Exists (cfgPath))
				return;

			XmlDocument xmlConfig = new XmlDocument ();
			xmlConfig.Load (cfgPath);
			XmlNode cfgNode = xmlConfig.DocumentElement.SelectSingleNode ("/config");
			if (cfgNode == null)
				return;
			print ("KT: config loaded");
			foreach(XmlNode node in cfgNode.ChildNodes)
			{
				print ("KT: cfgnode: " + node.InnerText);
				String tLvl = node.Attributes ["level"].Value;
				if (tLvl == null) {
					Debug.Log ("KourageousTourists: tourist config entry has no attribute 'level'");
					return;
				}

				print ("KT: lvl=" + tLvl);
				Tourist t = new Tourist (Int32.Parse(tLvl));

				foreach (XmlNode situation in node.SelectSingleNode("situations").ChildNodes)
					t.situations.Add (situation.InnerText);

				foreach (XmlNode body in node.SelectSingleNode("celestialbodies").ChildNodes)
					t.celestialBodies.Add (body.InnerText);

				foreach (XmlNode ability in node.SelectSingleNode("abilities").ChildNodes)
					t.abilities.Add (ability.InnerText);

				XmlNode srfSpeedNode = node.SelectSingleNode ("limitingfactors/srfspeed");
				if (srfSpeedNode != null) {
					
					print ("KT: srfspeed = " + srfSpeedNode.InnerText);
					t.srfspeed = Double.Parse (srfSpeedNode.InnerText);
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

		private EVAAttempt touristCanEVA(ProtoCrewMember crewMember, Vessel v)
		{
			Tourist t = findTouristConfigForLvl (crewMember.experienceLevel);
			if (t == null) {
				Debug.Log ("KourageousTourists: Can't find config for tourists level " + crewMember.experienceLevel);
				return new EVAAttempt ("", false);
			}

			String message = "";

			bool canEVA = false;
			foreach (String ability in t.abilities)
				if (ability.Equals ("EVA")) {
					canEVA = true;
					break;
				}

			if (!canEVA) 
				return new EVAAttempt(String.Format("Level {0} tourists can not go EVA at all", 
					crewMember.experienceLevel), false);
					
			// Check if our situation is among allowed
			bool situationOk = false;
			foreach (String situation in t.situations)
				if (v.situation.ToString().Equals(situation)) {
					situationOk = true;
					break;
				}

			bool celestialBodyOk = false;
			foreach (String body in t.celestialBodies)
				if (body.Equals("*") || v.mainBody.GetName().Equals(body)) {
					celestialBodyOk = true;
					break;
				}

			bool srfSpeedOk = Double.IsNaN(t.srfspeed) || Math.Abs (v.srfSpeed) < t.srfspeed;

			String preposition = " ";
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
	}
}

