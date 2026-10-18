using Comfort.Common;
using EFT;

namespace SPTushonka.Reflection.Utils
{
    // Compatibility shim for the helper removed from SPTushonka.Reflection in 5.0.
    internal static class ClientAppUtils
    {
        public static TarkovApplication GetMainApp()
        {
            if (!Singleton<ClientApplication<EFT.IEftSession>>.Instantiated)
            {
                return null;
            }

            return Singleton<ClientApplication<EFT.IEftSession>>.Instance as TarkovApplication;
        }
    }
}
