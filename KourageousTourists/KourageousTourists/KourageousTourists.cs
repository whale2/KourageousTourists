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

		public KourageousTouristsAddOn ()
		{
		}

		public void Start()
		{
			print ("KT: Start()");

			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (factory == null)
				factory = new TouristFactory ();
			if (tourists == null)
				tourists = new Dictionary<String, Tourist> ();

			selfieTime = DateTime.Now;

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

		public void OnDestroy() {

			// Switch tourists back
			print ("KT: OnDestroy");
			foreach (Vessel v in FlightGlobals.VesselsLoaded) {
				print ("KT: restoring vessel " + v.name);
				List<ProtoCrewMember> crewList = v.GetVesselCrew ();
				foreach (ProtoCrewMember crew in crewList) {
					print ("KT: restoring crew=" + crew.name);
					if (crew.trait.Equals ("Tourist"))
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

			print ("KT: On EVA attempt");

			Tourist t;
			if (!tourists.TryGetValue (crewMemeber.name, out t))
				return;

			if (!Tourist.isTourist (crewMemeber)) // crew always can EVA
				return;

			Vessel v = FlightGlobals.ActiveVessel;
			print ("KT: Body: " + v.mainBody.GetName () + "; situation: " + v.situation);
			EVAAttempt attempt = t.canEVA(v);
			if (!attempt.status) {
				
				ScreenMessages.PostScreenMessage ("<color=orange>" + attempt.message + "</color>");
				FlightEVA.fetch.overrideEVA = true;
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

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselWillDestroy(Vessel vessel) {

			if (vessel == null || vessel.evaController == null)
				return;

			Tourist t;
			if (!tourists.TryGetValue(vessel.evaController.name, out t))
				return;

			t.smile = false;
			t.taken = false;
		}

		private void reinitVessel(Vessel vessel) {

			print ("KT: reinitVessel()");
			foreach (ProtoCrewMember crew in vessel.GetVesselCrew()) {
				print ("KT: crew = " + crew.name);
				if (tourists.ContainsKey (crew.name))
					continue;

				Tourist t = factory.createForLevel (crew.experienceLevel, crew);
				tourists.Add (crew.name, t);
				print ("KT: Added: " + crew.name);

				if (Tourist.isTourist (crew)) {
					crew.type = ProtoCrewMember.KerbalType.Crew;
					print ("KT: Tourist promotion: " + crew.name);
				}
			}
		}

		private void reinitEvents(Vessel v) {

			print ("KT: reinitEvents()");
			if (v.evaController == null)
				return;
			KerbalEVA evaCtl = v.evaController;

			String kerbalName = v.GetVesselCrew () [0].name;
			print ("KT: evCtl found; checking name: " + kerbalName);
			Tourist t;
			if (!tourists.TryGetValue(kerbalName, out t))
				return;

			print ("KT: among tourists: " + kerbalName);
			t.smile = false;
			t.taken = false;

			if (!Tourist.isTourist(v.GetVesselCrew()[0]))
				return; // not a real tourist

			// TODO: Refactor and call when go off rails
			BaseEventList pEvents = evaCtl.Events;
			foreach (BaseEvent e in pEvents) {
				print ("KT: disabling event " + e.guiName);
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
				print ("KT: science module id: " + ((ModuleScienceExperiment)m).experimentID);
				// Disable all science
				foreach (BaseEvent e in m.Events) {
					e.guiActive = false;
					e.guiActiveUnfocused = false;
					e.guiActiveUncommand = false;
				}

				foreach (BaseAction a in m.Actions)
					a.active = false;
			}

			print ("KT: Initializing sound");
			getOrCreateAudio (evaCtl.part.gameObject);

			if (!t.hasAbility ("Jetpack")) {

				ScreenMessages.PostScreenMessage (String.Format(
					"<color=orange>Jetpack shut down as tourists of level {0} are not allowed to use it</color>", 
					t.level));
				ModuleJetpackLock jl = v.GetComponent<ModuleJetpackLock> ();
				if (jl != null) {

					print ("KT: Found JetPack Lock");
					jl.disabled = true;
				} else
					print ("KT: No JetPack Lock");
			}
		}

		private void OnVesselGoOffRails(Vessel vessel)
		{
			print ("KT: OnVesselGoOffRails()");

			reinitVessel (vessel);
			reinitEvents (vessel);
		}

		private void OnVesselChange(Vessel vessel)
		{
			print ("KT: OnVesselChange()");
			if (vessel.evaController == null)
				return;
			// OnVesselChange called after OnVesselCreate, but with more things initialized
			OnVesselCreate(vessel);
		}

		private void OnFlightReady() 
		{
			print ("KT: OnFlightReady()");
			foreach (Vessel v in FlightGlobals.VesselsLoaded)
				reinitVessel (v);
		}

		private void OnVesselRecoveryRequested(Vessel vessel) 
		{
			print ("KT: OnVesselRecoveryRequested() - " + vessel.name );
			// Switch tourists back to tourists
			List<ProtoCrewMember> crewList = vessel.GetVesselCrew ();
			foreach (ProtoCrewMember crew in crewList) {
				print ("KT: crew=" + crew.name);
				if (crew.trait.Equals("Tourist"))
					crew.type = ProtoCrewMember.KerbalType.Tourist;
			}
		}

		public void FixedUpdate() {

			if (!smile)
				return;
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

		public void TakeSelfie() {

			ScreenMessages.PostScreenMessage ("Selfie...!");
			smile = true;
			selfieTime = DateTime.Now;
			foreach (Tourist t in tourists.Values)
				t.generateEmotion ();

			//FlightGlobals.ActiveVessel.evaController.part.Events ["TakeSelfie"].active = false;
			GameEvents.onHideUI.Fire();
			print ("KT: Selfie ");

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
					print ("KT: Slf: No expression system");
				}
			}
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
