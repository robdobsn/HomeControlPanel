using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeControlPanel
{
    interface DeviceBase
    {
        void Control(int idx, string cmd);
        int GetVal(int idx, string valType);
        string GetString(int idx, string valType);
    }
}
