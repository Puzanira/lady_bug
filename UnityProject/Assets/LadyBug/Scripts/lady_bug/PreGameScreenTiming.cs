// Shared dwell/pause timing for pre-game attract screens: loader prompts,

namespace LadyBug
{

// start-menu instruction carousel, training trick carousel (static pages).
public static class PreGameScreenTiming
{
    // Was 5.5s (carousel), 4s (carousel start delay), 1.6s (loader) — unified
    // and bumped slightly so each page is easier to read while choosing.
    public const float PageDwellSeconds = 7f;
}
}
