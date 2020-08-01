using System;
using System.Collections.Generic;
using System.Text;

namespace Snek
{
    // All snakes have a dedicated task - from bot to not, they each behave on their own.
    public interface ISnek
    {
        // Every snek should have a unique name!
        string Name { get; }

        // Bold of you to assume a snek can Run(), it has no legs! Snakes can slither tho.
        void Slither();
    }
}
