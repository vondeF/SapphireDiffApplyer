using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public class ApplyStatus
    {
        public string lssd { get; set; }
        public string lsad_Gost { get; set; } // last successed applier date (по ГОСТ)
        public string lsad_NonGost { get; set; } // last successed applier date (не по ГОСТ)
        public ApplyStatus() { }
    }
}