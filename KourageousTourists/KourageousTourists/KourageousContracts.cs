using System;
using System.Collections.Generic;
using Contracts;
using FinePrint.Contracts.Parameters;

namespace KourageousTourists
{
	public class KourageousWalkContract : Contract
	{
		CelestialBody targetBody = null;
		private int numTourists = 0;
		private List<ProtoCrewMember> tourists;

		public KourageousWalkContract() {
			tourists = new List<ProtoCrewMember> ();
		}

		protected override bool Generate()
			//System.Type contractType, Contract.ContractPrestige difficulty, int seed, State state)
		{
			KourageousTouristsAddOn.printDebug ("entered");
			targetBody = selectNextCelestialBody ();

			numTourists = UnityEngine.Random.Range (1, 8);
			KourageousTouristsAddOn.printDebug ("num tourists: " + numTourists);
			for (int i = 0; i < numTourists; i++) {
				ProtoCrewMember tourist = CrewGenerator.RandomCrewMemberPrototype (ProtoCrewMember.KerbalType.Tourist);

				tourists.Add (tourist);
				KourageousTouristsAddOn.printDebug ("generated: " + tourist.name);

				// TODO: Add support for gender for 1.3 build
				KerbalTourParameter itinerary = new KerbalTourParameter (tourist.name); //, tourist.gender);
				// TODO: Add difficulty multiplier
				itinerary.FundsCompletion = 25000.0;
				itinerary.ReputationCompletion = 0.0f;
				itinerary.ReputationFailure = 0.0f;
				itinerary.ScienceCompletion = 0.0f;
				this.AddParameter (itinerary);

				KerbalDestinationParameter dstParameter = new KerbalDestinationParameter (
					targetBody, FlightLog.EntryType.Land, tourist.name
				);
				dstParameter.FundsCompletion = 1000.0f;
				dstParameter.FundsFailure = 0.0f;
				dstParameter.ReputationCompletion = 0.0f;
				dstParameter.ReputationFailure = 0.0f;
				dstParameter.ScienceCompletion = 0.0f;
				/*dstParameter.NestToParent (itinerary);
				dstParameter.CreateID ();
				dstParameter.AddParameter (new Contracts.Parameters.LandOnBody (targetBody));*/
				itinerary.AddParameter (dstParameter);

				KourageousWalkParameter walkParameter = new KourageousWalkParameter (targetBody, tourist.name);
				walkParameter.FundsCompletion = 3000.0;
				walkParameter.FundsFailure = 0.0;
				walkParameter.ReputationCompletion = 0.0f;
				walkParameter.ReputationFailure = 0.0f;
				walkParameter.ScienceCompletion = 0.0f;
				itinerary.AddParameter (walkParameter);

				Contracts.Parameters.RecoverKerbal recoverParameter = 
					new Contracts.Parameters.RecoverKerbal (
						String.Format("Return {0} safely and recover {1} on {2}", 
							tourist.name, 
							tourist.gender == ProtoCrewMember.Gender.Male ? "him" : "her",
							Planetarium.fetch.Home.bodyName));
				recoverParameter.AddKerbal (tourist.name);
				recoverParameter.FundsCompletion = 1000.0;
				recoverParameter.ReputationCompletion = 0.0f;
				recoverParameter.ReputationFailure = 0.0f;
				recoverParameter.ScienceCompletion = 0.0f;
				itinerary.AddParameter (recoverParameter);


			}

			base.SetExpiry ();
			base.SetScience (0.0f, targetBody);
			base.SetDeadlineYears (1, targetBody);
			base.SetReputation (2, 5, targetBody);
			base.SetFunds (2000, 7000, 18000, targetBody);


			return true;
		}

		protected override void OnAccepted() {
			KourageousTouristsAddOn.printDebug ("entered: body=" + targetBody.bodyName);
			foreach (ProtoCrewMember tourist in tourists) {
				HighLogic.CurrentGame.CrewRoster.AddCrewMember (tourist);
				KourageousTouristsAddOn.printDebug ("adding to roster: " + tourist.name);
			}
		}

		// Perhaps this is implemented somewhere in Contract.TextGen
		private string getProperTouristWord() {

			string [] numbers = new[] {
				"", "One","Two","Three","Four","Five","Six","Seven","Eight","Nine","Ten","Eleven","Twelve"
			};

			string t;
			if (this.numTourists > 13)
				t = this.numTourists.ToString();
			else
				t = numbers[this.numTourists];
			return t + " " + (this.numTourists > 1 ? "tourists" : "tourist");
		}

		private string getProperTouristWordLc() {
			string t = getProperTouristWord ();
			return char.ToLower (t [0]) + t.Substring (1);
		}

		private string getProperForMultiples() {
			return this.numTourists > 1 ? "" : "s";
		}

		public override bool CanBeCancelled() {
			// Once accepted, we can't left tourists on their own
			return false;
		}

		public override bool CanBeDeclined() {
			return true;
		}

		protected override string GetHashString() {
			KourageousTouristsAddOn.printDebug ("generating hash");
			string hash = "walkctrct" + targetBody.bodyName;
			foreach (ProtoCrewMember tourist in tourists)
				hash += tourist.name;
			KourageousTouristsAddOn.printDebug ("hash: " + hash);
			return hash;
		}

		protected override string GetTitle () {
			return String.Format("Let {0} walk on the surface of {1}",
				getProperTouristWordLc(), targetBody.bodyName);
		}

