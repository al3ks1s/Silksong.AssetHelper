using System;

namespace AssetHelperTesting;

internal static class Events
{
    private static Action? _onHeroStart;

    public static event Action? OnHeroStart
    {
        add
        {
            EnsureHooked();
            _onHeroStart += value;
        }
        remove
        {
            _onHeroStart -= value;
        }
    }

    private static bool _hooked;

    private static void EnsureHooked()
    {
        if (_hooked) return;
        Md.HeroController.Start.Postfix(InvokeSubscribers);
        _hooked = true;
    }

    private static void InvokeSubscribers(HeroController self)
    {
        _onHeroStart?.Invoke();
    }
}
