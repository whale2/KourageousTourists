using System;
using System.Collections.Generic;


namespace KourageousTourists
{
	public class Tourist : ProtoTourist
	{
		public String name;

		public bool smile;
		public bool taken;
		public float whee;
		public float fear;
		public System.Random rnd;


		internal Tourist() 
		{
			level = 0;
			abilities = new List<String> ();
			situations = new List<String> ();
			celestialBodies = new List<String> ();
			srfspeed = Double.NaN;
			name = "";
			whee = 0.0f;
			fear = 0.0f;
			rnd = new System.Random ();
		}

		public bool hasAbility(String ability) {

			foreach (String a in this.abilities) {
				if (a.Equals (ability))
					return true;
			}
			return false;
		}

		public static bool isTourist(ProtoCrewMember crew) {
		
			return crew.trait.Equals ("Tourist");
		}

		public void generateEmotion() {
			
			int type = rnd.Next (-1, 2);
			whee = (float)type;
			fear = (float)-type;
		}

		public EVAAttempt canEVA(Vessel v)
		{
			if (!hasAbility("EVA")) 
				return new EVAAttempt(String.Format("Level {0} tourists can not go EVA at all", 
					level), false);

			// message makes sense when they can not go EVA
			return(checkSituation (v));
		}

		private EVAAttempt checkSituation(Vessel v)
		{
			String message = "";

			// Check if our situation is among allowed
			bool situationOk = situations.Count == 0;
			foreach (String situation in situations)
				if (v.situation.ToString().Equals(situation)) {
					situationOk = true;
					break;
				}

			bool celestialBodyOk = celestialBodies.Count == 0;
			foreach (String body in celestialBodies)
				if (v.mainBody.GetName().Equals(body)) {
					celestialBodyOk = true;
					break;
				}

			bool srfSpeedOk = Double.IsNaN(srfspeed) || Math.Abs (v.srfSpeed) < srfspeed;

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
				level, preposition, v.situation.ToString ().ToLower ().Replace ("_", ""),
				v.mainBody.GetName ());
			if (!srfSpeedOk)
				message += String.Format (" and moving at speed {0:F2} m/s", v.srfSpeed);

			// message makes sense when they can not go EVA
			return new EVAAttempt(message, situationOk && celestialBodyOk && srfSpeedOk);
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
}

