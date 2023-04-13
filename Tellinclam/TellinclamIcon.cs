using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tellinclam
{
    public class TellinclamIcon : Grasshopper.Kernel.GH_AssemblyPriority
    {
        public override Grasshopper.Kernel.GH_LoadingInstruction PriorityLoad()
        {
            Grasshopper.Instances.ComponentServer.AddCategoryIcon("Clam", Properties.Resources.clam);
            Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Clam", 'C');

            return Grasshopper.Kernel.GH_LoadingInstruction.Proceed;
        }
    }
}