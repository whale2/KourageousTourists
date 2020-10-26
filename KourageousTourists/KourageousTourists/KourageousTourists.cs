using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections;


namespace KourageousTourists
{

	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class KourageousTouristsAddOn : MonoBehaviour
	{
		
		public const String cfgRoot = "KOURAGECONFIG";
		public const String cfgLevel = "LEVEL";
		public const String debugLog = "debuglog";
		
		private const String audioPath = "KourageousTourists/Sounds/shutter";

		private TouristFactory factory = null;
		// We keep every kerbal in scene in here just to make every one of them smile
		// on photo; some, however, clearly are not tourists
		public Dictionary<String, Tourist> tourists = null;

		public DateTime selfieTime;

		public Vector3 savedCameraPosition;
		public Quaternion savedCameraRotation;
		public Transform savedCameraTarget;

		bool smile = false;
		bool taken = false;
		private FXGroup fx = null;

		public double RCSamount;
		public double RCSMax;
		internal static bool debug = true;
		public static bool noSkyDiving = false;
		internal static float paraglidingChutePitch = 1.1f;
		internal static float paraglidingDeployDelay = 5f;
		public static float paraglidingMaxAirspeed = 100f;
		public static float paraglidingMinAltAGL = 1500f;

		bool highGee = false;

		public static EventVoid selfieListeners = new EventVoid("Selfie");

		public KourageousTouristsAddOn ()
		{
		}

		public void Awake()
		{
			printDebug ("entered");

			bool forceTouristsInSandbox = false;

			ConfigNode config = GameDatabase.Instance.GetConfigNodes(
				KourageousTouristsAddOn.cfgRoot).FirstOrDefault();
			if (config == null)
			{
				printDebug("No config nodes!");
				return;
			}
			String debugState = config.GetValue("debug");
			String noDiving = config.GetValue("noSkyDiving");
			String forceInSandbox = config.GetValue("forceTouristsInSandbox");

			try
			{
				paraglidingChutePitch = float.Parse(config.GetValue("paraglidingChutePitch"));
				paraglidingDeployDelay = float.Parse(config.GetValue("paraglidingDeployDelay"));
				paraglidingMaxAirspeed = float.Parse(config.GetValue("paraglidingMaxAirpseed"));
				paraglidingMinAltAGL = float.Parse(config.GetValue("paraglidingMinAltAGL"));
				printDebug($"paragliding params: pitch: {paraglidingChutePitch}, delay: {paraglidingDeployDelay}, " +
									$"speed: {paraglidingMaxAirspeed}, alt: {paraglidingMinAltAGL}");
			}
			catch (Exception) {
				printDebug("Failed parsing paragliding tweaks!");
			}
			
			printDebug($"debug: {debugState}; nodiving: {noDiving}; forceInSB: {forceInSandbox}");

			debug = debugState != null && 
			        (debugState.ToLower().Equals ("true") || debugState.Equals ("1"));
			noSkyDiving = noDiving != null && 
			        (noDiving.ToLower().Equals ("true") || noDiving.Equals ("1"));
			forceTouristsInSandbox = forceInSandbox != null && 
			              (forceInSandbox.ToLower().Equals ("true") || forceInSandbox.Equals ("1"));
			
			printDebug($"debug: {debug}; nodiving: {noSkyDiving}; forceInSB: {forceTouristsInSandbox}");
			printDebug($"highlogic: {HighLogic.fetch}");
			printDebug($"game: {HighLogic.CurrentGame}");

			// Ignore non-career game mode
			if (HighLogic.CurrentGame == null || 
			    (!forceTouristsInSandbox && HighLogic.CurrentGame.Mode != Game.Modes.CAREER))
			{
				return;
			}
			printDebug ("scene: " + HighLogic.LoadedScene);

			GameEvents.OnVesselRecoveryRequested.Add (OnVesselRecoveryRequested);

			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (factory == null)
				factory = new TouristFactory ();
			if (tourists == null)
				tourists = new Dictionary<String, Tourist> ();

			selfieTime = DateTime.Now;

			printDebug ("Setting handlers");

			GameEvents.onVesselGoOffRails.Add (OnVesselGoOffRails);
			GameEvents.onFlightReady.Add (OnFlightReady);
			GameEvents.onAttemptEva.Add (OnAttemptEVA);

			GameEvents.onNewVesselCreated.Add (OnNewVesselCreated);
			GameEvents.onVesselCreate.Add (OnVesselCreate);
			GameEvents.onVesselChange.Add (OnVesselChange);
			GameEvents.onVesselLoaded.Add (OnVesselLoad);
			GameEvents.onVesselWillDestroy.Add (OnVesselWillDestroy);
			GameEvents.onCrewBoardVessel.Add (OnCrewBoardVessel);
			GameEvents.onCrewOnEva.Add (OnEvaStart);
			GameEvents.onKerbalLevelUp.Add (OnKerbalLevelUp);
			GameEvents.onVesselRecovered.Add (OnVesselRecoveredOffGame);

			//reinitCrew (FlightGlobals.ActiveVessel);
		}

		public void OnDestroy() {

			// Switch tourists back
			printDebug("entered OnDestroy");
			try {
				if (null == FlightGlobals.VesselsLoaded) return;
				printDebug("VesselsLoaded: {0}", FlightGlobals.VesselsLoaded);
				foreach (Vessel v in FlightGlobals.VesselsLoaded) {
					if (null == v) continue; // Weird behaviour on KSP 1.10?
					printDebug("restoring vessel {0}", v.name);
					List<ProtoCrewMember> crewList = v.GetVesselCrew();
					if (null == v.GetVesselCrew()) continue; // Weird behaviour on KSP 1.10?
					foreach (ProtoCrewMember crew in crewList) {
						if (null == crew) continue; // Weird behaviour on KSP 1.10?
						printDebug("restoring crew={0}", crew.name);
						if (Tourist.isTourist(crew))
							crew.type = ProtoCrewMember.KerbalType.Tourist;
					}
				}
			}
			catch(Exception e) {
				printDebug("Got Exception while attempting to access loaded vessels {0}", e);
			}

			GameEvents.onVesselRecovered.Remove(OnVesselRecoveredOffGame);
			GameEvents.onKerbalLevelUp.Remove(OnKerbalLevelUp);
			GameEvents.onCrewOnEva.Remove(OnEvaStart);
			GameEvents.onCrewBoardVessel.Remove(OnCrewBoardVessel);
			GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);
			GameEvents.onVesselLoaded.Add(OnVesselLoad);
			GameEvents.onVesselChange.Remove(OnVesselChange);
			GameEvents.onVesselCreate.Remove(OnVesselCreate);
			GameEvents.onNewVesselCreated.Remove(OnNewVesselCreated);

			GameEvents.onAttemptEva.Remove(OnAttemptEVA);
			GameEvents.onFlightReady.Remove(OnFlightReady);
			GameEvents.onVesselGoOffRails.Remove (OnVesselGoOffRails);

			tourists = null;
			factory = null; // do we really need this?
			smile = false;
			taken = false;
			fx = null;
		}

