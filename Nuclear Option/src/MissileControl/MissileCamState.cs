using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace NuclearOptionMissileControl
{
    // Third-person view behind the controlled missile, looking along the commanded direction.
    // Mouse/view axes move the command like cockpit free-look; flight-stick axes turn it at a rate.
    internal sealed class MissileCamState : CameraBaseState
    {
        Vector3 viewDir;
        float savedNearClip;
        float fovAdjust;
        RaycastHit hit;

        public override void EnterState(CameraStateManager cam)
        {
            cam.cameraPivot.SetParent(Datum.origin);
            cam.transform.SetParent(cam.cameraPivot);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.cameraVelocity = Vector3.zero;
            savedNearClip = cam.mainCamera.nearClipPlane;
            cam.mainCamera.nearClipPlane = 0.5f;
            FlightHud.EnableCanvas(enable: false);
            CameraStateManager.cameraMode = CameraMode.chase;
            fovAdjust = 0f;
            viewDir = MissileControlPlugin.Aim.Direction;
        }

        public override void LeaveState(CameraStateManager cam)
        {
            cam.mainCamera.nearClipPlane = savedNearClip;
            cam.mainCamera.fieldOfView = cam.desiredFOV;
        }

        public override void FixedUpdateState(CameraStateManager cam) { }

        public override void UpdateState(CameraStateManager cam)
        {
            Missile missile = MissileControlPlugin.Controlled;
            if (missile == null) return;
            float dt = Time.deltaTime; // paused or slowed time steers paused or slowed
            var input = GameManager.playerInput;
            if (input != null && dt > 0f && !InputFieldChecker.InsideInputField)
            {
                float zoom = Mathf.Min(cam.mainCamera.fieldOfView / 20f, 1f);
                // Like the game's orbit view, the mouse steers only while the cursor is hidden (no menu/map open).
                float look = Cursor.visible ? 0f : 120f * PlayerSettings.viewSensitivity * MissileControlPlugin.LookSensitivity.Value * zoom * dt;
                float invert = PlayerSettings.viewInvertPitch ? -1f : 1f;
                float turn = Mathf.Clamp(input.GetAxis("Roll") + input.GetAxis("Yaw"), -1f, 1f);
                MissileControlPlugin.Aim.Steer(input.GetAxis("Pan View") * look, input.GetAxis("Tilt View") * look * invert,
                    Mathf.Clamp(input.GetAxis("Pitch"), -1f, 1f), turn, MissileControlPlugin.StickTurnRate.Value, dt);
                fovAdjust -= cam.fovChangeSpeed * zoom * input.GetAxis("FOV");
            }
            if (missile.rb != null) MissileControlPlugin.Aim.LimitLead(missile.rb.velocity, MissileControlPlugin.MaxLeadDegrees.Value);

            Vector3 aim = MissileControlPlugin.Aim.Direction;
            viewDir = Vector3.Slerp(viewDir, aim, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime)).normalized;
            float length = missile.definition != null ? missile.definition.length : 3f;
            float distance = Mathf.Max(6f, length * 3f) * MissileControlPlugin.CameraDistance.Value;
            Vector3 position = missile.transform.position - viewDir * distance + Vector3.up * (distance * 0.18f);
            if (Physics.Linecast(position + Vector3.up * 2000f, position - Vector3.up * 2f, out hit, PhysicsLayers.StaticsMask))
                position.y = Mathf.Max(position.y, hit.point.y + 2f);
            position.y = Mathf.Max(position.y, Datum.LocalSeaY + 1f);
            cam.cameraPivot.position = position;
            cam.transform.localPosition = Vector3.zero;
            cam.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            cam.cameraVelocity = missile.rb != null ? missile.rb.velocity : Vector3.zero;

            fovAdjust = Mathf.Clamp(fovAdjust, 10f - cam.desiredFOV, 100f - cam.desiredFOV);
            cam.mainCamera.fieldOfView = Mathf.Lerp(cam.mainCamera.fieldOfView, cam.desiredFOV + fovAdjust, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }
    }
}
