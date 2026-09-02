// Round-trip test for ReplayFile/ReplayData - runs outside the game.
using System;
using System.Collections.Generic;
using CulticReplay;
using UnityEngine;

public static class SerializationTest
{
    private static int failures;

    private static void Check(bool ok, string what)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL: " + what);
        }
    }

    private static bool V3Eq(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f && Mathf.Abs(a.z - b.z) < 0.0001f;
    }

    private static bool V2Eq(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    public static int Main()
    {
        ReplayData d = new ReplayData();
        d.sceneName = "sceneE1M1";
        d.recordedAt = "2026-08-22 21:00:00";
        d.duration = 3.5f;

        int s0 = d.InternSprite("cultist_idle_0");
        int s1 = d.InternSprite("player_run_7");
        int s2 = d.InternSprite("cultist_idle_0"); // dedupe
        int tracerSprite = d.InternSprite("___tracer___");
        Check(s0 == s2, "sprite interning dedupes");
        Check(s1 == 1, "second sprite gets index 1");

        ActorDef a1 = new ActorDef();
        a1.id = 1; a1.typeName = "Handgun Cultist"; a1.isPlayer = false;
        LayerHeader h1 = new LayerHeader();
        h1.lossyScale = new Vector3(2f, -1.5f, 1f);
        h1.sortOrder = 5; h1.sortingLayerId = 12345;
        h1.r = 1f; h1.g = 0.5f; h1.b = 0.25f; h1.a = 1f;
        a1.layers.Add(h1);
        d.actors.Add(a1);

        ActorDef a2 = new ActorDef();
        a2.id = 2; a2.typeName = "Player"; a2.isPlayer = true;
        d.actors.Add(a2);

        ActorDef a3 = new ActorDef();
        a3.id = 3; a3.typeName = "prefabEnemyBullet(Clone)"; a3.isProjectile = true;
        d.actors.Add(a3);

        for (int i = 0; i < 4; i++)
        {
            ReplayFrame f = new ReplayFrame();
            f.t = i * 0.5f;
            f.st = i * 0.4f;
            f.ts = (i == 1) ? 0.02f : 1f;
            f.camPos = new Vector3(-12.5f + i, 300.25f, -0.001f * i);
            f.camPitch = 88f - i;
            f.camYaw = 359f + i;
            f.wpSprite = (i == 3) ? -1 : 0;
            f.wpPos = new Vector2(120f + i, -40f);
            f.wpSize = new Vector2(300f, 150f);
            f.wpRotZ = 5f * i;

            ActorFrame af = new ActorFrame();
            af.actorId = 1;
            LayerState ls = new LayerState();
            if (i == 2)
            {
                ls.spriteIdx = -1; // hidden layer case
            }
            else
            {
                ls.spriteIdx = (i % 2);
                ls.pos = new Vector3(-1000f + i, 42.125f, 7f);
                ls.yaw = 270f + i * 90f;
                ls.flipX = (i == 1);
                ls.aux = 1.5f + i;
            }
            af.layers.Add(ls);
            f.entries.Add(af);

            ActorFrame empty = new ActorFrame();
            empty.actorId = 2; // actor with zero layers must survive
            f.entries.Add(empty);

            ActorFrame projectile = new ActorFrame();
            projectile.actorId = 3;
            LayerState tracer = new LayerState();
            tracer.spriteIdx = tracerSprite;
            tracer.pos = new Vector3(i, 1f, 2f);
            tracer.yaw = 90f;
            tracer.aux = 0.75f + i;
            tracer.pitch = -15f + i;
            projectile.layers.Add(tracer);
            f.entries.Add(projectile);

            d.frames.Add(f);
        }

        a2.isProjectile = false;
        AudioEvent ev0 = new AudioEvent();
        ev0.st = 0.4f; ev0.clip = "shotgun_fire"; ev0.pos = new Vector3(1f, -2f, 3f);
        ev0.vol = 0.8f; ev0.pitch = 1.1f;
        d.audio.Add(ev0);
        EffectEvent fx0 = new EffectEvent();
        fx0.st = 0.8f; fx0.prefab = "prefabExplosion";
        fx0.pos = new Vector3(-4f, 1.5f, 12f);
        fx0.rot = new Quaternion(0f, 0.3826834f, 0f, 0.9238795f);
        d.effects.Add(fx0);
        d.uiRefRes = new Vector2(1920f, 1080f);
        d.uiMatchMode = 0; d.uiMatch = 0.5f;

        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cshrep_roundtrip.cshrep");
        ReplayFile.Write(path, d);
        ReplayData r = ReplayFile.Read(path);

        Check(r.sceneName == d.sceneName, "sceneName round-trips");
        Check(r.formatVersion == ReplayFile.FormatVersion && r.formatVersion == 4,
            "v4 format version is reported");
        Check(r.recordedAt == d.recordedAt, "recordedAt round-trips");
        Check(Mathf.Abs(r.duration - d.duration) < 0.0001f, "duration round-trips");
        Check(r.spriteTable.Count == 3, "sprite table count");
        Check(r.spriteTable[0] == "cultist_idle_0" && r.spriteTable[1] == "player_run_7" &&
            r.spriteTable[2] == "___tracer___", "sprite names round-trip");
        Check(r.actors.Count == 3, "actor count");
        Check(r.actors[0].typeName == "Handgun Cultist", "actor name round-trips");
        Check(!r.actors[0].isPlayer && r.actors[1].isPlayer, "isPlayer flags round-trip");
        Check(r.actors[2].isProjectile, "projectile flag round-trips");

        LayerHeader rh = r.actors[0].layers[0];
        Check(V3Eq(rh.lossyScale, h1.lossyScale), "layer scale round-trips (incl. negative)");
        Check(rh.sortOrder == 5 && rh.sortingLayerId == 12345, "sorting round-trips");
        Check(Mathf.Abs(rh.g - 0.5f) < 0.0001f && Math.Abs(rh.b - 0.25f) < 0.0001f, "color round-trips");

        Check(r.frames.Count == 4, "frame count");
        ReplayFrame rf = r.frames[2];
        Check(V3Eq(rf.camPos, new Vector3(-10.5f, 300.25f, -0.002f)), "camera pos round-trips (negatives)");
        Check(Mathf.Abs(rf.camYaw - 361f) < 0.0001f, "yaw > 360 round-trips");
        Check(rf.entries[0].layers[0].spriteIdx == -1, "hidden-layer sentinel survives");
        Check(rf.entries[0].layers.Count == 1, "layer count preserved");

        ReplayFrame rf1 = r.frames[1];
        Check(rf1.entries[0].layers[0].flipX, "flipX round-trips");
        Check(rf1.entries[0].layers[0].spriteIdx == 1, "sprite index round-trips");
        Check(rf1.entries[1].layers.Count == 0, "zero-layer actor preserved");
        Check(rf1.entries[2].layers.Count == 1 && rf1.entries[2].layers[0].spriteIdx == tracerSprite,
            "projectile tracer sample round-trips");
        Check(Mathf.Abs(rf1.entries[2].layers[0].aux - 1.75f) < 0.0001f,
            "projectile tracer length round-trips");
        Check(Mathf.Abs(rf1.entries[2].layers[0].pitch - (-14f)) < 0.0001f,
            "projectile vertical angle round-trips");
        Check(Mathf.Abs(rf1.st - 0.4f) < 0.0001f, "scaled time round-trips");
        Check(Mathf.Abs(rf1.ts - 0.02f) < 0.0001f, "recorded timeScale round-trips");
        Check(rf1.wpSprite == 0 && Mathf.Abs(rf1.wpPos.x - 121f) < 0.0001f, "weapon UI round-trips");
        Check(Mathf.Abs(rf1.entries[0].layers[0].aux - 2.5f) < 0.0001f, "tracer length round-trips");
        Check(r.audio.Count == 1 && r.audio[0].clip == "shotgun_fire", "audio event round-trips");
        Check(Mathf.Abs(r.audio[0].st - 0.4f) < 0.0001f && Mathf.Abs(r.audio[0].vol - 0.8f) < 0.0001f, "audio event fields round-trip");
        Check(r.effects.Count == 1 && r.effects[0].prefab == "prefabExplosion",
            "native effect event round-trips");
        Check(Mathf.Abs(r.effects[0].st - 0.8f) < 0.0001f &&
            V3Eq(r.effects[0].pos, new Vector3(-4f, 1.5f, 12f)),
            "native effect timing and position round-trip");
        Check(V2Eq(r.uiRefRes, new Vector2(1920f, 1080f)), "ui reference resolution round-trips");

        // A one-second wall-clock interval recorded at 0.1 timeScale occupies
        // 0.1 seconds on the compressed timeline. It must not be multiplied by
        // inverse timeScale a second time, and camera/actor interpolation must
        // use the same cursor.
        ReplayFrame slowA = new ReplayFrame(); slowA.t = 10f; slowA.st = 2f;
        ReplayFrame slowB = new ReplayFrame(); slowB.t = 11f; slowB.st = 2.1f;
        Check(Mathf.Abs(ReplayTimeline.FrameTime(slowB, true) - 2.1f) < 0.0001f,
            "compressed timeline uses scaled game time");
        Check(Mathf.Abs(ReplayTimeline.FrameTime(slowB, false) - 11f) < 0.0001f,
            "uncompressed timeline uses wall time");
        Check(Mathf.Abs(ReplayTimeline.Interpolation(slowA, slowB, 2.05f, true) - 0.5f) < 0.001f,
            "compressed actor/camera interpolation stays synchronized");

        string sceneOnly = ReplayFile.ReadSceneName(path);
        Check(sceneOnly == "sceneE1M1", "ReadSceneName header peek works");

        try
        {
            ReplayFile.Read(path.Replace(".cshrep", "_corrupt.cshrep")); // missing file
            Check(false, "missing file should throw");
        }
        catch (Exception)
        {
            Check(true, "");
        }

        if (failures == 0)
        {
            Console.WriteLine("PASS: all serialization checks passed.");
            return 0;
        }
        Console.WriteLine(failures + " check(s) failed.");
        return 1;
    }
}