		private void OnEvaStart(GameEvents.FromToAction<Part, Part> evaData) {
			
			printDebug ("entered; Parts: " + evaData.from + "; " + evaData.to);
			printDebug ("active vessel: " + FlightGlobals.ActiveVessel);
			Vessel v = evaData.to.vessel;
			if (!v || !v.evaController)
				return;
			printDebug ("vessel: " + v + "; evaCtl: " + v.evaController);
			ProtoCrewMember crew = v.GetVesselCrew () [0];
			printDebug ("crew: " + crew);
			if (this.tourists == null) {
				// Why we get here twice with the same data?
				printDebug ("for some reasons tourists is null");
				return;
			}
			foreach(KeyValuePair<String, Tourist> pair in this.tourists)
				printDebug (pair.Key + "=" + pair.Value);
			printDebug ("roster: " + this.tourists);
			Tourist t;
			if (!tourists.TryGetValue(crew.name, out t))
				return;
			printDebug ("tourist: " + t);
			if (!Tourist.isTourist (crew) || t.hasAbility ("Jetpack"))
				return;

			evaData.to.RequestResource (v.evaController.propellantResourceName, 
				v.evaController.propellantResourceDefaultAmount);
			// Set propellantResourceDefaultAmount to 0 for EVAFuel to recognize it.
			v.evaController.propellantResourceDefaultAmount = 0.0;
			
			ScreenMessages.PostScreenMessage (String.Format(
				"<color=orange>Jetpack propellant drained as tourists of level {0} are not allowed to use it</color>", 
				t.level));

			// SkyDiving...
			print(String.Format("skydiving: {0}, situation: {1}", t.looksLikeSkydiving(v), v.situation));
			if (t.looksLikeSkydiving(v)) {
				v.evaController.ladderPushoffForce = 50;
				v.evaController.autoGrabLadderOnStart = false;
				StartCoroutine (this.deployChute (v));
			}
		}

