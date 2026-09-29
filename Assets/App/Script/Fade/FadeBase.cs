using UnityEngine;

public interface FadeBase
{
    void Initialize();

    void Begin(FadePreset preset, bool fadetoblack);

    void Apply (float progress , FadePreset preset, bool fadetoblack);

    void End(FadePreset preset, bool fadetoblack);

    void ForceClear(FadePreset preset);

    void ForceBlack(FadePreset preset);

}
