using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimezonesGeojsonThinOut
{
	public class Feature
	{
		public string Tzid { get; set; } = "";

		public float[][] Coordinates { get; set; }
	}
}
