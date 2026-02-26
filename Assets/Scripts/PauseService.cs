using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum PauseType
{
    None = 0,
    Simulation = 1 << 0,
    Input = 1 << 1,
    Audio = 1 << 2
}

public static class PauseService
{
    private static int simulationLocks;
    private static int inputLocks;
    private static int audioLocks;
    private static PauseType activePauseTypes;
    private static float resumeTimeScale = 1f;
    private static readonly HashSet<int> simulationPauseBypassIds = new HashSet<int>();

    public static PauseType ActivePauseTypes => activePauseTypes;
    public static event Action<PauseType> PauseChanged;

    public static void Pause(PauseType pauseTypes)
    {
        if ((pauseTypes & PauseType.Simulation) != 0) simulationLocks++;
        if ((pauseTypes & PauseType.Input) != 0) inputLocks++;
        if ((pauseTypes & PauseType.Audio) != 0) audioLocks++;
        RecomputePauseState();
    }

    public static void Resume(PauseType pauseTypes)
    {
        if ((pauseTypes & PauseType.Simulation) != 0) simulationLocks = Mathf.Max(0, simulationLocks - 1);
        if ((pauseTypes & PauseType.Input) != 0) inputLocks = Mathf.Max(0, inputLocks - 1);
        if ((pauseTypes & PauseType.Audio) != 0) audioLocks = Mathf.Max(0, audioLocks - 1);
        RecomputePauseState();
    }

    public static void SetPaused(PauseType pauseTypes, bool paused)
    {
        if (paused) Pause(pauseTypes);
        else Resume(pauseTypes);
    }

    public static bool IsPaused(PauseType pauseType) => (activePauseTypes & pauseType) != 0;

    public static bool IsPaused(PauseType pauseType, UnityEngine.Object context)
    {
        if (!context) return IsPaused(pauseType);

        if ((pauseType & PauseType.Simulation) != 0 && simulationPauseBypassIds.Contains(context.GetInstanceID()))
            pauseType &= ~PauseType.Simulation;

        return IsPaused(pauseType);
    }

    public static void SetSimulationPauseBypass(UnityEngine.Object context, bool enabled)
    {
        if (!context) return;

        var id = context.GetInstanceID();
        if (enabled) simulationPauseBypassIds.Add(id);
        else simulationPauseBypassIds.Remove(id);
    }

    public static void ClearAll()
    {
        simulationLocks = 0;
        inputLocks = 0;
        audioLocks = 0;
        simulationPauseBypassIds.Clear();
        RecomputePauseState();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        simulationLocks = 0;
        inputLocks = 0;
        audioLocks = 0;
        activePauseTypes = PauseType.None;
        resumeTimeScale = 1f;
        simulationPauseBypassIds.Clear();
        AudioListener.pause = false;
    }

    private static void RecomputePauseState()
    {
        var nextPauseTypes = PauseType.None;
        if (simulationLocks > 0) nextPauseTypes |= PauseType.Simulation;
        if (inputLocks > 0) nextPauseTypes |= PauseType.Input;
        if (audioLocks > 0) nextPauseTypes |= PauseType.Audio;

        if (nextPauseTypes == activePauseTypes) return;

        var previousPauseTypes = activePauseTypes;
        activePauseTypes = nextPauseTypes;
        ApplyPauseState(previousPauseTypes, activePauseTypes);
        PauseChanged?.Invoke(activePauseTypes);
    }

    private static void ApplyPauseState(PauseType previousPauseTypes, PauseType nextPauseTypes)
    {
        var wasSimulationPaused = (previousPauseTypes & PauseType.Simulation) != 0;
        var isSimulationPaused = (nextPauseTypes & PauseType.Simulation) != 0;
        if (wasSimulationPaused != isSimulationPaused)
        {
            if (isSimulationPaused)
            {
                if (Time.timeScale > 0f) resumeTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = resumeTimeScale <= 0f ? 1f : resumeTimeScale;
            }
        }

        AudioListener.pause = (nextPauseTypes & PauseType.Audio) != 0;
    }
}
