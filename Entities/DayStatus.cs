using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public class DayStatus
    {
        public bool IsOkay { get; set; }
        public int[] ErrorIds { get; set; }
        public int NumOfDiffs { get; set; }
        public int CancelledDiffs { get; set; }
        public DayStatus() { }
    }
}