		public IEnumerator deployChute(Vessel v) {
			printDebug ("Priming chute");
			if (!v.evaController.part.Modules.Contains ("ModuleEvaChute")) {
				printDebug ("No ModuleEvaChute!!! Oops...");
				yield  break;
			}
			printDebug ("checking chute module...");
			ModuleEvaChute chuteModule = (ModuleEvaChute)v.evaController.part.Modules ["ModuleEvaChute"];
			printDebug ("deployment state: " + chuteModule.deploymentSafeState + "; enabled: " + chuteModule.enabled);
			chuteModule.deploymentSafeState = ModuleParachute.deploymentSafeStates.UNSAFE; // FIXME: is it immediate??? 

			printDebug ($"counting {paraglidingDeployDelay} sec...");
			yield return new WaitForSeconds (paraglidingDeployDelay); // 5 seconds to deploy chute. TODO: Make configurable
			printDebug ("Deploying chute");
			chuteModule.Deploy ();
			
			// Set low forward pitch so uncontrolled kerbal doesn't gain lot of speed
			chuteModule.chuteDefaultForwardPitch = paraglidingChutePitch;
		}

		private void OnAttemptEVA(ProtoCrewMember crewMemeber, Part part, Transform transform) {

			// Can we be sure that all in-scene kerbal tourists were configured?

			printDebug ("entered");

			Tourist t;
			if (!tourists.TryGetValue (crewMemeber.name, out t))
				return;

			if (!Tourist.isTourist (crewMemeber)) // crew always can EVA
				return;

			Vessel v = FlightGlobals.ActiveVessel;
			printDebug ("Body: " + v.mainBody.GetName () + "; situation: " + v.situation);
			EVAAttempt attempt = t.canEVA(v);
			if (!attempt.status) {
				
				ScreenMessages.PostScreenMessage ("<color=orange>" + attempt.message + "</color>");
				FlightEVA.fetch.overrideEVA = true;
			}
		}

		private void OnNewVesselCreated(Vessel vessel)
		{
			printDebug ("name=" + vessel.GetName ());
		}

