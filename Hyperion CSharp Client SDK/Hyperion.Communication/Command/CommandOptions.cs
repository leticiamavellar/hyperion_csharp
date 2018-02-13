using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicronOptics.Hyperion.Communication
{
    [Flags]
    public enum CommandOptions : byte
    {
        None = 0,
        SuppressMessage = 1,
        SuppressContent = 2,
        UseCompression = 4
    }
}
