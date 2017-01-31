using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;


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

		private const String audioPath = "KourageousTourists/Sounds/shutter";

		private List<Tourist> touristConfig;

		public bool smile = false;
		public bool taken = false;
		public DateTime selfieTime;
		public float whee = 0.0f;
		public float fear = 0.0f;
		private System.Random rnd;

		public Vector3 savedCameraPosition;
		public Quaternion savedCameraRotation;
		public Transform savedCameraTarget;

		private FXGroup fx = null;

		public KourageousTouristsAddOn ()
		{
		}

		public void Start()
		{
			print ("KT: Start()");

			touristConfig = new List<Tourist> ();
			readConfig ();
			selfieTime = DateTime.Now;
			rnd = new System.Random ();

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
			GameEvents.onVesselWillDestroy.Add (OnVesselWillDestroy);

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
			if (vessel == null)
				return;
			
			print ("KT: OnVesselCreated; name=" + vessel.GetName ());

			if (vessel.evaController == null) {
				print ("KT: no EVA ctrl");
				return;
			}

			if (vessel.GetVesselCrew ().Count == 0) {
				print ("KT: no crew");
				return;
			}

			if (!Tourist.isTourist(vessel)) {
				print ("KT: crew 0 is not tourist (" + vessel.GetVesselCrew () [0].trait + ")");
				return;
			}

			reinitVessel (vessel);
		}

		private void OnVesselWillDestroy(Vessel vessel) {

			if (vessel == null || vessel.evaController == null)
				return;

			if (!Tourist.isTourist (vessel))
				return;

			smile = false;
			taken = false;
			fx = null;
		}

		private void reinitVessel(Vessel vessel) {

			smile = false;
			taken = false;

			// TODO: Refactor and call when go off rails
			BaseEventList pEvents = vessel.evaController.Events;
			foreach (BaseEvent e in pEvents) {
				print ("KT: disabling event " + e.guiName);
				e.guiActive = false;
			}
			// Adding Selfie button
			BaseEventDelegate slf = new BaseEventDelegate(TakeSelfie);
			KSPEvent evt = new KSPEvent ();
			evt.active = true;
			evt.externalToEVAOnly = true;
			evt.guiActive = true;
			evt.guiActiveEditor = false;
			evt.guiName = "Take Selfie";
			evt.name = "TakeSelfie";
			BaseEvent selfie = new BaseEvent(pEvents, "Take Selfie", slf, evt);
			pEvents.Add (selfie);
			selfie.guiActive = true;
			selfie.active = true;

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

			print ("KT: Initializing sound");
			getOrCreateAudio (vessel.evaController.part.gameObject);

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
			if (vessel.evaController == null)
				return;
			reinitCrew (vessel);
			reinitVessel (vessel);
		}

		private void OnVesselChange(Vessel vessel)
		{
			print ("KT: OnVesselChange()");
			if (vessel.evaController == null)
				return;
			// OnVesselChange called after OnVesselCreate, but with more things initialized
			OnVesselCreate(vessel);
			reinitCrew(vessel);
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

			String preposition = " ";
			switch (v.situation) {
			case Vessel.Situations.LANDED:
			case Vessel.Situations.SPLASHED:
			case Vessel.Situations.PRELAUNCH:
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

		public void FixedUpdate() {

			if (smile) {
				int sec = (DateTime.Now - selfieTime).Seconds;
				if (!taken && sec > 1) {

					print ("KT: Getting snd");
					FXGroup snd = getOrCreateAudio (FlightGlobals.ActiveVessel.evaController.gameObject);
					if (snd != null) {
						snd.audio.Play ();
					}
					else print ("KT: snd is null");

					String fname = "../Screenshots/" + generateSelfieFileName ();
					print ("KT: wrting file " + fname);
					Application.CaptureScreenshot (fname);
					taken = true;
				}

				if (sec > 5) {
					smile = false;
					taken = false;

					/*FlightCamera camera = FlightCamera.fetch;
					camera.transform.position = savedCameraPosition;
					camera.transform.rotation = savedCameraRotation;
					camera.SetTarget (savedCameraTarget, FlightCamera.TargetMode.Transform);*/

					//FlightGlobals.ActiveVessel.evaController.part.Events ["TakeSelfie"].active = true;
					GameEvents.onShowUI.Fire ();
					ScreenMessages.PostScreenMessage ("Selfie end");
				}
				else
					Smile ();
			}
		}

		public void TakeSelfie() {

			ScreenMessages.PostScreenMessage ("Selfie...!");
			smile = true;
			selfieTime = DateTime.Now;
			int type = rnd.Next (-1, 2);
			whee = (float)type;
			fear = (float)-type;

			//FlightGlobals.ActiveVessel.evaController.part.Events ["TakeSelfie"].active = false;
			GameEvents.onHideUI.Fire();
			print ("KT: Selfie with whee=" + whee + "; fear=" + fear);

			/*FlightCamera camera = FlightCamera.fetch;
			savedCameraPosition = camera.transform.position;
			savedCameraRotation = camera.transform.rotation;
			savedCameraTarget = camera.Target;
			camera.SetTargetNone ();*/
		}

		private void Smile() {
			KerbalEVA eva = FlightGlobals.ActiveVessel.evaController;
			if (eva != null) {
				kerbalExpressionSystem expression = getOrCreateExpressionSystem(eva);

				if (expression != null) {
					
					expression.wheeLevel = whee;
					expression.fearFactor = fear;

					/*FlightCamera camera = FlightCamera.fetch;
					camera.transform.position = eva.transform.position + Vector3.forward * 2;
					camera.transform.rotation = eva.transform.rotation;*/

				} else {
					print ("KT: Slf: No expression system");
				}
			} else
				print ("KT: Slf: No EVA ctl");
		}

		private FXGroup getOrCreateAudio(GameObject obj) {

			if (obj == null) {
				print ("KT: GameObject is null");
				return null;
			}

			if (fx != null) {
				print ("KT: returning audio from cache");
				return fx;
			}

			fx = new FXGroup ("SelfieShutter");

			fx.audio = obj.AddComponent<AudioSource> ();
			print ("KT: created audio source: " + fx.audio);
			fx.audio.volume = GameSettings.SHIP_VOLUME;
			fx.audio.rolloffMode = AudioRolloffMode.Logarithmic;
			fx.audio.dopplerLevel = 0.0f;
			fx.audio.maxDistance = 30;
			fx.audio.loop = false;
			fx.audio.playOnAwake = false;
			if (GameDatabase.Instance.ExistsAudioClip (audioPath)) {
				fx.audio.clip = GameDatabase.Instance.GetAudioClip (audioPath);
				print ("KT: Attached clip: " + GameDatabase.Instance.GetAudioClip (audioPath));
			} else
				print ("KT: No clip found with path " + audioPath);

			return fx;
		}

		private String generateSelfieFileName() {

			// KerbalName-CelestialBody-Time
			Vessel v = FlightGlobals.ActiveVessel;
			ProtoCrewMember crew = v.GetVesselCrew () [0];
			return crew.name + "-" + v.mainBody.name + "-" + DateTime.Now.ToString("yy-MM-dd-HH:mm:ss") + ".png";
		}

		private String dumper<T>(T obj) {
			if (obj == null)
				return "null";
			StringBuilder sb = new StringBuilder();
			try {
				var t = typeof(T);
				var props = t.GetProperties();
				if (props == null)
					return "type: " + t.ToString () + "; props=null";
				
				foreach (var item in props)
				{
					sb.Append($"{item.Name}:{item.GetValue(obj,null)}; ");
				}
				sb.AppendLine();
			}
			catch (Exception e) {
				sb.Append ("Exception while trying to dump object: " + e.ToString ());
			}
			return sb.ToString ();
		}

		private kerbalExpressionSystem getOrCreateExpressionSystem(KerbalEVA p) {

			kerbalExpressionSystem e = p.part.GetComponent<kerbalExpressionSystem>();
			/*print ("KT: expr. system: " + dumper(e));
			print ("KT: kerbalEVA: " + dumper(p));
			print ("KT: part: " + dumper(p.part));*/

			if (e == null) {

				AvailablePart evaPrefab = PartLoader.getPartInfoByName ("kerbalEVA");
				//print ("KT: eva prefab: " + dumper (evaPrefab));
				Part prefabEvaPart = evaPrefab.partPrefab;
				//print ("KT: eva prefab part: " + prefabEvaPart);

				ProtoCrewMember protoCrew = FlightGlobals.ActiveVessel.GetVesselCrew () [0];
				//print ("KT: proto crew: " + protoCrew);

				//var prefabExpr = prefabEva.GetComponent<kerbalExpressionSystem> ();

				Animator a = p.part.GetComponent<Animator> ();
				if (a == null) {
					print ("KT: Creating Animator...");
					var prefabAnim = prefabEvaPart.GetComponent<Animator> ();
					//print ("KT: animator prefab: " + dumper(prefabAnim));
					a = p.part.gameObject.AddComponent<Animator> ();
					//print ("KT: animator component: " + dumper(a));

					a.avatar = prefabAnim.avatar;
					a.runtimeAnimatorController = prefabAnim.runtimeAnimatorController;

					a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
					a.rootRotation = Quaternion.identity;
					a.applyRootMotion = false;

					//Animator.rootPosition = new Vector3(0.4f, 1.5f, 0.4f);
					//Animator.rootRotation = new Quaternion(-0.7f, 0.5f, -0.1f, -0.5f);
				}

				print ("KT: Creating kerbalExpressionSystem...");
				e = p.part.gameObject.AddComponent<kerbalExpressionSystem> ();
				e.evaPart = p.part;
				e.animator = a;
				e.protoCrewMember = protoCrew;
				//print ("KT: expression component: " + dumper (e));
			}
			return e;
		}
	}
}
