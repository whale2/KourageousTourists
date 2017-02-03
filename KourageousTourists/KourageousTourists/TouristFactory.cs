using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KourageousTourists
{
	public class TouristFactory
	{

		public const String cfgRoot = "KOURAGECONFIG";
		public const String cfgLevel = "LEVEL";

		public Dictionary<int,ProtoTourist> touristConfig;
		public bool initialized = false;

		public TouristFactory ()
		{
			touristConfig = new Dictionary<int, ProtoTourist> ();
			initialized = readConfig ();
		}

		public Tourist createForLevel(int level, ProtoCrewMember crew) {

			Tourist t = new Tourist ();
			if (!initialized) {
				Debug.Log ("KT: TouristFactory not initialized, can't make tourists!");
				return t;
			}

			ProtoTourist pt;
			if (!touristConfig.TryGetValue (level, out pt)) {
				Debug.Log ("KT: Can't find config for level " + level);
				return t;
			}

			t.level = pt.level;
			t.abilities = pt.abilities;
			t.situations = pt.situations;
			t.celestialBodies = pt.celestialBodies;
			t.srfspeed = pt.srfspeed;
			t.crew = crew;
			t.rnd = new System.Random ();
			return t;
		}

		private bool readConfig() 
		{
			Debug.Log ("KT: reading config");
			ConfigNode config = GameDatabase.Instance.GetConfigNodes(cfgRoot).FirstOrDefault();
			if (config == null) {
				Debug.Log ("KT: no config found in game database");
				return false;
			}

			ConfigNode[] nodes = config.GetNodes (cfgLevel);
			foreach (ConfigNode cfg in nodes) {

				String tLvl = cfg.GetValue("touristlevel");
				if (tLvl == null) {
					Debug.Log ("KT: tourist config entry has no attribute 'level'");
					return false;
				}

				Debug.Log ("KT: lvl=" + tLvl);
				ProtoTourist t = new ProtoTourist ();
				int lvl;
				if (!Int32.TryParse (tLvl, out lvl)) {
					Debug.Log ("KT: Can't parse tourist level as int: " + tLvl);
					return false;
				}
				t.level = lvl;

				if (cfg.HasValue("situations"))
					t.situations.AddRange(cfg.GetValue ("situations").Replace (" ", "").Split (','));
				if (cfg.HasValue("bodies"))
					t.celestialBodies.AddRange(cfg.GetValue ("bodies").Replace (" ", "").Split (','));
				if (cfg.HasValue("abilities"))
					t.abilities.AddRange(cfg.GetValue ("abilities").Replace (" ", "").Split (','));

				if (cfg.HasValue("srfspeed")) {
					String srfSpeed = cfg.GetValue ("srfspeed");
					Debug.Log ("KT: srfspeed = " + srfSpeed);
					double spd = 0.0;
					if (Double.TryParse (srfSpeed, out spd))
						t.srfspeed = spd;
					else
						t.srfspeed = Double.NaN;
				}

				Debug.Log ("KT: Adding cfg: " + t.ToString());
				this.touristConfig.Add (lvl, t);
			}
			return true;
		}
	}
}

