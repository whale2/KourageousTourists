using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;


namespace KourageousTourists
{

	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class KourageousTouristsAddOn : MonoBehaviour
	{
		

		private const String audioPath = "KourageousTourists/Sounds/shutter";

		private TouristFactory factory = null;
		public Dictionary<String, Tourist> tourists = null;

		public DateTime selfieTime;

		public Vector3 savedCameraPosition;
		public Quaternion savedCameraRotation;
		public Transform savedCameraTarget;

		bool smile = false;
		bool taken = false;
		private FXGroup fx = null;

		internal static bool debug = true;

		public KourageousTouristsAddOn ()
		{
		}

		public void Start()
		{
			printDebug ("Start()");

			GameEvents.OnVesselRecoveryRequested.Add (OnVesselRecoveryRequested);

			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (factory == null)
				factory = new TouristFactory ();
			if (tourists == null)
				tourists = new Dictionary<String, Tourist> ();

			selfieTime = DateTime.Now;

			printDebug ("Setting handlers");

			//GameEvents.onVesselChange.Add (OnVesselChange);
			GameEvents.onVesselGoOffRails.Add (OnVesselGoOffRails);
			GameEvents.onFlightReady.Add (OnFlightReady);
			GameEvents.onAttemptEva.Add (OnAttemptEVA);

			//GameEvents.onNewVesselCreated.Add (OnNewVesselCreated);
			//GameEvents.onVesselCreate.Add (OnVesselCreate);
			GameEvents.onVesselChange.Add (OnVesselChange);
			GameEvents.onVesselWillDestroy.Add (OnVesselWillDestroy);
			GameEvents.onCrewBoardVessel.Add (OnCrewBoardVessel);

			//reinitCrew (FlightGlobals.ActiveVessel);
		}

		public void OnDestroy() {

			// Switch tourists back
			printDebug ("OnDestroy");
			foreach (Vessel v in FlightGlobals.VesselsLoaded) {
				printDebug ("restoring vessel " + v.name);
				List<ProtoCrewMember> crewList = v.GetVesselCrew ();
				foreach (ProtoCrewMember crew in crewList) {
					printDebug ("restoring crew=" + crew.name);
					if (Tourist.isTourist(crew))
						crew.type = ProtoCrewMember.KerbalType.Tourist;
				}
			}

			GameEvents.onVesselGoOffRails.Remove (OnVesselGoOffRails);
			GameEvents.onFlightReady.Remove (OnFlightReady);
			GameEvents.onAttemptEva.Remove (OnAttemptEVA);
			GameEvents.onVesselChange.Remove (OnVesselChange);
			GameEvents.onVesselWillDestroy.Remove (OnVesselWillDestroy);

			tourists = null;
			factory = null; // do we really need this?
			smile = false;
			taken = false;
			fx = null;
		}

		private void OnAttemptEVA(ProtoCrewMember crewMemeber, Part part, Transform transform) {

			// Can we be sure that all in-scene kerbal tourists were configured?

			printDebug ("On EVA attempt");

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
			if (!t.hasAbility("Jetpack"))
				ScreenMessages.PostScreenMessage (String.Format(
					"<color=orange>Jetpack shut down as tourists of level {0} are not allowed to use it</color>", 
					t.level));
		}

		private void OnNewVesselCreated(Vessel vessel)
		{
			printDebug ("OnNewVesselCreated; name=" + vessel.GetName ());
		}

		private void OnVesselCreate(Vessel vessel)
		{
			if (vessel == null)
				return;
			
			printDebug ("OnVesselCreated; name=" + vessel.GetName ());

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselWillDestroy(Vessel vessel) {

			printDebug ("onVesselWllDestroy()");
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

			printDebug ("onCrewBoardVessel(): from = " + fromto.from.name + "; to = " + fromto.to.name);
			printDebug ("onCrewBoardVessel(): active vessel: " + FlightGlobals.ActiveVessel.name);

			reinitVessel (fromto.to.vessel);
		}

		private void reinitVessel(Vessel vessel) {

			printDebug ("reinitVessel()");
			foreach (ProtoCrewMember crew in vessel.GetVesselCrew()) {
				printDebug ("crew = " + crew.name);
				if (Tourist.isTourist (crew)) {
					crew.type = ProtoCrewMember.KerbalType.Crew;
					printDebug ("Tourist promotion: " + crew.name);
				}

				if (tourists.ContainsKey (crew.name))
					continue;

				Tourist t = factory.createForLevel (crew.experienceLevel, crew);
				tourists.Add (crew.name, t);
				printDebug ("Added: " + crew.name);
			}
		}

		private void reinitEvents(Vessel v) {

			printDebug ("reinitEvents()");
			if (v.evaController == null)
				return;
			KerbalEVA evaCtl = v.evaController;

			ProtoCrewMember crew = v.GetVesselCrew () [0];
			String kerbalName = crew.name;
			printDebug ("evCtl found; checking name: " + kerbalName);
			Tourist t;
			if (!tourists.TryGetValue(kerbalName, out t))
				return;

			printDebug ("among tourists: " + kerbalName);
			t.smile = false;
			t.taken = false;

			if (!Tourist.isTourist(v.GetVesselCrew()[0]))
				return; // not a real tourist

			// Change crew type right away to avoid them being crew after recovery
			crew.type = ProtoCrewMember.KerbalType.Tourist;

			BaseEventList pEvents = evaCtl.Events;
			foreach (BaseEvent e in pEvents) {
				printDebug ("disabling event " + e.guiName);
				e.guiActive = false;
				e.guiActiveUnfocused = false;
				e.guiActiveUncommand = false;
			}
			// Adding Selfie button
			BaseEventDelegate slf = new BaseEventDelegate(TakeSelfie);
			KSPEvent evt = new KSPEvent ();
			evt.active = true;
			evt.externalToEVAOnly = true;
			evt.guiActive = true;
			evt.guiActiveEditor = false;
			evt.guiActiveUnfocused = false;
			evt.guiActiveUncommand = false;
			evt.guiName = "Take Selfie";
			evt.name = "TakeSelfie";
			BaseEvent selfie = new BaseEvent(pEvents, "Take Selfie", slf, evt);
			pEvents.Add (selfie);
			selfie.guiActive = true;
			selfie.active = true;

			foreach (PartModule m in evaCtl.part.Modules) {

				if (!m.ClassName.Equals ("ModuleScienceExperiment"))
					continue;
				printDebug ("science module id: " + ((ModuleScienceExperiment)m).experimentID);
				// Disable all science
				foreach (BaseEvent e in m.Events) {
					e.guiActive = false;
					e.guiActiveUnfocused = false;
					e.guiActiveUncommand = false;
				}

				foreach (BaseAction a in m.Actions)
					a.active = false;
			}

			printDebug ("Initializing sound");
			// Should we always invalidate cache???
			fx = null;
			getOrCreateAudio (evaCtl.part.gameObject);

			if (!t.hasAbility ("Jetpack")) {
				ModuleJetpackLock jl = v.GetComponent<ModuleJetpackLock> ();
				if (jl != null) {

					printDebug ("Found JetPack Lock");
					jl.setLock (true);
				} else
					printDebug ("No JetPack Lock");
			}
		}

		private void OnVesselGoOffRails(Vessel vessel)
		{
			printDebug ("OnVesselGoOffRails()");

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselChange(Vessel vessel)
		{
			printDebug ("OnVesselChange()");
			if (vessel.evaController == null)
				return;
			// OnVesselChange called after OnVesselCreate, but with more things initialized
			OnVesselCreate(vessel);
		}

		private void OnFlightReady() 
		{
			printDebug ("OnFlightReady()");
			foreach (Vessel v in FlightGlobals.VesselsLoaded)
				reinitVessel (v);
		}

		private void OnVesselRecoveryRequested(Vessel vessel) 
		{
			printDebug ("OnVesselRecoveryRequested() - " + vessel.name );
			// Switch tourists back to tourists
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				printDebug ("crew=" + crew.name);
				if (Tourist.isTourist(crew))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		public void FixedUpdate() {

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
		}

		private void Smile() {

			foreach (Vessel v in FlightGlobals.Vessels) {

				if (v.evaController == null)
					continue;
				
				KerbalEVA eva = v.evaController;
				kerbalExpressionSystem expression = getOrCreateExpressionSystem (eva);

				if (expression != null) {

					Tourist t;
					if (!tourists.TryGetValue (v.GetVesselCrew()[0].name, out t))
						continue;
					
					expression.wheeLevel = t.whee;
					expression.fearFactor = t.fear;

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

				printDebug ("Creating kerbalExpressionSystem...");
				e = p.part.gameObject.AddComponent<kerbalExpressionSystem> ();
				e.evaPart = p.part;
				e.animator = a;
				e.protoCrewMember = protoCrew;
				//printDebug ("expression component: " + dumper (e));
			}
			return e;
		}

		internal static void printDebug(String message) {

			print ("KT: " + message);
		}
	}
}
