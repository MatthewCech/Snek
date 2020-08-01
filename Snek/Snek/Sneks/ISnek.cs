using System;
using System.Collections.Generic;
using System.Text;

namespace Snek
{
    public interface ISnek
    {
        // Every snek should have a unique name!
        string Name { get; }

        // Bold of you to assume a snek can Run(), it has no legs! It *can* slither tho.
        void Slither();
    }
}
