using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    //{ "0":637903427842091224,"1":214347,"2: Mounting or unmounted if absent":true,"3: quickMounting":true,"253":192}
    internal class opMount
    {
        public bool isMounting;
        public bool quickMounting;

        public opMount(bool isMounting, bool quickMounting)
        {
            this.isMounting = isMounting;
            this.quickMounting = quickMounting;
        }
    }
}


