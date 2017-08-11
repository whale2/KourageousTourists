using System;
using System.Collections.Generic;
using Contracts;

namespace KourageousTourists
{
	public abstract class KourageousContract : Contract
	{

		protected CelestialBody targetBody = null;
		protected int numTourists;
		protected List<ProtoCrewMember> tourists;
		protected string hashString;

		public KourageousContract() : base() {
			this.tourists = new List<ProtoCrewMember> ();
			this.hashString = "";
			this.numTourists = 0;
		}

		protected CelestialBody selectNextCelestialBody() {

			List<CelestialBody> allBodies = Contract.GetBodies_Reached (false, false);
			foreach (CelestialBody body in allBodies)
				if (!body.hasSolidSurface)
					allBodies.Remove (body);
			return allBodies [UnityEngine.Random.Range (0, allBodies.Count - 1)];
		}

		// Perhaps this is implemented somewhere in Contract.TextGen
		protected string getProperTouristWord() {

			string [] numbers = new[] {
				"No", "One","Two","Three","Four","Five","Six","Seven","Eight","Nine","Ten","Eleven","Twelve"
			};

			string t;
			KourageousTouristsAddOn.printDebug ("num=" + this.numTourists);
			if (this.numTourists > 13)
				t = this.numTourists.ToString();
			else
				t = numbers[this.numTourists];
			return t;
		}

		protected string getProperTouristWordLc() {
			string t = getProperTouristWord ();
			KourageousTouristsAddOn.printDebug ("word=" + t);
			return char.ToLower (t [0]) + t.Substring (1);
		}

		protected override string GetHashString() {
			return this.hashString;
		}

		protected abstract void GenerateHashString ();

		protected override void OnLoad (ConfigNode node)
		{
			int bodyID = int.Parse(node.GetValue ("targetBody"));
			foreach(var body in FlightGlobals.Bodies)
			{
				if (body.flightGlobalsIndex == bodyID)
					targetBody = body;
			}
			ConfigNode touristNode = node.GetNode ("TOURISTS");
			KourageousTouristsAddOn.printDebug ("tourist node: " + touristNode);
			if (touristNode == null) {
				KourageousTouristsAddOn.printDebug ("Can't load tourists from save file");
				return;
			}
			foreach (string tourist in touristNode.GetValues()) {
				KourageousTouristsAddOn.printDebug ("tourist: " + tourist);
				foreach (ProtoCrewMember crew in HighLogic.CurrentGame.CrewRoster.Crew) {
					if (crew.name.Equals(tourist))
						tourists.Add (crew);
				}
			}
			this.numTourists = tourists.Count;
			KourageousTouristsAddOn.printDebug ("numtourists: " + this.numTourists + "; " + tourists.Count);
		}

		protected override void OnSave (ConfigNode node)
		{
			KourageousTouristsAddOn.printDebug ("saving " + this.numTourists + "tourists");
			KourageousTouristsAddOn.printDebug ("node: " + node.ToString());
			int bodyID = targetBody.flightGlobalsIndex;
			node.AddValue ("targetBody", bodyID);
			ConfigNode touristNode = node.AddNode ("TOURISTS");
			foreach (ProtoCrewMember tourist in this.tourists) {
				touristNode.AddValue ("touristName", tourist.name);
				KourageousTouristsAddOn.printDebug ("adding tourist: " + tourist.name);
			}
			KourageousTouristsAddOn.printDebug ("node: " + node.ToString());
			KourageousTouristsAddOn.printDebug ("tourist node: " + touristNode.ToString());
			
		}

	}
}

