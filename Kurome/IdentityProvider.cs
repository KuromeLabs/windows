using System;

namespace Kurome
{
    public static class IdentityProvider
    {
        public static string GetMachineName()
        {
            return Environment.MachineName;
        }
    }
}