		protected override string GetDescription() {
			return String.Format (
				"{0} want{1} to practice their moon-walk by performing a real {2}-walk. Ferry them " +
				"there, let them out and return safely.", getProperTouristWord(), getProperForMultiples(),
				targetBody.bodyName);
		}

		protected override string GetSynopsys() {
			return String.Format (
				"Ferry {0} to {1} and let them walk on the surface.",
				getProperTouristWordLc(), targetBody.bodyName
			);
		}

		protected override string MessageCompleted ()
		{
			return String.Format ("You have successfully returned {0} {1} from the surface of {2}. They are pretty " +
			"impressed and had nothing but good time and brought back a lot of selfies",
				numTourists, getProperTouristWord (), targetBody.bodyName
			);
		}

		/* Not sure about those two */
		protected override void OnLoad (ConfigNode node)
		{
			int bodyID = int.Parse(node.GetValue ("targetBody"));
			foreach(var body in FlightGlobals.Bodies)
			{
				if (body.flightGlobalsIndex == bodyID)
					targetBody = body;
			}
			ConfigNode touristNode = node.GetNode ("Tourists");
			if (touristNode == null) {
				KourageousTouristsAddOn.printDebug ("Can't load tourists from save file");
				return;
			}
			foreach (ProtoCrewMember crew in HighLogic.CurrentGame.CrewRoster.Crew) {
				foreach (ConfigNode tourist in touristNode.GetNodes()) {
					if (crew.name.Equals(tourist.GetValue("name")))
						tourists.Add (crew);
				}
			}
		}

		protected override void OnSave (ConfigNode node)
		{
			int bodyID = targetBody.flightGlobalsIndex;
			node.AddValue ("targetBody", bodyID);
			ConfigNode touristNode = node.AddNode ("Tourists");
			foreach(ProtoCrewMember tourist in tourists)
				touristNode.AddValue ("name", tourist.name);
		}

		public override bool MeetRequirements ()
		{
			// Later we should offer the contract only after some other tourist contract were completed
			return true;
		}

		private CelestialBody selectNextCelestialBody() {

			List<CelestialBody> allBodies = new List<CelestialBody> ();
			getRelevantBodies(Planetarium.fetch.Sun, allBodies);
			return allBodies [UnityEngine.Random.Range (0, allBodies.Count - 1)];
		}

		private void getRelevantBodies(CelestialBody body, List<CelestialBody> bodies) {
			
			foreach (CelestialBody orbitingBody in body.orbitingBodies) {
				if (!orbitingBody.Equals(Planetarium.fetch.Home) && orbitingBody.hasSolidSurface)
					bodies.Add (orbitingBody);
				getRelevantBodies (orbitingBody, bodies);
			}
		}
	}

	public class KourageousWalkParameter: ContractParameter 
	{
		private CelestialBody targetBody;
		private String tourist;

		public KourageousWalkParameter() {
			targetBody = Planetarium.fetch.Home;
			tourist = "Unknown";
			KourageousTouristsAddOn.printDebug ("default constructor");
		}

		public KourageousWalkParameter(CelestialBody target, String kerbal) {
			this.targetBody = target;
			this.tourist = kerbal;
			KourageousTouristsAddOn.printDebug (String.Format("constructor: {0}, {1}",
				target.bodyName, kerbal
			));
		}

		protected override string GetHashString() {
			return "walk" + targetBody.bodyName + tourist;
		}

		protected override string GetTitle() {
			return String.Format ("Let {0} walk on the surface of {1}",
				tourist, targetBody.bodyName);
		}

		protected override void OnRegister() {
			KourageousTouristsAddOn.printDebug ("setting event OnEva");
			GameEvents.onCrewOnEva.Add (OnEva);
		}

		protected override void OnUnregister() {
			GameEvents.onCrewOnEva.Remove (OnEva);
		}

		private void OnEva(GameEvents.FromToAction<Part, Part> action) {
			KourageousTouristsAddOn.printDebug (
				String.Format("triggered; vessel: {0}, {1}",action.to.vessel, action.from.vessel));
			Vessel v = action.to.vessel;
			if (
				v.mainBody == targetBody &&
				v.GetVesselCrew () [0].name.Equals(tourist) &&
				v.situation == Vessel.Situations.LANDED)
				base.SetComplete ();
		}

		protected override void OnLoad (ConfigNode node)
		{
			int bodyID = int.Parse(node.GetValue ("targetBody"));
			foreach(var body in FlightGlobals.Bodies)
				if (body.flightGlobalsIndex == bodyID)
					targetBody = body;

			tourist = node.GetValue ("name");
		}
		protected override void OnSave (ConfigNode node)
		{
			int bodyID = targetBody.flightGlobalsIndex;
			node.AddValue ("targetBody", bodyID);
			node.AddValue ("name", tourist);
		}
	}

	public class KourageousKerbalDestinationParameter: KerbalDestinationParameter 
	{


		public KourageousKerbalDestinationParameter(): base() {
		}

		public KourageousKerbalDestinationParameter (
			CelestialBody targetBody, FlightLog.EntryType type, string name) : 
				base(targetBody, type, name) {
		}

		protected override string GetHashString() {
			string hash = "";
			try {
				hash = base.GetHashString();
			}
			catch (Exception e) {
				KourageousTouristsAddOn.printDebug ("Got exception in base class: " + e);
				hash = "kwdst" + targetBody.bodyName + kerbalName;
			}
			return hash;
		}
	}
}

