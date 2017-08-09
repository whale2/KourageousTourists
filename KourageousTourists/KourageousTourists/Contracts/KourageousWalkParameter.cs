using System;
using Contracts;

namespace KourageousTourists
{
	public class KourageousWalkParameter: ContractParameter 
	{
		private CelestialBody targetBody;
		private string tourist;

		public KourageousWalkParameter() {
			this.targetBody = Planetarium.fetch.Home;
			this.tourist = "Unknown";
			KourageousTouristsAddOn.printDebug ("default constructor");
		}

		public KourageousWalkParameter(CelestialBody target, String kerbal) {
			this.targetBody = target;
			this.tourist = String.Copy(kerbal);
			KourageousTouristsAddOn.printDebug (String.Format("constructor: {0}, {1}",
				target.bodyName, this.tourist
			));
		}

		protected override string GetHashString() {
			return "walk" + this.targetBody.bodyName + this.tourist;
		}

		protected override string GetTitle() {
			return String.Format ("Let {0} walk on the surface of {1}",
				tourist, targetBody.bodyName);
		}

		protected override void OnRegister() {
			KourageousTouristsAddOn.printDebug ("setting event OnEva");
			GameEvents.onCrewOnEva.Add (OnEva);
			GameEvents.onVesselSituationChange.Add (OnSituationChange);
		}

		protected override void OnUnregister() {
			GameEvents.onCrewOnEva.Remove (OnEva);
			GameEvents.onVesselSituationChange.Remove (OnSituationChange);
		}

		private void OnSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data) {
			if (!data.host.isEVA)
				return;
			
			checkCompletion (data.host);
		}

		private void OnEva(GameEvents.FromToAction<Part, Part> action) {
			Vessel v = action.to.vessel;
			KourageousTouristsAddOn.printDebug (
				String.Format("triggered; vessel: {0}, {1}; param tourist: {2}; body: {3}; vessel situation: {4}; vessel body: {5}",
					action.to.vessel, action.from.vessel, this.tourist, this.targetBody.bodyName, v.situation, v.mainBody.bodyName));
			
			checkCompletion (v);
		}

		private void checkCompletion(Vessel v) {

			foreach(ProtoCrewMember c in v.GetVesselCrew())
				KourageousTouristsAddOn.printDebug (
					String.Format("param vessel crew: {0}",c.name));
			if (
				v.mainBody == targetBody &&
				v.GetVesselCrew () [0].name.Equals(tourist) &&
				v.situation == Vessel.Situations.LANDED)
				base.SetComplete ();
		}

		protected override void OnLoad (ConfigNode node)
		{
			KourageousTouristsAddOn.printDebug (
				String.Format("loading; node: {0}",node));
		
			int bodyID = int.Parse(node.GetValue ("targetBody"));
			KourageousTouristsAddOn.printDebug (
				String.Format("loading; bodyID: {0}",bodyID));
			foreach (var body in FlightGlobals.Bodies)
				if (body.flightGlobalsIndex == bodyID) {
					targetBody = body;
					break;
				}
			KourageousTouristsAddOn.printDebug (
				String.Format("loading; body: {0}", targetBody.bodyName));

			this.tourist = String.Copy(node.GetValue ("tourist"));
			KourageousTouristsAddOn.printDebug (
				String.Format("loading; tourist: {0}",tourist));
		}
		protected override void OnSave (ConfigNode node)
		{
			int bodyID = targetBody.flightGlobalsIndex;
			node.AddValue ("targetBody", bodyID);
			node.AddValue ("tourist", tourist);
		}
	}

}

