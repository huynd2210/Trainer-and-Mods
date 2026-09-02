using UnityEngine;

namespace DisfigureTrainer;

/// <summary>
/// Scales gameplay speed through Unity's own timestep.
///
/// This replaces the previous ProcessClockSpeedController, which detoured
/// kernel32!QueryPerformanceCounter with a *managed* callback. That is not
/// survivable inside a CoreCLR-hosted process: the runtime itself calls QPC
/// while JITting and while building interop stubs, so the detour re-entered
/// managed code before its own constructor had finished wiring the trampoline
/// up, and the process died with a stack overflow before the window appeared.
/// </summary>
internal sealed class GameSpeedController
{
    public const float MinSpeed = 0.2f;
    public const float MaxSpeed = 3f;

    private float _speed = 1f;
    private float _baseFixedDeltaTime;
    private bool _baseCaptured;
    private bool _active;

    // The timeScale we last wrote, so Tick can tell our own value apart from one
    // the game set. Starts at the vanilla scale, which we are happy to take over.
    private float _applied = 1f;

    public float Speed => _speed;

    public void SetSpeed(float speed)
    {
        speed = Mathf.Clamp(speed, MinSpeed, MaxSpeed);

        if (Mathf.Approximately(speed, 1f))
        {
            Restore();
            return;
        }

        _speed = speed;
        _active = true;
        Apply();
    }

    /// <summary>
    /// Re-asserts the speed every frame while it is non-default. The game drives
    /// its own pause by zeroing timeScale and restoring it to 1, which would
    /// otherwise silently drop the trainer's setting on every unpause.
    /// </summary>
    public void Tick()
    {
        if (_active)
            Apply();
    }

    /// <summary>Returns Unity's timing to vanilla and stops re-asserting.</summary>
    public void Restore()
    {
        _speed = 1f;
        if (!_active)
            return;

        _active = false;
        _applied = 1f;

        // Never write timeScale while the game has it at 0 - that would unpause it.
        if (Time.timeScale != 0f)
            Time.timeScale = 1f;
        if (_baseCaptured)
            Time.fixedDeltaTime = _baseFixedDeltaTime;
    }

    private void Apply()
    {
        CaptureBaseFixedDeltaTime();

        float current = Time.timeScale;

        // 0 is the game's pause. Any other scale it set itself (the boss
        // slow-motion effect) is its effect to own - stand down and pick the
        // speed back up when it returns the clock to normal.
        if (current == 0f || (current != 1f && current != _applied))
        {
            StandDown();
            return;
        }

        _applied = _speed;
        Time.timeScale = _speed;

        // Scale the physics step with the clock so FixedUpdate keeps its vanilla
        // *real-time* rate: without this, 0.2x speed drops physics to 10 Hz and
        // 3x speed triples its CPU cost.
        Time.fixedDeltaTime = _baseFixedDeltaTime * _speed;
    }

    private void StandDown()
    {
        if (_baseCaptured && Time.fixedDeltaTime != _baseFixedDeltaTime)
            Time.fixedDeltaTime = _baseFixedDeltaTime;
    }

    private void CaptureBaseFixedDeltaTime()
    {
        if (_baseCaptured)
            return;

        // Capture the project's configured timestep rather than assuming Unity's
        // 0.02 default.
        _baseFixedDeltaTime = Time.fixedDeltaTime;
        _baseCaptured = true;
    }
}
