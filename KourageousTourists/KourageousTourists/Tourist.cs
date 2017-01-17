using System;
using System.Collections.Generic;


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

		public bool hasAbility(String ability) {

			foreach (String a in this.abilities) {
				if (a.Equals (ability))
					return true;
			}
			return false;
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

