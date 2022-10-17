using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace TimezonesGeojsonThinOut
{
    /// <summary>
    /// 時差境界
    /// </summary>
    class Zone
    {
        /// <summary>
        /// ゾーン名
        /// </summary>
        internal string Name { get; set; } = "";

        /// <summary>
        /// tzid
        /// </summary>
        internal string Tzid { get; set; } = "";

        /// <summary>
        /// オフセット　分単位
        /// </summary>
        internal short OffsetMin { get; set; } = 0;

        /// <summary>
        /// 頂点の配列
        /// </summary>
        internal PointF[] Vertexes { get; set; } = new PointF[0];
    }
}
