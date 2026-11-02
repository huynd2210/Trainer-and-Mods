using UnityEngine;

namespace NuclearOptionMissileControl
{
    // Drawn in the player's HUD colour: a cross where the commanded path meets the ground, a diamond
    // where the missile's current path does, and one readout line with the range/time to the cross.
    internal static class Overlay
    {
        static Texture2D white;
        static GUIStyle style;
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.6f);
        static readonly int Mask = ~((int)PhysicsLayers.ExclusionZonesMask | (int)PhysicsLayers.IgnoreRaycastMask);

        internal static void Draw(Missile missile, Vector3 aimDir)
        {
            var manager = SceneSingleton<CameraStateManager>.i;
            if (missile == null || manager == null || manager.mainCamera == null) return;
            Camera cam = manager.mainCamera;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var hud = new Color(PlayerSettings.hudColorR / 255f, PlayerSettings.hudColorG / 255f, PlayerSettings.hudColorB / 255f, 1f);

            float skip = (missile.definition != null ? missile.definition.length : 3f) + 2f;
            Vector3 origin = missile.transform.position;
            bool aimHit = Cast(origin, aimDir, skip, out Vector3 aimPoint);
            Cross(cam, aimHit ? aimPoint : origin + aimDir * 5000f, 14f * scale, hud);

            Vector3 velocity = missile.rb != null ? missile.rb.velocity : Vector3.zero;
            if (velocity.sqrMagnitude > 100f)
            {
                Vector3 path = velocity.normalized;
                Diamond(cam, Cast(origin, path, skip, out Vector3 pathPoint) ? pathPoint : origin + path * 5000f, 6f * scale, hud);
            }

            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(16f * scale);
            string text = (missile.GetWeaponInfo()?.weaponName ?? "Missile") + "   " + UnitConverter.SpeedReading(missile.speed);
            if (aimHit)
            {
                float range = Vector3.Distance(origin, aimPoint);
                text += "   " + UnitConverter.DistanceReading(range) + "   " + (range / Mathf.Max(missile.speed, 1f)).ToString("F1") + "s";
            }
            text += "      [" + MissileControlPlugin.ToggleKey.Value + "] release";
            var rect = new Rect(0f, 0f, Screen.width, Screen.height - 24f * scale);
            style.normal.textColor = Shadow;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = hud;
            GUI.Label(rect, text, style);
        }

        // Ground/unit hit along a ray, falling back to the sea surface, within 30 km.
        static bool Cast(Vector3 origin, Vector3 dir, float skip, out Vector3 point)
        {
            Vector3 start = origin + dir * skip;
            if (Physics.Raycast(start, dir, out RaycastHit hit, 30000f, Mask)) { point = hit.point; return true; }
            if (dir.y < -0.001f && start.y > Datum.LocalSeaY)
            {
                float t = (Datum.LocalSeaY - start.y) / dir.y;
                if (t < 30000f) { point = start + dir * t; return true; }
            }
            point = default;
            return false;
        }

        static bool ToScreen(Camera cam, Vector3 world, out Vector2 screen)
        {
            Vector3 p = cam.WorldToScreenPoint(world);
            screen = new Vector2(p.x, Screen.height - p.y);
            return p.z > 0f;
        }

        static void Cross(Camera cam, Vector3 world, float size, Color color)
        {
            if (!ToScreen(cam, world, out Vector2 c)) return;
            float gap = size * 0.35f, t = Mathf.Max(2f, size * 0.14f);
            foreach (var pass in new[] { (Shadow, 1f), (color, 0f) })
            {
                float o = pass.Item2;
                Bar(c.x - size + o, c.y - t / 2 + o, size - gap, t, pass.Item1);
                Bar(c.x + gap + o, c.y - t / 2 + o, size - gap, t, pass.Item1);
                Bar(c.x - t / 2 + o, c.y - size + o, t, size - gap, pass.Item1);
                Bar(c.x - t / 2 + o, c.y + gap + o, t, size - gap, pass.Item1);
            }
        }

        static void Diamond(Camera cam, Vector3 world, float r, Color color)
        {
            if (!ToScreen(cam, world, out Vector2 c)) return;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, c);
            float t = Mathf.Max(2f, r * 0.3f);
            foreach (var pass in new[] { (Shadow, 1f), (color, 0f) })
            {
                float o = pass.Item2;
                Bar(c.x - r + o, c.y - r + o, 2 * r, t, pass.Item1);
                Bar(c.x - r + o, c.y + r - t + o, 2 * r, t, pass.Item1);
                Bar(c.x - r + o, c.y - r + o, t, 2 * r, pass.Item1);
                Bar(c.x + r - t + o, c.y - r + o, t, 2 * r, pass.Item1);
            }
            GUI.matrix = saved;
        }

        static void Bar(float x, float y, float w, float h, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = Color.white;
        }
    }
}