		private void OnVesselCreate(Vessel vessel)
		{
			if (vessel == null)
				return;
			
			printDebug ("name=" + vessel.GetName ());

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselWillDestroy(Vessel vessel) {

			printDebug ("entered");
			if (vessel == null || vessel.evaController == null)
				return;

			printDebug ("eva name = " + vessel.evaController.name);
			Tourist t;
			if (!tourists.TryGetValue(vessel.evaController.name, out t))
				return;

			t.smile = false;
			t.taken = false;
		}

		private void OnCrewBoardVessel(GameEvents.FromToAction<Part, Part> fromto) {

			printDebug ("from = " + fromto.from.name + "; to = " + fromto.to.name);
			printDebug ("active vessel: " + FlightGlobals.ActiveVessel.name);

			reinitVessel (fromto.to.vessel);
		}

		private void OnKerbalLevelUp(ProtoCrewMember kerbal) {
		
			if (tourists == null || !tourists.ContainsKey (kerbal.name))
				return;
			printDebug (String.Format ("Leveling up {0}", kerbal.name)); 
			// Re-create tourist
			tourists[kerbal.name] = factory.createForLevel (kerbal.experienceLevel, kerbal);
		}

		private void checkApproachingGeeLimit() {
			
			if (FlightGlobals.ActiveVessel != null && 
					FlightGlobals.ActiveVessel.geeForce < 4.0) {// Can there be any tourist with Gee force tolerance below that?
			
				if (highGee) {
					reinitVessel (FlightGlobals.ActiveVessel);
					highGee = false;
					ScreenMessages.PostScreenMessage ("EVA prohibition cleared");
				}
				return;
			}
			if (tourists == null)
				return;
			foreach (ProtoCrewMember crew in FlightGlobals.ActiveVessel.GetVesselCrew()) {
				if (!tourists.ContainsKey(crew.name) || // not among tourists
				    !Tourist.isTourist(crew) || // not really a tourist
				    crew.type != ProtoCrewMember.KerbalType.Crew)
				{
					// was probably unpromoted
					continue;
				}

				if (crew.gExperienced / ProtoCrewMember.GToleranceMult(crew) > 50000) { // Magic number. At 60000 kerbal passes out
					
					printDebug (String.Format ("Unpromoting {0} due to high gee", crew.name));
					crew.type = ProtoCrewMember.KerbalType.Tourist;
					ScreenMessages.PostScreenMessage (String.Format (
						"{0} temporary prohibited from EVA due to experienced high Gee forces", crew.name));
					highGee = true;
				}
			}
		}

		private void reinitVessel(Vessel vessel) {

			printDebug (String.Format("entered for {0}", vessel.name));
			foreach (ProtoCrewMember crew in vessel.GetVesselCrew()) {
				printDebug ("crew = " + crew.name);
				if (Tourist.isTourist (crew)) {
					crew.type = ProtoCrewMember.KerbalType.Crew;
					printDebug ("Tourist promotion: " + crew.name);
				}

				if (tourists == null) {
					// TODO: Find out while half of the time we are getting this message
					printDebug ("for some reason tourists are null");
					continue;
				}
				if (tourists.ContainsKey (crew.name))
					continue;

				printDebug (String.Format("Creating tourist from cfg; lvl: {0}, crew: {1}", crew.experienceLevel, crew));
				Tourist t = factory.createForLevel (crew.experienceLevel, crew);
				this.tourists.Add (crew.name, t);
				printDebug ("Added: " + crew.name + " (" + this.tourists + ")");
			}
			printDebug (String.Format ("crew count: {0}", vessel.GetVesselCrew ().Count));
			if (vessel.isEVA) {
				List<ProtoCrewMember> roster = vessel.GetVesselCrew ();
				if (0 == roster.Count) return;
				if (!tourists.TryGetValue(roster[0].name, out Tourist t)) return;
				if (!Tourist.isTourist(roster[0])) return;
				if (!t.hasAbility("EVA")) EVASupport.equipHelmet(vessel);
			}
		}

		private void reinitEvents(Vessel v) {
			printDebug("entered reinitEvents for vessel {0}", v);
			foreach (Part p in v.Parts)
			{
				if (!"kerbalEVA".Equals(p.name)) continue;
				this.reinitEvents(p);
			}

			KerbalEVA evaCtl = v.evaController;
			if (null == evaCtl) return;

			List<ProtoCrewMember> roster = v.GetVesselCrew ();
			if (0 == roster.Count)
			{
				printDebug("Vessel has no crew.");
				return;
			}

			ProtoCrewMember crew = roster[0];
			String kerbalName = crew.name;
			printDebug("evCtl found; checking name: {0}", kerbalName);

			if (!tourists.TryGetValue(kerbalName, out Tourist t)) return;

			printDebug("among tourists: {0}", kerbalName);
			t.smile = false;
			t.taken = false;

			if (!Tourist.isTourist(crew)) {
				printDebug("...but is a crew, not a tourist!");
				return; // not a real tourist
			}

			// Change crew type right away to avoid them being crew after recovery
			crew.type = ProtoCrewMember.KerbalType.Tourist;

			EVASupport.disableEvaEvents(v, t.hasAbility("EVA"));
			this.addSelfie(evaCtl);

			printDebug("Initializing sound");
			// Should we always invalidate cache???
			fx = null;
			getOrCreateAudio (evaCtl.part.gameObject);
		}

		// Adding Selfie button
		private void addSelfie(KerbalEVA evaCtl)
		{
			printDebug("Adding Selfie to {0}", evaCtl.GUIName);
			BaseEventList pEvents = evaCtl.Events;
			BaseEventDelegate slf = new BaseEventDelegate(TakeSelfie);
			KSPEvent evt = new KSPEvent
			{
				active = true,
				externalToEVAOnly = true,
				guiActive = true,
				guiActiveEditor = false,
				guiActiveUnfocused = false,
				guiActiveUncommand = false,
				guiName = "Take Selfie",
				name = "TakeSelfie"
			};
			BaseEvent selfie = new BaseEvent(pEvents, "Take Selfie", slf, evt);
			pEvents.Add (selfie);
			selfie.guiActive = true;
			selfie.active = true;
		}

		private void reinitEvents(Part p) {
			printDebug("entered reinitEvents for part {0}", p.name);
			if (null == p.Modules) return;
			if (null == p.protoModuleCrew || 0 == p.protoModuleCrew.Count) return;

			foreach (ProtoCrewMember crew in p.protoModuleCrew)
			{
				String kerbalName = crew.name;
				printDebug("found crew {0}", kerbalName);
				if (this.tourists == null) {
					// Don't ask, could not figure out what's happening!
					printDebug ("for some reasons tourists is null");
					return;
				}
				if (!tourists.TryGetValue(kerbalName, out Tourist t)) continue;
				printDebug("crew {0} {1}", kerbalName, t.abilities);
				EVASupport.disableEvaEvents(p, t.hasAbility("EVA"));
				if (p.Modules.Contains<KerbalEVA>()) this.addSelfie(p.Modules.GetModule<KerbalEVA>());
			}
		}

		private void OnVesselGoOffRails(Vessel vessel)
		{
			printDebug ("entered");

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselChange(Vessel vessel)
		{
			printDebug ("entered");
			if (vessel.evaController == null)
				return;

			// OnVesselChange called after OnVesselCreate, but with more things initialized
			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselLoad(Vessel vessel)
		{
			if (vessel == null) return;
			printDebug("entered OnVesselLoad={0}", vessel.name);

			// That's the problem - somewhere in the not so near past, KSP implemented a stunt called
			// UpgradePipeline. This thing acts after the PartModule's OnLoad handler, and it injects
			// back default values from prefab into the part on loading. This was intended to allow older
			// savegames to be loaded on newer KSP (as it would inject default values on missing atributes
			// present only on the new KSP version - or to reset new defaults when things changed internally),
			// but also ended up trashing changes and atributes only available on runtime for some custom modules.

			// The aftermath is that default (stock) KerbalEVA settings are always bluntly restored on load, and
			// we need to reapply them again by brute force.

			// Interesting enough, Kerbals on Seats are autonomous, runtime created Part attached to seat.

			reinitVessel (vessel);
			reinitEvents (vessel);

		}

		private void OnFlightReady()
		{
			printDebug ("entered");
			foreach (Vessel v in FlightGlobals.VesselsLoaded)
				reinitVessel (v);
		}

		private void OnVesselRecoveryRequested(Vessel vessel) 
		{
			printDebug ("entered; vessel: " + vessel.name );
			// Switch tourists back to tourists
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				printDebug ("crew=" + crew.name);
				if (Tourist.isTourist(crew))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		private void OnVesselRecoveredOffGame(ProtoVessel vessel, bool wtf)
		{
			printDebug ("entered; vessel: " + vessel.vesselName + "; wtf: " + wtf);
			// Switch tourists back to tourists
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				printDebug ("crew=" + crew.name);
				if (Tourist.isTourist(crew))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		public void FixedUpdate() {

			if (!HighLogic.LoadedSceneIsFlight)
				return;

			checkApproachingGeeLimit ();

			if (!smile)
				return;
			
			int sec = (DateTime.Now - selfieTime).Seconds;
			if (!taken && sec > 1) {

				printDebug ("Getting snd");
				FXGroup snd = getOrCreateAudio (FlightGlobals.ActiveVessel.evaController.gameObject);
				if (snd != null) {
					snd.audio.Play ();
				}
				else printDebug ("snd is null");

				String fname = "../Screenshots/" + generateSelfieFileName ();
				printDebug ("wrting file " + fname);
				ScreenCapture.CaptureScreenshot(fname);
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
				ScreenMessages.PostScreenMessage ("Selfie taken!");
			}
			else
				Smile ();

		}

		public void TakeSelfie() {

			ScreenMessages.PostScreenMessage ("Selfie...!");
			smile = true;
			selfieTime = DateTime.Now;
			foreach (Tourist t in tourists.Values)
				t.generateEmotion ();

			//FlightGlobals.ActiveVessel.evaController.part.Events ["TakeSelfie"].active = false;
			GameEvents.onHideUI.Fire();
			printDebug ("Selfie ");

			/*FlightCamera camera = FlightCamera.fetch;
			savedCameraPosition = camera.transform.position;
			savedCameraRotation = camera.transform.rotation;
			savedCameraTarget = camera.Target;
			camera.SetTargetNone ();*/

			selfieListeners.Fire ();
		}

		private void Smile() {

			foreach (Vessel v in FlightGlobals.Vessels) {

				if (v.evaController == null)
					continue;
				
				KerbalEVA eva = v.evaController;
				kerbalExpressionSystem expression = getOrCreateExpressionSystem (eva);
				ProtoCrewMember crew = v.GetVesselCrew()[0];
				if (expression != null) {
					if (tourists.TryGetValue (crew.name, out Tourist t))
					{
						expression.wheeLevel = t.whee;
						expression.fearFactor = t.fear;
					}
					else // Allows crew members to always behave nicely!
					{
						expression.wheeLevel = 1f;
						expression.fearFactor = 0f;
					}

					/*FlightCamera camera = FlightCamera.fetch;
					camera.transform.position = eva.transform.position + Vector3.forward * 2;
					camera.transform.rotation = eva.transform.rotation;*/

				} else {
					printDebug ("Slf: No expression system");
				}
			}
		}


		// TODO: Refactor this - now we create audio every time active kerbal is changed
		private FXGroup getOrCreateAudio(GameObject obj) {

			if (obj == null) {
				printDebug ("GameObject is null");
				return null;
			}

			if (fx != null) {
				printDebug ("returning audio from cache");
				return fx;
			}

			fx = new FXGroup ("SelfieShutter");

			fx.audio = obj.AddComponent<AudioSource> ();
			printDebug ("created audio source: " + fx.audio);
			fx.audio.volume = GameSettings.SHIP_VOLUME;
			fx.audio.rolloffMode = AudioRolloffMode.Logarithmic;
			fx.audio.dopplerLevel = 0.0f;
			fx.audio.maxDistance = 30;
			fx.audio.loop = false;
			fx.audio.playOnAwake = false;
			if (GameDatabase.Instance.ExistsAudioClip (audioPath)) {
				fx.audio.clip = GameDatabase.Instance.GetAudioClip (audioPath);
				printDebug ("Attached clip: " + GameDatabase.Instance.GetAudioClip (audioPath));
			} else
				printDebug ("No clip found with path " + audioPath);

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
			/*printDebug ("expr. system: " + dumper(e));
			printDebug ("kerbalEVA: " + dumper(p));
			printDebug ("part: " + dumper(p.part));*/

			if (e == null) {

				AvailablePart evaPrefab = PartLoader.getPartInfoByName ("kerbalEVA");
				//printDebug ("eva prefab: " + dumper (evaPrefab));
				Part prefabEvaPart = evaPrefab.partPrefab;
				//printDebug ("eva prefab part: " + prefabEvaPart);

				ProtoCrewMember protoCrew = FlightGlobals.ActiveVessel.GetVesselCrew () [0];
				//printDebug ("proto crew: " + protoCrew);

				//var prefabExpr = prefabEva.GetComponent<kerbalExpressionSystem> ();

				Animator a = p.part.GetComponent<Animator> ();
				if (a == null) {
					printDebug ("Creating Animator...");
					var prefabAnim = prefabEvaPart.GetComponent<Animator> ();
					//printDebug ("animator prefab: " + dumper(prefabAnim));
					a = p.part.gameObject.AddComponent<Animator> ();
					//printDebug ("animator component: " + dumper(a));

					a.avatar = prefabAnim.avatar;
					a.runtimeAnimatorController = prefabAnim.runtimeAnimatorController;

					a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
					a.rootRotation = Quaternion.identity;
					a.applyRootMotion = false;

					//Animator.rootPosition = new Vector3(0.4f, 1.5f, 0.4f);
					//Animator.rootRotation = new Quaternion(-0.7f, 0.5f, -0.1f, -0.5f);
				}

				printDebug("Creating kerbalExpressionSystem...");
				e = p.part.gameObject.AddComponent<kerbalExpressionSystem> ();
				e.evaPart = p.part;
				e.animator = a;
				e.protoCrewMember = protoCrew;
				//printDebug ("expression component: " + dumper (e));
			}
			return e;
		}

		internal static void printDebug(String message, params object[] @params) {
			if (!debug)
				return;
			message = 0 == @params.Length ? message : string.Format(message, @params);
			StackTrace trace = new StackTrace ();
			String caller = trace.GetFrame(1).GetMethod ().Name;
			int line = trace.GetFrame (1).GetFileLineNumber ();
			print ("KT: " + caller + ":" + line + ": " + message);
		}

	}
}
