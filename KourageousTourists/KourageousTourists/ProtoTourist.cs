using System;
using System.Collections.Generic;

namespace KourageousTourists
{
	public class ProtoTourist
	{
		public int level { get; set; }
		public List<String> abilities { get; set; }
		public List<String> situations { get; set; }
		public List<String> celestialBodies { get; set; }
		public double srfspeed { get; set; }

		public ProtoTourist ()
		{
			abilities = new List<String> ();
			situations = new List<String> ();
			celestialBodies = new List<String> ();
		}
	}
}

